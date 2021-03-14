using System;
using System.Text.Json;

namespace RPCBroker
{
  public class RPCJsonSerializer : IRPCSerializer
  {
    public object Deserialize(byte[] bytes, Type type) => JsonSerializer.Deserialize(bytes, type);

    public byte[] Serialize(object obj) => JsonSerializer.SerializeToUtf8Bytes(obj, obj.GetType());
  }
}