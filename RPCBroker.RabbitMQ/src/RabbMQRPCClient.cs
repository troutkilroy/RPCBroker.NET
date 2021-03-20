using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RPCBroker.RabbitMQ
{
  public class RabbitMQRPCClient : RPCClient
  {
    private readonly int msgTTLMs;
    private readonly bool persistentSend;
    private readonly bool durableReplyTo;
    private readonly ConnectionFactory rmqConectionFactory;
    private bool disposedValue;
    private readonly string replyToExchange;
    private readonly string replyToExchangeRoutingKey;
    private IModel rmqChannel;
    private IConnection rmqConnection;

    /// <summary>
    /// The client will declare a temporary queue for the replyTo queue.  If replyToExchange is
    /// not specified, default routing is used.
    /// </summary>
    /// <param name="host"></param>
    /// <param name="name"></param>
    /// <param name="pswd"></param>
    /// <param name="serverRoutingKey">
    /// This defines the default destination route for client to server RPC request messages.
    /// If the RPC server uses default RabbitMQ routing (no exchange), then serverRoutingKey should be set
    /// to the server queue name. If the server uses an exchange then serverRoutingKey must be:
    ///
    /// "serverExchangeName/serverExchangeRoutingKey"
    ///
    /// serverRoutingKey can also be null (or empty) in which case the client must specify routing
    /// (requestDestination argument) when making an RPC request.</param>
    /// <param name="durableReplyTo">If true replyTo queue is created as durable</param>
    /// <param name="persistentSend">True if client to server messages are set as persistent.
    /// If enabled the RPC server must enable durable request queue</param>
    /// <param name="messageTTLMs">Message TTL in Ms. Default is 10s</param>
    /// <param name="replyToExchange">replyTo exchange to be used for server response. If specifying
    /// replyToExchange, the client code DOES NOT declare the exchange so it must exist in advance.
    /// However the client will bind the replyTo queue to the replyToExchange and replyToExchangeRoutingKey
    /// as long as both are specified.</param>
    /// <param name="replyToExchangeRoutingKey">replyTo exchange routing to be used for server response</param>
    /// <param name="virtualHost"></param>
    public RabbitMQRPCClient(
      string host,
      string name,
      string pswd,
      string serverRoutingKey,
      IRPCSerializer serializer = null,
      bool start = true,
      string replyToExchange = null,
      string replyToExchangeRoutingKey = null,
      bool durableReplyTo = false,
      bool persistentSend = false,
      int messageTTLMs = 10000,
      string virtualHost = "/")
    {
      this.serializer = serializer ?? new RPCJsonSerializer();
      this.persistentSend = persistentSend;
      this.durableReplyTo = durableReplyTo;
      this.replyToExchange = replyToExchange;
      this.replyToExchangeRoutingKey = replyToExchangeRoutingKey;
      msgTTLMs = messageTTLMs;
      defaultDestination = serverRoutingKey;

      rmqConectionFactory = new ConnectionFactory()
      {
        HostName = host,
        UserName = name,
        Password = pswd,
        VirtualHost = virtualHost,
        Port = AmqpTcpEndpoint.UseDefaultPort
      };

      if (start)
        Start();
    }

    private RabbitMQRPCClient()
    {
    }

    public override void Dispose()
    {
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          rmqChannel?.Dispose();
          rmqConnection?.Dispose();
        }
        disposedValue = true;
      }
    }

    protected override void SendBytesToQueue(byte[] bytes, string type, string requestDestination, string correlationId, IEnumerable<KeyValuePair<string, string>> headers)
    {
      string routing = string.IsNullOrEmpty(requestDestination) ? defaultDestination : requestDestination;
      string exchange = string.Empty;
      if (string.IsNullOrEmpty(routing))
      {
        return;
      }
      var parts = routing.Split(new char[] { '/' });
      if (parts.Length > 2) return;
      if (parts.Length == 2)
      {
        exchange = parts[0].Trim();
        routing = parts[1].Trim();
      }

      var replyToRoute = !string.IsNullOrEmpty(replyToExchange) && !string.IsNullOrEmpty(replyToExchangeRoutingKey) ?
        $"{replyToExchange}/{replyToExchangeRoutingKey}" :
          replyToDestination;

      var props = rmqChannel.CreateBasicProperties();
      props.CorrelationId = correlationId;
      props.Type = type;
      props.ReplyTo = replyToRoute;
      props.DeliveryMode = (byte)(persistentSend ? 2 : 1);
      props.Expiration = msgTTLMs.ToString();
      props.Headers = headers?.ToDictionary(pair => pair.Key, pair => (object)pair.Value);
      rmqChannel.BasicPublish(exchange, routing, props, bytes);
    }

    protected override void StartListening()
    {
      try
      {
        rmqConnection = rmqConectionFactory.CreateConnection();
        rmqConnection.ConnectionShutdown += (s, a) =>
        {
          Cleanup();
          OnConnectionError(a.ReplyText);
        };
        rmqChannel = rmqConnection.CreateModel();
        var consumer = new EventingBasicConsumer(rmqChannel);
        consumer.Received += (model, ea) =>
        {
          var body = ea.Body.ToArray();
          ReceivedBytesFromQueue(body, ea.BasicProperties.Type, ea.BasicProperties.CorrelationId, ea.BasicProperties.IsHeadersPresent() ?
            ea.BasicProperties.Headers.Where(h => h.Value is byte[]).
            Select(t => new KeyValuePair<string, string>(t.Key, System.Text.Encoding.UTF8.GetString((byte[])t.Value, 0, ((byte[])t.Value).Length))) :
            null);
        };

        replyToDestination = rmqChannel.QueueDeclare(string.Empty, durableReplyTo, false).QueueName;

        if (!string.IsNullOrEmpty(replyToExchange) && !string.IsNullOrEmpty(replyToExchangeRoutingKey))
        {
          rmqChannel.QueueBind(replyToDestination, replyToExchange, replyToExchangeRoutingKey);
        }

        rmqChannel.BasicConsume(replyToDestination, true, consumer);
        NotifyConnected();
      }
      catch (Exception e)
      {
        Cleanup();
        OnConnectionError(e.Message);
      }
    }

    protected override void StopListening()
    {
      Cleanup();
    }

    private void Cleanup()
    {
      try
      {
        rmqChannel?.Dispose();
        rmqConnection?.Dispose();
      }
      catch { }
      rmqChannel = null;
      rmqConnection = null;
    }
  }
}