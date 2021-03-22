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

    internal static string GetPayloadTypeName()
    {
      var tp = typeof(TPayload);
      var name = tp.Name;
      var attr = (RPCTypeNameAttribute)System.Attribute.GetCustomAttribute(tp, typeof(RPCTypeNameAttribute));
      if (attr != null)
      {
        RPCTypeNameUsage usage = attr.GetUsage();
        switch (usage)
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

    public string GetPayloadTypeFromPayload()
    {
      var tp = Payload.GetType();
      var name = tp.Name;
      var attr = (RPCTypeNameAttribute)System.Attribute.GetCustomAttribute(tp, typeof(RPCTypeNameAttribute));
      if (attr != null)
      {
        RPCTypeNameUsage usage = attr.GetUsage();
        switch (usage)
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

    private RPCMessage()
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
}