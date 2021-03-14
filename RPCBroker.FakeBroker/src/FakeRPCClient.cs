using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RPCBroker.FakeBroker
{
  public class FakeRPCClient : RPCClient
  {
    private readonly Channel<FakeMessage> clientChannel = Channel.CreateUnbounded<FakeMessage>();
    private bool disposedValue;
    private readonly string replyToQueue = Guid.NewGuid().ToString();
    private readonly Channel<FakeMessage> serverChannel;

    public FakeRPCClient(Channel<FakeMessage> serverChannel, IRPCSerializer serializer = null)
    {
      this.serverChannel = serverChannel;
      this.serializer = serializer ?? new RPCJsonSerializer();
      defaultDestination = "queue";
    }

    private FakeRPCClient()
    {
    }

    public override void Dispose()
    {
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
        }
        disposedValue = true;
      }
    }

    protected override void SendBytesToQueue(byte[] bytes, string type, string requestDestination, string correlationId, IEnumerable<KeyValuePair<string, string>> headers)
    {
      serverChannel.Writer.TryWrite(new FakeMessage()
      {
        type = type,
        payload = bytes,
        replyToQueue = replyToQueue,
        correlationId = correlationId,
        replyToChannel = clientChannel,
        headers = headers?.ToDictionary(x => x.Key, x => x.Value)
      });
    }

    protected override void StartListening()
    {
      try
      {
        Task.Run(async () =>
        {
          while (await clientChannel.Reader.WaitToReadAsync())
          {
            if (clientChannel.Reader.TryRead(out FakeMessage msg))
            {
              ReceivedBytesFromQueue(msg.payload, msg.type, msg.correlationId, msg.headers);
            }
          }
        });

        NotifyConnected();
      }
      catch (Exception e)
      {
        Cleanup();
        OnConnectionError(e.Message);
      }
    }

    protected override void StopListening()
    {
      Cleanup();
    }

    private void Cleanup()
    {
      clientChannel.Writer.TryComplete();
    }
  }
}