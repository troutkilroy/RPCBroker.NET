using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPCBroker
{
  public abstract class RPCServer : IRPCServer
  {
    protected string destinationName;
    protected IRPCSerializer serializer;

    private readonly ConcurrentDictionary<string,
      (Func<object, IEnumerable<KeyValuePair<string, string>>, Task<ResponsePayloadData>> handler, Type requestType)> handlers =
      new ConcurrentDictionary<string, (Func<object, IEnumerable<KeyValuePair<string, string>>,
        Task<ResponsePayloadData>> handler, Type requestType)>();

    private bool running;

    public event RPCServerErrorLogDelegate ErrorLog;

    public string DestinationName => destinationName;

    public void OnConnectionError(string err)
    {
      ErrorLog?.Invoke(err);
      if (running)
      {
        ThreadPool.QueueUserWorkItem((w) => StartListening());
      }
    }

    public void RegisterHandler<TRequest, TResponse>(RPCServerHandlerDelegate<TRequest, TResponse> handler)
      where TRequest : class
      where TResponse : class
    {
      var responseType = RPCMessage<TResponse>.GetPayloadTypeName();
      var requestType = RPCMessage<TRequest>.GetPayloadTypeName();

      async Task<ResponsePayloadData> wrappedFunc(object request, IEnumerable<KeyValuePair<string, string>> headers)
      {
        var response = await handler(request as TRequest, headers);
        return new ResponsePayloadData()
        {
          responseType = responseType,
          responseObject = response.Payload,
          headers = response.Headers
        };
      }

      try
      {
        if (!handlers.TryAdd(requestType, (wrappedFunc, typeof(TRequest))))
          throw new ArgumentOutOfRangeException($"Handler for request type {requestType} already registered");
      }
      catch (InvalidOperationException)
      {
        handlers.TryRemove(requestType, out _);
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

    public void UnregisterHandler<TRequest>() where TRequest : class
    {
      handlers.TryRemove(RPCMessage<TRequest>.GetPayloadTypeName(), out _);
    }

    protected void ReceiveBytesPayload(byte[] bytes, string replyTo, string type, string correlationId, IEnumerable<KeyValuePair<string, string>> headers)
    {
      try
      {
        if (!handlers.TryGetValue(type, out (Func<object, IEnumerable<KeyValuePair<string, string>>, Task<ResponsePayloadData>> handler,
          Type requestType) handlerTuple))
        {
          ErrorLog?.Invoke($"Request msg processing failed. No handler for received msg of type: {type}");
          return;
        }

        var requestObj = serializer.Deserialize(bytes, handlerTuple.requestType);

        handlerTuple.handler.Invoke(requestObj, headers).ContinueWith(r =>
        {
          if (r.IsFaulted)
          {
            var ex = r.Exception.InnerExceptions.Count == 1 ? r.Exception.InnerException.Message : r.Exception.Message;
            ErrorLog?.Invoke($"Response msg processing failed. Response handler processing msg {correlationId} of type {type} threw exception: {ex}");
            return;
          }
          try
          {
            if (string.IsNullOrEmpty(replyTo))
            {
              ErrorLog?.Invoke($"Msg with correlation {correlationId} has no replyTo specified. Response will not be sent");
              return;
            }
            if (string.IsNullOrEmpty(correlationId))
            {
              ErrorLog?.Invoke($"Msg of type {type} and replyTo {replyTo} has no correlationId. Response will not be sent");
              return;
            }

            SendBytesPayloadResponse(serializer.Serialize(r.Result.responseObject),
              replyTo, r.Result.responseType, correlationId, r.Result.headers);
           
          }
          catch (Exception e)
          {
            ErrorLog?.Invoke($"SendBytesPayloadResponse exception processing message with correlation {correlationId}: {e.Message}");
          }
        });
      }
      catch (Exception e)
      {
        ErrorLog?.Invoke($"Response msg processing failed for msg with correlation {correlationId}. Exception: {e.Message}");
      }
    }

    protected abstract void SendBytesPayloadResponse(byte[] responseBytes, string replyTo, string type, string correlationId, IEnumerable<KeyValuePair<string, string>> headers);

    protected abstract void StartListening();

    protected abstract void StopListening();

    private class ResponsePayloadData
    {
      public IEnumerable<KeyValuePair<string, string>> headers;
      public object responseObject;
      public string responseType;
    }
  }
}