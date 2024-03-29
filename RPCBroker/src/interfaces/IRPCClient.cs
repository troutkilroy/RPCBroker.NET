﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPCBroker
{

  public delegate void UncorrelatedResponseDelegate(string correlationId, string type);
  public delegate void ClientLogEventDelegate(string eventTrace);
  public interface IRPCClient : IDisposable
  {
    event UncorrelatedResponseDelegate UncorrelatedResponseEvent;
    event ClientLogEventDelegate LogEvent;

    /// <summary>
    /// Server destination queue name or other routing name
    /// </summary>
    /// <returns></returns>
    string ServerDestination { get; }

    string ReplyTo { get; }
    bool LogPayloadAndHeaders { get; set; }

    /// <summary>
    /// Send request message to queue. Request and response payloads must implement IRPCBytesPayload.
    /// Throws TimeoutException if timeout MS elapses with no response
    /// </summary>
    /// <param name="request">RPCMessage wrapped request payload</param>
    /// <param name="cancel"></param>
    /// <param name="requestDestination">Queue name or other destination name for RPC server</param>
    /// <param name="timeoutWaitMilliseconds">Timeout</param>
    /// <returns>RPCMessage wrapped response payload</returns>
    Task<RPCMessage<TResponse>> RemoteCall<TRequest, TResponse>(RPCMessage<TRequest> msg,
      CancellationToken? cancel = null,
      string requestDestination = null, int timeoutWaitMilliseconds = 10000)
      where TResponse : class
      where TRequest : class;

    Task<RPCMessage<TResponse>> RemoteCall<TResponse>(RPCOpaqueMessage msg,
      CancellationToken? cancel = null,
      string requestDestination = null, int timeoutWaitMilliseconds = 10000)
      where TResponse : class;
    void Start();

    void Stop();
  }
}