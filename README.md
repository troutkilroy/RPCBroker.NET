# RPCBroker.NET
RPC framework for .NET

RPCBroker is a .NET library for creating RPC client and server endpoints using a message broker as transport. This release supprts ActiveMQ and RabbitMQ, and can be expanded to support other brokers or pub/sub systems. The client provides async Task  based request/response call sematics with message type identification. Serialization is configurable and includes binary JSON and Protocol buffers. Others can be added.

The library requires a message payload class as either inheriting from `IRPCBytesPayload` or you can provide a byte array along with serialization methods. Inheriting from IRPCBytesPayload provides built in serialzation and message type identification for both client and server. Two concrete implementations of IRPCBytesPayload are provided that you can inherit from:
```
- RPCBinaryJsonPayload
- RPCProtoBufPayload
```

To see how this is all put together for client and server, let's create a remote method which negates an integer number and returns the result. Here are our request and reply messages:
```
 public class NegateJsonMsgRequest : RPCBinaryJsonPayload
 {
    public int Value { get; set; }
 }
 public class NegateJsonMsgResponse : RPCBinaryJsonPayload
 {
    public int Result { get; set; }
 }
```
 Now let's create a server endpoint for the request message that returns a response:
```
 var server = new RabbitMQRPCServer("localhost", "testqueue", "guest", "guest");
 server.RegisterHandler<NegateJsonMsgRequest, NegateJsonMsgResponse>(
          async (request) =>
          {
            var response = new NegateJsonMsgResponse()
            {
              Result = -request.Value
            };
            return response;
          });
server.Start();
```
And likewise for the client, we'll create a client instance and call the server endpoint:
```
var client = new RabbitMQRPCClient("localhost", "testqueue", "guest", "guest");
        client.Start();
var ct = new CancellationTokenSource();
var response = await client.RemoteCall<NegateJsonMsgRequest, NegateJsonMsgResponse>(
            new NegateJsonMsgRequest() { Value = 1 }, ct.Token, 2000);
```







 
