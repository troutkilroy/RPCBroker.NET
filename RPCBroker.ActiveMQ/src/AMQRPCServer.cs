﻿using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RPCBroker.ActiveMQ
{
  public class AMQRPCServer : RPCServer
  {
    private readonly ConnectionFactory amqConnectionFactory;
    private readonly TimeSpan msgTTL;
    private readonly string userName;
    private readonly string userPassword;
    private readonly bool asyncSend;
    private IConnection amqConnection;
    private ISession amqSession;
    private IMessageConsumer msgReceiver;
    private IMessageProducer msgSender;

    public AMQRPCServer(string uri, string name, string pswd, string requestQueue, IRPCSerializer serializer = null, bool asyncSend = true, int messageTTLMs = 10000)
    {
      this.serializer = serializer ?? new RPCJsonSerializer();
      this.asyncSend = asyncSend;
      msgTTL = TimeSpan.FromMilliseconds(messageTTLMs);
      destinationName = requestQueue;
      userName = name;
      userPassword = pswd;
      amqConnectionFactory = new ConnectionFactory($"failover:({uri})")
      {
        AsyncSend = true
      };
    }

    private AMQRPCServer()
    {
    }

    protected override void SendBytesPayloadResponse(byte[] responseBytes, string replyTo, string type, string correlationId, IEnumerable<KeyValuePair<string, string>> headers)
    {
      IDestination replyToDestination = null;
      var amqBytesMsg = new ActiveMQBytesMessage
      {
        CorrelationId = correlationId,
        NMSType = type,
        Persistent = false,
        NMSTimeToLive = msgTTL
      };

      if (replyTo.StartsWith("queue://"))
      {
        replyToDestination = new ActiveMQQueue(replyTo.Substring("queue://".Length));
      }
      else if (replyTo.StartsWith("temp-queue://"))
      {
        replyToDestination = new ActiveMQTempQueue(replyTo.Substring("temp-queue://".Length));
      }

      if (headers != null)
      {
        foreach (var h in headers)
        {
          amqBytesMsg.Properties.SetString(h.Key, h.Value);
        }
      }

      amqBytesMsg.WriteBytes(responseBytes);

      // IMessageProducer is not thread safe
      lock (this)
      {
        msgSender.Send(replyToDestination, amqBytesMsg);
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

        msgReceiver = amqSession.CreateConsumer(new ActiveMQQueue(destinationName));
        msgReceiver.Listener += (m) =>
        {
          if (m is ActiveMQBytesMessage bytesMsg)
          {
            ReceiveBytesPayload(bytesMsg.Content,
              bytesMsg.ReplyTo.ToString(),
              bytesMsg.NMSType,
              bytesMsg.CorrelationId,
              (from string k in bytesMsg.Properties.Keys
               let vs = bytesMsg.Properties[k.ToString()]
               where vs is string
               select new KeyValuePair<string, string>(k.ToString(), (string)vs)).ToList());
          }
        };
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
        msgReceiver?.Dispose();
        msgSender?.Dispose();
        amqSession?.Dispose();
        amqConnection?.Close();
      }
      catch { }
      msgReceiver = null;
      msgSender = null;
      amqSession = null;
      amqConnection = null;
    }
  }
}