using System;
using System.Collections.Generic;
using System.Linq;

namespace RPCBroker
{
  /// <summary>
  /// RPCBroker message payload wrapper. This object contains the message payload request or response and any tranmitted headers.
  /// </summary>
  /// <typeparam name="TPayload"></typeparam>
  public class RPCMessage<TPayload> where TPayload : class
  {
    public readonly TPayload Payload;
    public readonly Dictionary<string, string> Headers;

    private static string PayloadTypeName(Type tp)
    {
      var name = tp.Name;
      var attr = (RPCTypeNameAttribute)System.Attribute.GetCustomAttribute(tp, typeof(RPCTypeNameAttribute));
      if (attr != null)
      {
        switch (attr.GetUsage())
        {
          case RPCTypeNameUsage.UseName:
            name = tp.Name;
            break;
          case RPCTypeNameUsage.UseFullName:
            name = tp.FullName;
            break;
          case RPCTypeNameUsage.Custom:
            if (!string.IsNullOrEmpty(attr.name))
              name = attr.name;
            break;
        }
      }
      return name;
    }

    internal static string GetPayloadTypeName()
    {
      return PayloadTypeName(typeof(TPayload));
    }

    public string GetPayloadTypeFromPayload()
    {
      return PayloadTypeName(Payload.GetType());
    }

    internal RPCMessage()
    {
    }

    public RPCMessage(TPayload payload, IEnumerable<KeyValuePair<string, string>> headers = null)
    {
      Payload = payload;
      Headers = headers?.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public static RPCMessage<TPayload> Create(TPayload payload, IEnumerable<KeyValuePair<string, string>> headers = null)
    {
      return new RPCMessage<TPayload>(payload, headers);
    }
  }

  public class RPCOpaqueMessage : RPCMessage<object>
  {
    public new static RPCOpaqueMessage Create(object payload, IEnumerable<KeyValuePair<string, string>> headers = null) 
    {
      return new RPCOpaqueMessage(payload, headers);
    }

    public RPCOpaqueMessage(object payload, IEnumerable<KeyValuePair<string, string>> headers = null) : base(payload,headers)
    {
    }
  }
}