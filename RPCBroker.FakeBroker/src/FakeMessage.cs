using System.Collections.Generic;
using System.Threading.Channels;

namespace RPCBroker.FakeBroker
{
  public class FakeMessage
  {
    public Channel<FakeMessage> replyToChannel;
    public string replyToQueue;
    public byte[] payload;
    public string type;
    public string correlationId;
    public Dictionary<string, string> headers;
  }
}