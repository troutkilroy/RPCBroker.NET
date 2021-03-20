using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RPCBroker.ActiveMQ
{
  public class AMQRPCClient : RPCClient
  {
    private readonly ConnectionFactory amqConnectionFactory;

    private readonly TimeSpan msgTTL;

    private readonly bool persistentSend;

    private readonly bool asyncSend;

    private readonly IDestination requestAMQQueue;

    private readonly string userName;

    private readonly string userPassword;

    private IConnection amqConnection;

    private ISession amqSession;

    private bool disposedValue;

    private IMessageProducer msgSender;

    private ReplyToHandlerData replyToData;

    public AMQRPCClient(string amqUri, string amqUser, string amqPswd, string requestQueueName,
      IRPCSerializer serializer = null,
      bool start = true,
      bool asyncSend = true,
      bool persistenMessaging = false,
      int messageTTLMs = 10000)
    {
      this.serializer = serializer ?? new RPCJsonSerializer();
      this.asyncSend = asyncSend;
      msgTTL = TimeSpan.FromMilliseconds(messageTTLMs);
      persistentSend = persistenMessaging;
      defaultDestination = requestQueueName;
      requestAMQQueue = string.IsNullOrEmpty(defaultDestination) ? null : GetAMQDestination(defaultDestination);
      userName = amqUser;
      userPassword = amqPswd;
      amqConnectionFactory = new ConnectionFactory(amqUri)
      {
        AsyncSend = asyncSend
      };
      if (start)
        Start();
    }

    private IDestination GetAMQDestination(string destination)
    {
      if (destination.StartsWith("queue://"))
        return new ActiveMQQueue(destination.Substring("queue://".Length));
      if (destination.StartsWith("temp-queue://"))
        return new ActiveMQTempQueue(destination.Substring("temp-queue://".Length));
      if (destination.StartsWith("topic://"))
        return new ActiveMQTopic(destination.Substring("topic://".Length));
      if (destination.StartsWith("temp-topic://"))
        return new ActiveMQTempTopic(destination.Substring("temp-topic://".Length));
      return new ActiveMQQueue(destination);
    }

    private AMQRPCClient()
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
          replyToData?.Dispose();
          msgSender?.Dispose();
          amqSession?.Close();
          amqConnection?.Close();
        }
        disposedValue = true;
      }
    }

    protected override void SendBytesToQueue(byte[] bytes, string type, string requestDestination, string correlationId, IEnumerable<KeyValuePair<string, string>> headers)
    {
      IDestination rq = requestAMQQueue;
      if (!string.IsNullOrEmpty(requestDestination))
      {
        rq = GetAMQDestination(requestDestination);
      }

      if (rq == null)
      {
        return;
      }

      var amqBytesMsg = new ActiveMQBytesMessage
      {
        NMSCorrelationID = correlationId,
        NMSType = type,
        ReplyTo = replyToData.ReplyQueue,
        Persistent = persistentSend,
        NMSTimeToLive = msgTTL
      };

      if (headers != null)
      {
        foreach (var h in headers)
        {
          amqBytesMsg.Properties.SetString(h.Key, h.Value);
        }
      }

      amqBytesMsg.WriteBytes(bytes);
      lock (this)
      {
        msgSender.Send(rq, amqBytesMsg);
      }

    }

    protected override void StartListening()
    {
      try
      {
        amqConnection = amqConnectionFactory.CreateConnection(userName, userPassword);
        ((Connection)amqConnection).AsyncSend = asyncSend;
        amqConnection.AcknowledgementMode = AcknowledgementMode.AutoAcknowledge;
        // We're specifying this timeout as otherwise IConnection.Start()
        // hangs indefinetly on initial connect in certain cases like no
        // DNS response to the connection URI, network unavailable conditions or
        // SSL negotiation failure. It's really an odd design in the .NET
        // Apache client as IO exceptions in the call path of Start()
        // are black-holed internally while a monitor waits (with this timeout) to
        // send a handshake message on a socket that can't connect. At least
        // this way, we get an IOException albeit somewhat indirect to the
        // underlying cause.
        ((Connection)amqConnection).ITransport.Timeout = 5000;
        amqConnection.Start();
        amqSession = amqConnection.CreateSession();
        msgSender = amqSession.CreateProducer();
        msgSender.DeliveryMode = MsgDeliveryMode.NonPersistent;
        replyToData = new ReplyToHandlerData
        {
          ReplyQueue = (ActiveMQTempQueue)amqSession.CreateTemporaryQueue()
        };
        replyToDestination = replyToData.ReplyQueue.GetQueueName();
        replyToData.Consumer = amqSession.CreateConsumer(replyToData.ReplyQueue);
        replyToData.Consumer.Listener += (m) =>
        {
          if (m is ActiveMQBytesMessage bytesMsg)
          {
            ReceivedBytesFromQueue(bytesMsg.Content,
              bytesMsg.NMSType,
              bytesMsg.NMSCorrelationID,
              (from string k in bytesMsg.Properties.Keys
               let vs = bytesMsg.Properties[k.ToString()]
               where vs is string
               select new KeyValuePair<string, string>(k.ToString(), (string)vs)).ToList());
          }
        };
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
        replyToData?.Dispose();
        msgSender?.Dispose();
        amqSession?.Dispose();
        amqConnection?.Close();
      }
      catch { }
      replyToData = null;
      msgSender = null;
      amqSession = null;
      amqConnection = null;
    }

    private class ReplyToHandlerData : IDisposable
    {
      public IMessageConsumer Consumer;
      public ActiveMQTempQueue ReplyQueue;
      private bool disposedValue;

      public void Dispose()
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
            Consumer.Dispose();
          }
          disposedValue = true;
        }
      }
    }
  }
}