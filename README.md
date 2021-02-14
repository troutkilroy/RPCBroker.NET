# RPCBroker.NET
RPC framework for .NET

RPCBroker is a .NET library for creating RPC client and server endpoints using a message broker as transport. This release supprts ActiveMQ and RabbitMQ, and can be expanded to support other brokers or pub/sub systems. The client provides async Task based request/response call semantics. Serialization is configurable and includes binary JSON and Protocol buffers. 

The library defines a message payload as a class inherited from `IRPCBytesPayload` or you provide a byte array along with serialization methods. Inheriting from IRPCBytesPayload simplifies serialzation and message type identification for both client and server. Two concrete implementations of `IRPCBytesPayload` are provided that you can inherit from:
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
Finally for the client, we'll create a client instance and call the server endpoint:
```
var client = new RabbitMQRPCClient("localhost", "testqueue", "guest", "guest");
var ct = new CancellationTokenSource();
var response = await client.RemoteCall<NegateJsonMsgRequest, NegateJsonMsgResponse>(
            new NegateJsonMsgRequest() { Value = 1 }, ct.Token, 2000);
```

The second RPC call option is to provide the type names and serialization logic to the client and server explicitly. In contrast to using an implementation of `IRPCBytesPayload` (which transmits .NET type information and uses reflection to execute serialization), you define your request and reply objects as you desire, type names, and specify the serialization logic explictly. As an example consider our negate method, but this time our message definitions are simple POCO's that don't implement `IRPCBytesPayload`
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
For the server we define our handler as before, except this time we specify arbitrary message type names and the hander method is responsible for deserialization of the request and serialization of the response:
```
  var server = new RabbitMQRPCServer("localhost", "testqueue", "guest", "guest");
  server.RegisterHandler(
    "NegateRequest",
    "NegateResponse",
    async(requestBytes) =>
    {
      var request = JsonSerializer.Deserialize<NegateBytesRequest>(requestBytes);
      return JsonSerializer.SerializeToUtf8Bytes(new NegateBytesResponse() { Result = -request.Value });
    });

  server.Start();
```
And similarly for the client we specify the request object, type names, and call RegisterHandlers method to specify methods to serialize the request and deserialize the response.
```
  var client = new RabbitMQRPCClient("localhost", "testqueue", "guest", "guest");
  var ct = new CancellationTokenSource();
  
  client.RegisterHandlers<NegateBytesRequest,NegateBytesResponse>(
    "NegateRequest", "NegateResponse",
    (requestObj) =>
    {
      return JsonSerializer.SerializeToUtf8Bytes(requestObj);
    },
    (responseBytes) =>
    {
      return JsonSerializer.Deserialize<NegateBytesResponse>(responseBytes);
    }
  );
  
  var response = await client.RemoteCall<NegateBytesRequest, NegateBytesResponse>(
    request,
    "NegateRequest", "NegateResponse",
    ct.Token,
    5000);
```
You can pass null or empty strings for the type names to the RegisterHandlers and RemoteCall API's. In this case the code will use the .NET request and response type names (i.e., Type.Name). If you do this, it must be done for both RegisterHandlers and RemoteCall for a given pair of types. For example:
```
client.RegisterHandlers<NegateBytesRequest,NegateBytesResponse>(
    null,null,...

var response = await client.RemoteCall<NegateBytesRequest, NegateBytesResponse>(
    request,
    nul,null,
    ct.Token,
    5000);
```
This option might be useful if you want to just use the client implementations of this library to interact with an RPC server enpoint implemented in some other language or environment where you have to adhere to particular type names and serialization. This assumes the server you're interacting with follows the idiomatic implementation for an RPC message exchange. Namely that it reads the standard correlation ID, Type, and replyTo headers from the broker message for the request, and uses those to construct the response.






 
