using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPCBroker
{
  public delegate void RPCServerErrorLogDelegate(string msg);

  public delegate Task<RPCMessage<TResponse>> RPCServerHandlerDelegate<TRequest, TResponse>(TRequest request, IEnumerable<KeyValuePair<string, string>> headers)
    where TRequest : class
    where TResponse : class;

  public interface IRPCServer
  {
    event RPCServerErrorLogDelegate ErrorLog;

    /// <summary>
    /// Queue or routing name for the server endpoint
    /// </summary>
    string DestinationName { get; }

    /// <summary>
    /// Register response handler for IRPCBytesPayload derived message
    /// </summary>
    /// <param name="handler">Handler returns TResponse instance from TRequest request</param>
    void RegisterHandler<TRequest, TResponse>(RPCServerHandlerDelegate<TRequest, TResponse> handler)
      where TRequest : class
      where TResponse : class;

    void Start();

    void Stop();

    void UnregisterHandler<TRequest>() where TRequest : class;

  }
}