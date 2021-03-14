using System;
using System.IO;

namespace RPCBroker
{
  public class RPCProtoBufSerializer : IRPCSerializer
  {
    public object Deserialize(byte[] bytes, Type type)
    {
      using (MemoryStream m = new MemoryStream(bytes))
        return ProtoBuf.Serializer.Deserialize(type, m);
    }

    public byte[] Serialize(object obj)
    {
      using (MemoryStream m = new MemoryStream())
      {
        ProtoBuf.Serializer.Serialize(m, obj);
        return m.ToArray();
      }
    }
  }
}