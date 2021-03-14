using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RPCBroker.RabbitMQ
{
  public class RabbitMQRPCServer : RPCServer
  {
    private readonly bool durableReceive;
    private readonly int msgTTLMs;
    private readonly ConnectionFactory rmqConectionFactory;
    private IModel rmqChannel;
    private IConnection rmqConnection;
    private EventingBasicConsumer rmqConsumer;
    private readonly string rmqExchange;
    private readonly string rmqExchangeRouting;

    /// <summary>
    /// Specify RPC server endpoint by queue name, exchange, and exchange routing key.
    /// NOTE: If using an exchange this code DOES NOT declare the exchange  Caller is responsilbe for
    /// exchange creation and lifetime. However it does declare the queue specified and binds it
    /// to requestExchange (if specified). The queue is declared auto delete and non-exclusive with durabiltiy
    /// set from durableRequestQueue (default is true).
    ///
    /// Note on Queue to Exchange Binding:
    /// 1. If requestExchange is null or empty, then default routing is assumed with routing by queue name
    ///    (requestExchangeRoutingKey is ignored)
    /// 2. If requestExchange is specified and requestExchangeRoutingKey is null, exhange will be ignored
    ///    and routing is same as (1).
    /// 3. If requestExchange and requestExchangeRoutingKey are both specified, queue is bound to exchange
    ///    with requestExchangeRoutingKey. In this case the RPCCient instance will need to specify server
    ///    destination (either in the constructor or when executing RPC requests) as:
    ///
    ///    "requestExchange/requestExchangeRoutingKey"
    ///
    ///    The server queue name would not be used by the client in this case as the message has to be sent
    ///    to the exchange.
    /// </summary>
    /// <param name="host"></param>
    /// <param name="name"></param>
    /// <param name="pswd"></param>
    /// <param name="requestQueueName"></param>
    /// <param name="requestExchange"></param>
    /// <param name="requestExchangeRoutingKey"></param>
    /// <param name="durableRequestQueue"></param>
    /// <param name="messageTTLMs"></param>
    /// <param name="virtualHost"></param>
    public RabbitMQRPCServer(string host, string name, string pswd,
      string requestQueueName, IRPCSerializer serializer = null, string requestExchange = null, string requestExchangeRoutingKey = null,
      bool durableRequestQueue = true, int messageTTLMs = 10000, string virtualHost = "/")
    {
      this.serializer = serializer ?? new RPCJsonSerializer();
      durableReceive = durableRequestQueue;
      rmqExchange = requestExchange;
      rmqExchangeRouting = requestExchangeRoutingKey;
      destinationName = requestQueueName;
      msgTTLMs = messageTTLMs;
      rmqConectionFactory = new ConnectionFactory()
      {
        HostName = host,
        UserName = name,
        Password = pswd,
        VirtualHost = virtualHost,
        Port = AmqpTcpEndpoint.UseDefaultPort
      };
    }

    private RabbitMQRPCServer()
    {
    }

    protected override void SendBytesPayloadResponse(byte[] responseBytes, string replyTo, string type, string correlationId, IEnumerable<KeyValuePair<string, string>> headers)
    {
      var props = rmqChannel.CreateBasicProperties();
      props.CorrelationId = correlationId;
      props.Type = type;
      props.Expiration = msgTTLMs.ToString();
      props.Headers = headers?.ToDictionary(pair => pair.Key, pair => (object)pair.Value);

      string routing = replyTo;
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

      rmqChannel.BasicPublish(exchange, routing, props, responseBytes);
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
        rmqConsumer = new EventingBasicConsumer(rmqChannel);
        rmqConsumer.Received += (model, ea) =>
        {
          ReceiveBytesPayload(ea.Body.ToArray(),
                  ea.BasicProperties.ReplyTo,
                  ea.BasicProperties.Type,
                  ea.BasicProperties.CorrelationId,
                  ea.BasicProperties.IsHeadersPresent() ?
                  ea.BasicProperties.Headers.
                  Where(h => h.Value is byte[]).
                  Select(t =>
                  {
                    var bytes = t.Value as byte[];
                    return new KeyValuePair<string, string>(t.Key, System.Text.Encoding.UTF8.GetString(bytes, 0, bytes.Length));
                  }) :
                  null);
        };

        rmqChannel.QueueDeclare(destinationName, durableReceive, false);
        if (!string.IsNullOrEmpty(rmqExchange) && !string.IsNullOrEmpty(rmqExchangeRouting))
        {
          rmqChannel.QueueBind(destinationName, rmqExchange, rmqExchangeRouting);
        }

        rmqChannel.BasicConsume(destinationName, true, rmqConsumer);
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