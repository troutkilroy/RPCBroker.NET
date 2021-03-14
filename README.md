# RPCBroker.NET
RPC framework for .NET

RPCBroker is a .NET library for creating RPC client and server endpoints using a message broker as transport. This project defines the interfaces and abstract client and server implementations while concrete versions for ActiveMQ and RabbitMQ are included. The client provides async Task based request/response call semantics. Serialization is configurable and includes binary JSON (the default) and [Protocol buffers](https://github.com/protobuf-net/protobuf-net). 

To see how this is all put together for client and server, let's create a remote method which negates an integer and returns the result. Here are our request and reply messages:
```
 public class NegateJsonMsgRequest 
 {
   public int Value { get; set; }
 }
 public class NegateJsonMsgResponse 
 {
   public int Result { get; set; }
 }
```
 Now let's create a server endpoint for the request message that returns a response:
```
  var server = new RabbitMQRPCServer("localhost", "guest", "guest", "serverQueue");
  server.RegisterHandler<NegateJsonMsgRequest, NegateJsonMsgResponse>(
      (request, headers) =>
      {
        var response = new NegateJsonMsgResponse()
        {
          Result = -request.Value
        };
        return Task.FromResult(RPCMessage<NegateJsonMsgResponse>.Create(response));
      });

  server.Start();
```
You can have multiple handlers for different request types. Finally for the client, we'll create a client instance and call the server endpoint:
```
var client = new RabbitMQRPCClient("localhost", "guest", "guest", q);
var ct = new CancellationTokenSource();
var response = await client.RemoteCall<NegateJsonMsgRequest, NegateJsonMsgResponse>(
  RPCMessage<NegateJsonMsgRequest>.Create(new NegateJsonMsgRequest() { Value = 1 }), ct.Token);
Assert.IsTrue(response.Payload.Result == -1);
```
Both client and server use `RPCMessage<TPayload>` which wraps the request/reply and optional headers that are transmitted by the RPC server or client.

By default the client transmits, and the server expects, the request object's .NET type name (via the broker's message type header). This can be controlled with a custom attribute on the request/reply class. The options are .NET type name, full type name, or a custom name that you supply. 
```
 [RPCTypeName(RPCTypeNameUsage.Custom, name = "NEGATE_REQUEST")]
 public class NegateBytesRequest 
 {
   public int Value { get; set; }
 }
 [RPCTypeName(RPCTypeNameUsage.Custom, name ="NEGATE_RESPONSE")]
 public class NegateBytesResponse 
 {
   public int Result { get; set; }
 }
```
A custom name may be useful if you intend to use on only the client implementation of this library to interact with a broker RPC endpoint written in another langauge or envirnonment where you have to adhere to particular type names. This assumes the server you're interacting with follows the [idiomatic implementation for an RPC message exchange](https://www.rabbitmq.com/tutorials/tutorial-six-python.html). Namely that it reads the standard correlation ID, Type, and replyTo headers from the broker message for the request, and uses those to construct and route the response.





 
