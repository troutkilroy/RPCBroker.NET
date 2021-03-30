using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace RPCBroker
{
  public abstract class RPCClient : IRPCClient
  {
    protected string defaultDestination;
    protected string replyToDestination;
    protected IRPCSerializer serializer;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<(string type, byte[] bytes, IEnumerable<KeyValuePair<string, string>> headers)>> rpcTasks
      = new ConcurrentDictionary<string, TaskCompletionSource<(string type, byte[] bytes, IEnumerable<KeyValuePair<string, string>> headers)>>();

    private TaskCompletionSource<bool> connectWait = new TaskCompletionSource<bool>();
    private bool running;

    public string ServerDestination => defaultDestination;
    public string ReplyTo => replyToDestination;
    public event UncorrelatedResponseDelegate UncorrelatedResponseEvent;
    public event ClientLogEventDelegate LogEvent;

    public abstract void Dispose();

    /// <summary>
    /// Sends request and receives response from server. Request and response payloads implement IRPCBytesPayload
    /// </summary>
    /// <typeparam name="TRequest">IRPCBytesPayload request payload type</typeparam>
    /// <typeparam name="TResponse">IRPCBytesPayload response payload type</typeparam>
    /// <param name="msg">RPCMessage request wrapper</param>
    /// <param name="cancel"></param>
    /// <param name="requestDestination">Queue name or other destination routing on RPC server</param>
    /// <param name="timeoutWaitMilliseconds"></param>
    /// <returns></returns>
    public async Task<RPCMessage<TResponse>> RemoteCall<TRequest, TResponse>(RPCMessage<TRequest> msg,
      CancellationToken? cancel = null,
      string requestDestination = null, int timeoutWaitMilliseconds = 10000)
      where TResponse : class
      where TRequest : class
    {
      if (string.IsNullOrEmpty(requestDestination))
        requestDestination = defaultDestination;
      if (msg == null)
        throw new ArgumentNullException($"{nameof(msg)} cannot be null");
      if (serializer == null)
        throw new ArgumentNullException("No seriaizer defined");
      if (string.IsNullOrEmpty(requestDestination))
        throw new ArgumentException($"{nameof(requestDestination)} is not defined and no default destination specified");
      var correlationId = Guid.NewGuid().ToString();

      var ts = new TaskCompletionSource<(string type, byte[] bytes, IEnumerable<KeyValuePair<string, string>> headers)>(TaskCreationOptions.RunContinuationsAsynchronously);
      if (!rpcTasks.TryAdd(correlationId, ts))
      {
        throw new InvalidOperationException("Message CorrelationID already in use");
      }
      try
      {
        Task completedTask;
        var waitStartTask = connectWait.Task;
        var timedWaitStartTask = cancel.HasValue ?
          Task.WhenAny(Task.Delay(timeoutWaitMilliseconds, cancel.Value), waitStartTask) :
          Task.WhenAny(Task.Delay(timeoutWaitMilliseconds), waitStartTask);
        if (!waitStartTask.IsCompleted && await timedWaitStartTask != waitStartTask)
        {
          throw new TimeoutException("Timeout waiting for client to connect to broker");
        }
        await waitStartTask;
        var msgType = msg.GetPayloadTypeFromPayload();
        SendBytesToQueue(serializer.Serialize(msg.Payload), msgType, requestDestination, correlationId, msg.Headers);
        LogEvent?.Invoke($"RPC send msg {msgType} with correlation {correlationId} and replyTo {ReplyTo} to {requestDestination}. Payload: {JsonSerializer.Serialize(msg.Payload)}");
        var timedRequestTask = cancel.HasValue ?
          Task.WhenAny(Task.Delay(timeoutWaitMilliseconds, cancel.Value), ts.Task) :
          Task.WhenAny(Task.Delay(timeoutWaitMilliseconds), ts.Task);
        if ((completedTask = await timedRequestTask) == ts.Task)
        {
          var (type, bytes, headers) = await ts.Task;
          var responseMsg = new RPCMessage<TResponse>(serializer.Deserialize(bytes, typeof(TResponse)) as TResponse, headers);
          var headersLog = responseMsg.Headers != null ? $"Headers: {string.Join(" ", responseMsg.Headers)}" : "";
          LogEvent?.Invoke($"RPC received msg {type} with correlation {correlationId} and replyTo {ReplyTo}. Payload: {JsonSerializer.Serialize(responseMsg.Payload)} {headersLog}");
          return responseMsg;
        }
        else
        {
          // if cancelled throws here...
          await completedTask;
          throw new TimeoutException("Timeout waiting for response");
        }
      }
      catch
      {
        rpcTasks.TryRemove(correlationId, out _);
        throw;
      }
    }

    
    public Task<RPCMessage<TResponse>> RemoteCall<TResponse>(RPCOpaqueMessage msg,
      CancellationToken? cancel = null,
      string requestDestination = null, int timeoutWaitMilliseconds = 10000)
      where TResponse : class
    {
      return RemoteCall<object, TResponse>(msg, cancel, requestDestination, timeoutWaitMilliseconds);
    }

    public void Start()
    {
      lock (this)
      {
        if (running)
        {
          return;
        }

        running = true;
      }

      StartListening();
    }

    public void Stop()
    {
      lock (this)
      {
        running = false;
      }

      StopListening();
    }

    protected void NotifyConnected()
    {
      connectWait.SetResult(true);
      LogEvent?.Invoke($"RPC client with replyTo {ReplyTo} connected to broker");
    }

    protected void OnConnectionError(string err)
    {
      LogEvent?.Invoke($"RPC client with replyTo {ReplyTo} connection error: {err}");
      lock (this)
      {
        if (connectWait.Task.IsCompleted)
        {
          connectWait = new TaskCompletionSource<bool>();
        }
      }
      if (running)
      {
        ThreadPool.QueueUserWorkItem((w) => StartListening());
      }
    }

    protected void ReceivedBytesFromQueue(byte[] bytes, string type, string correlationId, IEnumerable<KeyValuePair<string, string>> headers)
    {
      if (rpcTasks.TryRemove(correlationId, out TaskCompletionSource<(string type, byte[] bytes, IEnumerable<KeyValuePair<string, string>> headers)> tsk))
      {
        tsk.TrySetResult((type, bytes, headers));
      }
      else
      {
        UncorrelatedResponseEvent?.Invoke(correlationId, type);
      }
    }

    protected abstract void SendBytesToQueue(byte[] bytes, string type, string requestDestination, string correlationId, IEnumerable<KeyValuePair<string, string>> headers);

    protected abstract void StartListening();

    protected abstract void StopListening();
  }
}