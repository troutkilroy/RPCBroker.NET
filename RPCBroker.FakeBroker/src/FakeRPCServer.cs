using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RPCBroker.FakeBroker
{
  public class FakeRPCServer : RPCServer
  {
    public readonly Channel<FakeMessage> ServerChannel = Channel.CreateUnbounded<FakeMessage>();

    private readonly ConcurrentDictionary<string, Channel<FakeMessage>> clientChannels =
      new ConcurrentDictionary<string, Channel<FakeMessage>>();

    public FakeRPCServer(IRPCSerializer serializer = null)
    {
      this.serializer = serializer ?? new RPCJsonSerializer();
      this.destinationName = "queue";
    }

    protected override void SendBytesPayloadResponse(byte[] responseBytes, string replyTo, string type, string correlationId, IEnumerable<KeyValuePair<string, string>> headers)
    {
      if (clientChannels.TryGetValue(replyTo, out Channel<FakeMessage> replyChannel))
      {
        replyChannel.Writer.TryWrite(new FakeMessage()
        {
          type = type,
          correlationId = correlationId,
          payload = responseBytes,
          headers = headers?.ToDictionary(x => x.Key, x => x.Value)
        });
      }
    }

    protected override void StartListening()
    {
      try
      {
        Task.Run(async () =>
        {
          while (await ServerChannel.Reader.WaitToReadAsync())
          {
            if (ServerChannel.Reader.TryRead(out FakeMessage msg))
            {
              try
              {
                clientChannels.TryAdd(msg.replyToQueue, msg.replyToChannel);
                ReceiveBytesPayload(msg.payload,
                  msg.replyToQueue,
                  msg.type,
                  msg.correlationId, msg.headers);
              }
              catch
              { }
            }
          }
        });
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
      ServerChannel.Writer.TryComplete();
    }
  }
}