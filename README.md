# RPCBroker.NET
RPC framework for .NET

RPCBroker is a .NET library for creating RPC client and server endpoints using a message broker as transport. This project defines the interfaces and abstract client and server implementations while concrete versions for ActiveMQ and RabbitMQ have been released. The client provides async Task based request/response call semantics. Serialization is configurable and includes binary JSON and [Protocol buffers](https://github.com/protobuf-net/protobuf-net). 

The library defines two types of client to server RPC interactions. In the first, a message payload implements `IRPCBytesPayload` which utilizes .NET type information and a reflection driven serialization scheme. In the second, you provide explicit type and serialization information for both client and server. Implementing `IRPCBytesPayload` simplifies serialzation and message type identification for both client and server. Two concrete implementations of `IRPCBytesPayload` are provided that you can inherit from:
```
  RPCBinaryJsonPayload
  RPCProtoBufPayload
```

To see how this is all put together for client and server, let's create a remote method which negates an integer and returns the result. Here are our request and reply messages:
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
var client = new RabbitMQRPCClient("localhost", "testqueue", "guest", "guest");
var ct = new CancellationTokenSource();
var response = await client.RemoteCall<NegateJsonMsgRequest, NegateJsonMsgResponse>(
  RPCMessage<NegateJsonMsgRequest>.Create(new NegateJsonMsgRequest() { Value = 1 }), ct.Token, null, 2000);
Assert.IsTrue(response.Payload.Result == -1);
```
Both client and server use `RPCMessage<TPayload>` which wraps the `IRPCBytesPayload` derived payload object and optional headers that are transmitted by the RPC server or client.

The second RPC call option is to provide the type names and serialization logic to the client and server explicitly. In contrast to using `IRPCBytesPayload` (which transmits .NET type information and uses reflection to execute serialization), here you define explicit type names, and specify the serialization logic for request and reply objects. As an example consider our negate method, but this time our message definitions are simple POCO's that don't implement `IRPCBytesPayload`
```
 public class NegateBytesRequest 
 {
   public int Value { get; set; }
 }
 public class NegateBytesResponse 
 {
   public int Result { get; set; }
 }
```
For the server we define our handler as before, except this time we specify explicit message type names and the handler method is responsible for deserialization of the request and serialization of the response which must return `RPCMessage<byte[]>` (in contrast to `RPCMessage<IRPCBytesPayload>`) :
```
  var server = new RabbitMQRPCServer("localhost", "testqueue", "guest", "guest");
  server.RegisterExplicitHandler(
    "NegateRequest",
    "NegateResponse",
    (requestBytes, headers) =>
    {
      var request = JsonSerializer.Deserialize<NegateBytesRequest>(requestBytes);
      return Task.FromResult(RPCMessage<byte[]>.Create(JsonSerializer.SerializeToUtf8Bytes(new NegateBytesResponse() { Result = -request.Value })));
    });

  server.Start();
```
And similarly for the client we specify the request object, type names, and call the client's `RegisterExplicit*Handler` methods to define serialization for the request and deserialization of the response.
```
  var client = new RabbitMQRPCClient("localhost", "testqueue", "guest", "guest");
  var ct = new CancellationTokenSource();
  
  client.RegisterExplicitRequestHandler<NegateBytesRequest>(
  "NegateRequest", (requestObj) =>
  {
    return JsonSerializer.SerializeToUtf8Bytes(requestObj);
  });

  client.RegisterExplicitResponseHandler(
  "NegateResponse", (responseBytes) =>
  {
    return JsonSerializer.Deserialize<NegateBytesResponse>(responseBytes);
  });
  
  var request = new NegateBytesRequest() { Value = 1 };
  
  var response = await client.RemoteCallExplicit<NegateBytesRequest, NegateBytesResponse>(
    RPCMessage<NegateBytesRequest>.Create(request),
   "NegateRequest",
   "NegateResponse",
    ct.Token, null,
    5000);

  Assert.IsTrue(response.Payload.Result == -1);
```
You can pass null or empty strings for the type names to the `RegisterExplicit*Handler` and `RemoteCall` APIs. In this case the code will use the .NET request and response type names (i.e., [Type.Name](https://docs.microsoft.com/en-us/dotnet/api/system.type.name?view=netstandard-1.6&viewFallbackFrom=net-5.0)). For the server you can use Type.Name in this case. If you do this, it must be done for both `RegisterExplicit*Handler` and `RemoteCall` for a given pair of types. For example:
```
server.RegisterExplicitHandler(
    typeof(NegateBytesRequest).Name,
    typeof(NegateBytesResponse).Name,...
    
client.RegisterExplicitRequestHandler<NegateBytesRequest>(
    null,...
    
client.RegisterExplicitResponseHandler<NegateBytesResponse>(
    null,...
    
var request = new NegateBytesRequest() { Value = 1 };
var response = await client.RemoteCallExplicit<NegateBytesRequest, NegateBytesResponse>(
    RPCMessage<NegateBytesRequest>.Create(request),
    null,
    null,
    ct.Token, null,
    5000);
```
In closing the explicit type and serialization option might be useful if you just want to use this library's RPC client to interact with an RPC server enpoint implemented in some other language or environment where you have to adhere to particular type names and serialization. This assumes the server you're interacting with follows the [idiomatic implementation for an RPC message exchange](https://www.rabbitmq.com/tutorials/tutorial-six-python.html). Namely that it reads the standard correlation ID, Type, and replyTo headers from the broker message for the request, and uses those to construct and route the response.






 
