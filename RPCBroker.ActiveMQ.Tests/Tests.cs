using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPCBroker.ActiveMQ.Tests
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
    public async Task TEST_BASIC_JSON_AMQ()
    {
      var q = GetRandomQueue();
      var server = new AMQRPCServer("tcp://localhost:61616", "admin", "admin", q);

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

      var client = new AMQRPCClient("tcp://localhost:61616", "admin", "admin", q);

      var ct = new CancellationTokenSource();

      var response = await client.RemoteCall<NegateJsonMsgRequest, NegateJsonMsgResponse>(
          RPCMessage<NegateJsonMsgRequest>.Create(new NegateJsonMsgRequest() { Value = 1 }), ct.Token);

      Assert.IsTrue(response.Payload.Result == -1);
    }

    [TestMethod]
    public async Task TEST_BASIC_WITH_HEADERS_JSON_AMQ()
    {
      var q = GetRandomQueue();
      var server = new AMQRPCServer("tcp://localhost:61616", "admin", "admin", q);

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

      try
      {
        server.Start();

        var client = new AMQRPCClient("tcp://localhost:61616", "admin", "admin", q);

        var ct = new CancellationTokenSource();

        var response = await client.RemoteCall<NegateJsonMsgRequest, NegateJsonMsgResponse>(
            RPCMessage<NegateJsonMsgRequest>.Create(new NegateJsonMsgRequest() { Value = 1 }), ct.Token);

        Assert.IsTrue(response.Payload.Result == -1);
        Assert.IsTrue(response.Headers["key"].Equals("value"));
      }
      catch (Exception e)
      {
        Assert.Fail(e.Message);
      }
    }


    [TestMethod]
    public async Task TEST_BASIC_PROTO_AMQ()
    {
      var q = GetRandomQueue();
      var protoSerializer = new RPCProtoBufSerializer();
      var server = new AMQRPCServer("tcp://localhost:61616", "admin", "admin",
          q, protoSerializer);

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
        var client = new AMQRPCClient("tcp://localhost:61616", "admin", "admin",
          q, protoSerializer);

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
    public async Task TESTRPC_MULTIPLE_CLIENTS_SAME_REQUESTQUEUE_AMQ()
    {
      var q = GetRandomQueue();
      var protoSerializer = new RPCProtoBufSerializer();
      var server = new AMQRPCServer("tcp://localhost:61616", "admin", "admin", q, protoSerializer);

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
      var ct = new CancellationTokenSource();

      List<AMQRPCClient> clients = new List<AMQRPCClient>();
      for (int i = 0; i < 10; i++)
      {
        var client = new AMQRPCClient("tcp://localhost:61616", "admin", "admin", q, protoSerializer);
        clients.Add(client);
      }

      try
      {
        await Task.WhenAll(
          clients.Select(c =>
          {
            return c.RemoteCall<NegateProtoMsgRequest, NegateProtoMsgResponse>(
              RPCMessage<NegateProtoMsgRequest>.Create(new NegateProtoMsgRequest() { Value = 1 }), ct.Token);
          }));
      }
      catch
      {
        Assert.Fail("Not all RPC calls completed successsfully");
      }
    }

    [TestMethod]
    public async Task TESTRPC_MULTIPLE_SERVER_QUEUES_ONE_CLIENT_AMQ()
    {
      var protoSerializer = new RPCProtoBufSerializer();
      List<AMQRPCServer> serverEndpoints = new List<AMQRPCServer>();
      for (int i = 0; i < 10; i++)
      {
        AMQRPCServer srv;
        serverEndpoints.Add(srv = new AMQRPCServer("tcp://localhost:61616", "admin", "admin",
          $"queue5-{i}", protoSerializer));
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

      var client = new AMQRPCClient("tcp://localhost:61616", "admin", "admin", null, protoSerializer);

      var ct = new CancellationTokenSource();

      try
      {
        await Task.WhenAll(
          serverEndpoints.Select(s =>
          {
            return client.RemoteCall<NegateProtoMsgRequest, NegateProtoMsgResponse>(
              RPCMessage<NegateProtoMsgRequest>.Create(new NegateProtoMsgRequest() { Value = 1 }), ct.Token, s.DestinationName);
          }));
      }
      catch
      {
        Assert.Fail("Not all RPC calls completed successsfully");
      }
    }
  }
}
