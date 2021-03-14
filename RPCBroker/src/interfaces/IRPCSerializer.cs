using System;

namespace RPCBroker
{
  public interface IRPCSerializer
  {
    object Deserialize(byte[] bytes, Type type);

    byte[] Serialize(object obj);
  }
}