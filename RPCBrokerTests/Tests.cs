using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;
using RPCBroker;
using RPCBroker.FakeBroker;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPCBrokerTests
{
  public class NegateJsonMsgRequest
  {
    public int Value { get; set; }
  }

  public class NegateJsonMsgResponse
  {
    public int Result { get; set; }
  }

  [RPCTypeName(RPCTypeNameUsage.Custom, name = "NEGATE_REQUEST")]
  public class NegateJsonMsgRequestCustom
  {
    public int Value { get; set; }
  }

  [RPCTypeName(RPCTypeNameUsage.Custom, name ="NEGATE_RESPONSE")]
  public class NegateJsonMsgResponseCustom
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
    [TestMethod]
    public async Task TEST_BASIC_PAYLOAD_JSON()
    {
      var server = new FakeRPCServer();
      server.RegisterHandler<NegateJsonMsgRequest, NegateJsonMsgResponse>(
          (request, headers) =>
          {
            var response = new NegateJsonMsgResponse()
            {
              Result = -request.Value
            };
            return Task.FromResult(RPCMessage<NegateJsonMsgResponse>.Create(response, null));
          });

      server.Start();

      try
      {
        var client = new FakeRPCClient(server.ServerChannel);
        client.Start();

        var ct = new CancellationTokenSource();

        var response = await client.RemoteCall<NegateJsonMsgResponse>(
            RPCOpaqueMessage.Create(new NegateJsonMsgRequest() { Value = 1 }), ct.Token);

        Assert.IsTrue(response.Payload.Result == -1);
      }
      catch (System.Exception e)
      {
        Assert.Fail(e.Message);
      }
    }

    [TestMethod]
    public async Task TEST_ANCESTOR_PAYLOAD_JSON()
    {
      var server = new FakeRPCServer();
      server.RegisterHandler<NegateJsonMsgRequest, NegateJsonMsgResponse>(
          (request, headers) =>
          {
            var response = new NegateJsonMsgResponse()
            {
              Result = -request.Value
            };
            return Task.FromResult(RPCMessage<NegateJsonMsgResponse>.Create(response, null));
          });

      server.Start();

      try
      {
        var client = new FakeRPCClient(server.ServerChannel);
        client.Start();

        var ct = new CancellationTokenSource();

        var req = (object)new NegateJsonMsgRequest() { Value = 1 };
        var response = await client.RemoteCall<NegateJsonMsgResponse>(
            RPCOpaqueMessage.Create(req), ct.Token);

        Assert.IsTrue(response.Payload.Result == -1);
      }
      catch (System.Exception e)
      {
        Assert.Fail(e.Message);
      }
    }

    [TestMethod]
    public async Task TEST_BASIC_PAYLOAD_PROTO()
    {
      var server = new FakeRPCServer(new RPCProtoBufSerializer());
      server.RegisterHandler<NegateProtoMsgRequest, NegateProtoMsgResponse>(
          (request, headers) =>
          {
            var response = new NegateProtoMsgResponse()
            {
              Result = -request.Value
            };
            return Task.FromResult(RPCMessage<NegateProtoMsgResponse>.Create(response, null));
          });

      server.Start();

      try
      {
        var client = new FakeRPCClient(server.ServerChannel, new RPCProtoBufSerializer());
        client.Start();

        var ct = new CancellationTokenSource();

        var response = await client.RemoteCall<NegateProtoMsgResponse>(
            RPCOpaqueMessage.Create(new NegateProtoMsgRequest() { Value = 1 }), ct.Token, null, 3000);

        Assert.IsTrue(response.Payload.Result == -1);
      }
      catch (System.Exception e)
      {
        Assert.Fail(e.Message);
      }
    }

    [TestMethod]
    public async Task TEST_BASIC_HEADERS_PAYLOAD()
    {
      var headers = new Dictionary<string, string> { { "key", "value" } };
      var server = new FakeRPCServer();
      server.RegisterHandler<NegateJsonMsgRequest, NegateJsonMsgResponse>(
          (request, headers) =>
          {
            var response = new NegateJsonMsgResponse()
            {
              Result = -request.Value
            };
            return Task.FromResult(RPCMessage<NegateJsonMsgResponse>.Create(response, headers));
          });

      server.Start();

      try
      {
        var client = new FakeRPCClient(server.ServerChannel);
        client.Start();

        var ct = new CancellationTokenSource();

        var response = await client.RemoteCall<NegateJsonMsgResponse>(
            RPCOpaqueMessage.Create(new NegateJsonMsgRequest() { Value = 1 }, headers), ct.Token, null, 3000);

        Assert.IsTrue(response.Headers["key"].Equals("value"));
        Assert.IsTrue(response.Payload.Result == -1);
      }
      catch (System.Exception e)
      {
        Assert.Fail(e.Message);
      }
    }

    [TestMethod]
    public async Task TEST_CUSTOM_TYPE_NAMES()
    {
      var server = new FakeRPCServer();
      server.RegisterHandler<NegateJsonMsgRequestCustom, NegateJsonMsgResponseCustom>(
          (request, headers) =>
          {
            var response = new NegateJsonMsgResponseCustom()
            {
              Result = -request.Value
            };
            return Task.FromResult(RPCMessage<NegateJsonMsgResponseCustom>.Create(response));
          });

      server.Start();

      try
      {
        var client = new FakeRPCClient(server.ServerChannel);
        client.Start();

        var ct = new CancellationTokenSource();

        var response = await client.RemoteCall<NegateJsonMsgResponseCustom>(
            RPCOpaqueMessage.Create(new NegateJsonMsgRequestCustom() { Value = 1 }), ct.Token, null, 3000);
      }
      catch (System.Exception e)
      {
        Assert.Fail(e.Message);
      }
    }

    [TestMethod]
    public async Task TEST_CONCURRENT_MULTIPLE_CLIENTS_LONGLIVED_SERVER_HANDLER()
    {
      var server = new FakeRPCServer();
      server.RegisterHandler<NegateJsonMsgRequest, NegateJsonMsgResponse>(
          async (request, headers) =>
          {
            var response = new NegateJsonMsgResponse()
            {
              Result = -request.Value
            };
            await Task.Delay(1000);
            return RPCMessage<NegateJsonMsgResponse>.Create(response, headers);
          });

      server.Start();

      List<FakeRPCClient> clients = new List<FakeRPCClient>();
      for (int i = 0; i < 50; i++)
      {
        var client = new FakeRPCClient(server.ServerChannel);
        client.Start();
        clients.Add(client);
      }

      var requests = new List<Task<RPCMessage<NegateJsonMsgResponse>>>();
      foreach (var c in clients)
      {
        var ct = new CancellationTokenSource();
        requests.Add(c.RemoteCall<NegateJsonMsgResponse>(
          RPCOpaqueMessage.Create(new NegateJsonMsgRequest() { Value = 1 }, null),ct.Token, null, 5000));
      }

      try
      {
        await Task.WhenAll(requests);
        foreach (var r in requests)
        {
          Assert.IsTrue(r.Result.Payload.Result == -1);
        }
      }
      catch
      {
        Assert.Fail("Not all RPC calls completed successsfully");
      }
    }

    [TestMethod]
    public async Task TEST_TIMEOUT()
    {
      var server = new FakeRPCServer();
      server.RegisterHandler<NegateJsonMsgRequest, NegateJsonMsgResponse>(
          async (request, headers) =>
          {
            var response = new NegateJsonMsgResponse()
            {
              Result = -request.Value
            };
            await Task.Delay(2000);
            return RPCMessage<NegateJsonMsgResponse>.Create(response, null);
          });

      server.Start();

      try
      {
        var client = new FakeRPCClient(server.ServerChannel);
        client.Start();

        var ct = new CancellationTokenSource();

        await Assert.ThrowsExceptionAsync<System.TimeoutException>(async () =>
        {
          await client.RemoteCall<NegateJsonMsgResponse>(
            RPCOpaqueMessage.Create(new NegateJsonMsgRequest() { Value = 1 }), ct.Token, null, 1000);
        });
      }
      catch (System.Exception e)
      {
        Assert.Fail(e.Message);
      }
    }

    [TestMethod]
    public async Task TEST_CANCELLATION()
    {
      var server = new FakeRPCServer();
      server.RegisterHandler<NegateJsonMsgRequest, NegateJsonMsgResponse>(
          async (request, headers) =>
          {
            var response = new NegateJsonMsgResponse()
            {
              Result = -request.Value
            };
            await Task.Delay(2000);
            return RPCMessage<NegateJsonMsgResponse>.Create(response, null);
          });

      server.Start();

      try
      {
        var client = new FakeRPCClient(server.ServerChannel);
        client.Start();

        var ct = new CancellationTokenSource();

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
        {
          ct.CancelAfter(500);
          await client.RemoteCall<NegateJsonMsgResponse>(
            RPCOpaqueMessage.Create(new NegateJsonMsgRequest() { Value = 1 }), ct.Token, null, 1000);
        });
      }
      catch (System.Exception e)
      {
        Assert.Fail(e.Message);
      }
    }

    [TestMethod]
    public async Task TEST_UNCORRELATED()
    {
      var server = new FakeRPCServer();
      server.RegisterHandler<NegateJsonMsgRequest, NegateJsonMsgResponse>(
          async (request, headers) =>
          {
            var response = new NegateJsonMsgResponse()
            {
              Result = -request.Value
            };
            await Task.Delay(20);
            return RPCMessage<NegateJsonMsgResponse>.Create(response, null);
          });

      server.Start();
      var uncorrelatedReceived = false;
      try
      {
        var client = new FakeRPCClient(server.ServerChannel);

        client.UncorrelatedResponseEvent += (cid, t) =>
        {
          uncorrelatedReceived = true;
        };

        client.Start();
        var ct = new CancellationTokenSource();
        await client.RemoteCall<NegateJsonMsgResponse>(
          RPCOpaqueMessage.Create(new NegateJsonMsgRequest() { Value = 1 }), ct.Token, null, 10);

      }
      catch (System.TimeoutException)
      {
        await Task.Delay(100);
        if (!uncorrelatedReceived)
          Assert.Fail("No uncorrelated event received");
      }
      catch (System.Exception e)
      {
        Assert.Fail(e.Message);
      }
      
    }
  }
}