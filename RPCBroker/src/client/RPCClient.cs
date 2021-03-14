using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPCBroker
{
  public abstract class RPCClient : IRPCClient
  {
    protected string defaultDestination;
    protected IRPCSerializer serializer;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<(string type, byte[] bytes, IEnumerable<KeyValuePair<string, string>> headers)>> rpcTasks
      = new ConcurrentDictionary<string, TaskCompletionSource<(string type, byte[] bytes, IEnumerable<KeyValuePair<string, string>> headers)>>();

    private TaskCompletionSource<bool> connectWait = new TaskCompletionSource<bool>();
    private bool running;

    public string ServerDestination => defaultDestination;
    public event UncorrelatedResponseDelegate UncorrelatedResponseEvent;

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
      CancellationToken cancel,
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
      if (cancel == null)
        throw new ArgumentNullException($"{nameof(cancel)} cannot be null");
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
        if (!waitStartTask.IsCompleted && (await Task.WhenAny(Task.Delay(timeoutWaitMilliseconds, cancel), waitStartTask)) != waitStartTask)
        {
          throw new TimeoutException("Timeout waiting for client to connect to broker");
        }
        await waitStartTask;
        SendBytesToQueue(serializer.Serialize(msg.Payload), RPCMessage<TRequest>.GetPayloadTypeName(), requestDestination, correlationId, msg.Headers);
        if ((completedTask = await Task.WhenAny(Task.Delay(timeoutWaitMilliseconds, cancel), ts.Task)) == ts.Task)
        {
          var payload = await ts.Task;
          return new RPCMessage<TResponse>(serializer.Deserialize(payload.bytes, typeof(TResponse)) as TResponse, payload.headers);
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
    }

    protected void OnConnectionError(string err)
    {
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