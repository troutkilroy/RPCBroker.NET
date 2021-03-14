using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPCBroker.RabbitMQ.Tests
{
  public class NegateJsonMsgRequest
  {
    public int Value { get; set; }
  }

  public class NegateJsonMsgResponse
  {
    public int Result { get; set; }
  }

  [ProtoContract]
  public class NegateProtoMsgRequest
  {
    [ProtoMember(1)]
    public int Value { get; set; }
  }

  [ProtoContract]
  public class NegateProtoMsgResponse
  {
    [ProtoMember(1)]
    public int Result { get; set; }
  }


  [TestClass]
  public class Tests
  {
    public string GetRandomQueue()
    {
      return Guid.NewGuid().ToString();
    }

    [TestMethod]
    public async Task TEST_BASIC_JSON_RMQ()
    {
      var q = GetRandomQueue();
      var server = new RabbitMQRPCServer("localhost", "guest", "guest", q);
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

      try
      {
        var client = new RabbitMQRPCClient("localhost", "guest", "guest", q);

        var ct = new CancellationTokenSource();

        var response = await client.RemoteCall<NegateJsonMsgRequest, NegateJsonMsgResponse>(
            RPCMessage<NegateJsonMsgRequest>.Create(new NegateJsonMsgRequest() { Value = 1 }), ct.Token);

        Assert.IsTrue(response.Payload.Result == -1);
      }
      catch (System.Exception e)
      {
      }
    }

    [TestMethod]
    public async Task TEST_BASIC_WITH_HEADERS_JSON_RMQ()
    {
      var q = GetRandomQueue();
      var server = new RabbitMQRPCServer("localhost", "guest", "guest", q);
      server.RegisterHandler<NegateJsonMsgRequest, NegateJsonMsgResponse>(
          (request, headers) =>
          {
            var response = new NegateJsonMsgResponse()
            {
              Result = -request.Value
            };
            var responseHeaders = new Dictionary<string, string> { { "key", "value" } };
            return Task.FromResult(RPCMessage<NegateJsonMsgResponse>.Create(response, responseHeaders));
          });

      server.Start();

      try
      {
        var client = new RabbitMQRPCClient("localhost", "guest", "guest", q);

        var ct = new CancellationTokenSource();

        var response = await client.RemoteCall<NegateJsonMsgRequest, NegateJsonMsgResponse>(
            RPCMessage<NegateJsonMsgRequest>.Create(new NegateJsonMsgRequest() { Value = 1 }), ct.Token);

        Assert.IsTrue(response.Payload.Result == -1);
        Assert.IsTrue(response.Headers["key"].Equals("value"));
      }
      catch (System.Exception e)
      {
      }
    }

    [TestMethod]
    public async Task TEST_BASIC_PROTO_RMQ()
    {
      var q = GetRandomQueue();
      var protoSerializer = new RPCProtoBufSerializer();
      var server = new RabbitMQRPCServer("localhost", "guest", "guest", q, protoSerializer);
      server.RegisterHandler<NegateProtoMsgRequest, NegateProtoMsgResponse>(
          (request, headers) =>
          {
            var response = new NegateProtoMsgResponse()
            {
              Result = -request.Value
            };
            return Task.FromResult(RPCMessage<NegateProtoMsgResponse>.Create(response));
          });

      server.Start();

      try
      {
        var client = new RabbitMQRPCClient("localhost", "guest", "guest", q, protoSerializer);

        var ct = new CancellationTokenSource();

        var response = await client.RemoteCall<NegateProtoMsgRequest, NegateProtoMsgResponse>(
             RPCMessage<NegateProtoMsgRequest>.Create(new NegateProtoMsgRequest() { Value = 1 }), ct.Token);

        Assert.IsTrue(response.Payload.Result == -1);
      }
      catch (System.Exception e)
      {
        Assert.Fail(e.Message);
      }
    }

    [TestMethod]
    public async Task TESTRPC_MULTIPLE_SERVER_QUEUES_ONE_CLIENT_RMQ()
    {
      var protoSerializer = new RPCProtoBufSerializer();
      try
      {
        var q = GetRandomQueue();
        List<RabbitMQRPCServer> serverEndpoints = new List<RabbitMQRPCServer>();
        for (int i = 0; i < 10; i++)
        {
          RabbitMQRPCServer srv;
          serverEndpoints.Add(srv = new RabbitMQRPCServer("localhost", "guest", "guest", $"{q}-{i}", protoSerializer));
          srv.RegisterHandler<NegateProtoMsgRequest, NegateProtoMsgResponse>(
            (request, headers) =>
            {
              var response = new NegateProtoMsgResponse()
              {
                Result = -request.Value
              };
              return Task.FromResult(RPCMessage<NegateProtoMsgResponse>.Create(response));
            });
          srv.Start();
        }

        var client = new RabbitMQRPCClient("localhost", "guest", "guest", null, protoSerializer);

        var ct = new CancellationTokenSource();
        await Task.WhenAll(
          serverEndpoints.Select(s =>
          {
            return client.RemoteCall<NegateProtoMsgRequest, NegateProtoMsgResponse>(
              RPCMessage<NegateProtoMsgRequest>.Create(new NegateProtoMsgRequest() { Value = 1 }), ct.Token, s.DestinationName);
          }));
      }
      catch (Exception e)
      {
        Assert.Fail("Not all RPC calls completed successsfully");
      }
    }

    [TestMethod]
    public async Task TESTRPC_MULTIPLE_CLIENTS_SAME_REQUESTQUEUE_RMQ()
    {
      var q = GetRandomQueue();
      var protoSerializer = new RPCProtoBufSerializer();
      var server = new RabbitMQRPCServer("localhost", "guest", "guest", q, protoSerializer);
      server.RegisterHandler<NegateProtoMsgRequest, NegateProtoMsgResponse>(
          (request, headers) =>
          {
            var response = new NegateProtoMsgResponse()
            {
              Result = -request.Value
            };
            return Task.FromResult(RPCMessage<NegateProtoMsgResponse>.Create(response));
          });

      server.Start();

      List<RabbitMQRPCClient> clients = new List<RabbitMQRPCClient>();
      for (int i = 0; i < 10; i++)
      {
        var client = new RabbitMQRPCClient("localhost", "guest", "guest", q, protoSerializer);
        clients.Add(client);
      }

      List<Task> requests = new List<Task>();
      foreach (var c in clients)
      {
        var ct = new CancellationTokenSource();
        requests.Add(c.RemoteCall<NegateProtoMsgRequest, NegateProtoMsgResponse>(
          RPCMessage<NegateProtoMsgRequest>.Create(new NegateProtoMsgRequest() { Value = 1 }), ct.Token));
      }

      try
      {
        await Task.WhenAll(requests);
      }
      catch
      {
        Assert.Fail("Not all RPC calls completed successsfully");
      }
    }
  }
}
