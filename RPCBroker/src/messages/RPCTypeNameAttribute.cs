using System;
using System.Collections.Generic;
using System.Text;

namespace RPCBroker
{
  public enum RPCTypeNameUsage
  {
    UseName,
    UseFullName,
    Custom
  }
  [AttributeUsage(AttributeTargets.Class)]
  public class RPCTypeNameAttribute : System.Attribute
  {
    private readonly RPCTypeNameUsage usage;
    public string name;
    public RPCTypeNameUsage GetUsage() => usage;
    
    public RPCTypeNameAttribute(RPCTypeNameUsage usage)
    {
      this.usage = usage;
    }
  }
}
