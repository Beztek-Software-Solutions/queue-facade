// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Apache.NMS;
    using Apache.NMS.ActiveMQ;

internal sealed class ActiveMqProvider : IQueueProvider
{
	private const int Zero = 0;

	private const int One = 1;

	private readonly ILogger logger;

	private readonly string brokerUri;

	private readonly string userName;

	private readonly string password;

	private readonly string high;

	private readonly string low;

	private readonly string poison;

	private readonly object sync = new object();

	private int created;

	private Apache.NMS.IConnection connection;

	private ISession session;

	public bool HasLowPriorityQueue { get; }

	public int MaxMessageSize { get; }

	public int VisibilityTimeoutMilliseconds { get; }

	public int MaxMessageCountPerPoll { get; }

	internal ActiveMqProvider(ActiveMqProviderConfig config, ILogger logger = null)
	{
		this.logger = logger;
		VisibilityTimeoutMilliseconds = config.VisibilityTimeoutMilliseconds;
		brokerUri = config.BrokerUri;
		userName = config.UserName;
		password = config.Password;
		high = config.HighPriorityQueue;
		low = config.LowPriorityQueue;
		poison = config.UnprocessedQueue;
		HasLowPriorityQueue = !string.IsNullOrEmpty(low);
		MaxMessageSize = 262144;
		MaxMessageCountPerPoll = 32;
	}

	/// <summary>Test seam: inject an open session (and optional connection).</summary>
	internal ActiveMqProvider(ActiveMqProviderConfig config, ISession session, Apache.NMS.IConnection connection = null, ILogger logger = null)
		: this(config, logger)
	{
		this.session = session ?? throw new ArgumentNullException(nameof(session));
		this.connection = connection;
		created = One;
	}

	public void CreateIfNotExists()
	{
		if (created != 0)
		{
			return;
		}
		lock (sync)
		{
			if (Interlocked.Exchange(ref created, 1) != 0)
			{
				return;
			}
			try
			{
				Apache.NMS.ActiveMQ.ConnectionFactory connectionFactory = new Apache.NMS.ActiveMQ.ConnectionFactory(brokerUri);
				connection = connectionFactory.CreateConnection(userName, password);
				connection.Start();
				session = connection.CreateSession(AcknowledgementMode.IndividualAcknowledge);
				session.GetQueue(high);
				if (HasLowPriorityQueue)
				{
					session.GetQueue(low);
				}
				session.GetQueue(poison);
			}
			catch
			{
				Interlocked.Exchange(ref created, 0);
				throw;
			}
		}
	}

	public Task<bool> SendMessageAsync(string message, bool useHighPriorityQueue)
	{
		CreateIfNotExists();
		string queueName = ((useHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		Send(queueName, message);
		return Task.FromResult(result: true);
	}

	public Task<bool> SendUnprocessedMessageAsync(string message)
	{
		CreateIfNotExists();
		Send(poison, message);
		return Task.FromResult(result: true);
	}

	public IList<object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string queueName = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		return ReceiveMany(queueName, maxMessagesToRetrieve);
	}

	public int GetReceiveCount(object messageHook)
	{
		if (messageHook is ITextMessage textMessage)
		{
			try
			{
				int val = textMessage.Properties.GetInt("JMSXDeliveryCount");
				return Math.Max(1, val);
			}
			catch
			{
				return 1;
			}
		}
		return 1;
	}

	public IList<object> PeekUnprocessedMessages(int maxMessagesToRetrieve)
	{
		return ReceiveUnprocessedMessages(maxMessagesToRetrieve);
	}

	public IList<object> ReceiveUnprocessedMessages(int maxMessagesToRetrieve)
	{
		CreateIfNotExists();
		return ReceiveMany(poison, maxMessagesToRetrieve);
	}

	public Task DeleteUnprocessedMessageAsync(object messageHook)
	{
		if (messageHook is Apache.NMS.IMessage message)
		{
			message.Acknowledge();
		}
		return Task.CompletedTask;
	}

	public Task<long> GetApproximateUnprocessedQueueLength()
	{
		CreateIfNotExists();
		return Task.FromResult(ProbeDepth(poison));
	}

	public string GetMessageBody(object messageHook)
	{
		return (messageHook is ITextMessage textMessage) ? textMessage.Text : messageHook?.ToString();
	}

	public Task DeleteMessageAsync(object messageHook, bool isHighPriorityQueue)
	{
		if (messageHook is Apache.NMS.IMessage message)
		{
			message.Acknowledge();
		}
		return Task.CompletedTask;
	}

	public Task<long> GetApproximateQueueLength(bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string queueName = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		return Task.FromResult(ProbeDepth(queueName));
	}

	private void Send(string queueName, string body)
	{
		IQueue queue = session.GetQueue(queueName);
		using IMessageProducer messageProducer = session.CreateProducer(queue);
		messageProducer.DeliveryMode = MsgDeliveryMode.Persistent;
		ITextMessage message = session.CreateTextMessage(body);
		messageProducer.Send(message);
	}

	private IList<object> ReceiveMany(string queueName, int max)
	{
		int num = Math.Min(Math.Max(max, 1), MaxMessageCountPerPoll);
		IQueue queue = session.GetQueue(queueName);
		using IMessageConsumer messageConsumer = session.CreateConsumer(queue);
		List<object> list = new List<object>();
		for (int i = 0; i < num; i++)
		{
			Apache.NMS.IMessage message = messageConsumer.Receive(TimeSpan.FromMilliseconds(50));
			if (message == null)
			{
				break;
			}
			list.Add(message);
		}
		return list;
	}

	private long ProbeDepth(string queueName)
	{
		try
		{
			IQueue queue = session.GetQueue(queueName);
			using IQueueBrowser queueBrowser = session.CreateBrowser(queue);
			System.Collections.IEnumerator enumerator = queueBrowser.GetEnumerator();
			long num = 0L;
			while (enumerator.MoveNext())
			{
				num++;
				if (num > 1000)
				{
					break;
				}
			}
			return num;
		}
		catch (Exception exception)
		{
			logger?.LogDebug(exception, "ActiveMQ browse depth failed for {Queue}", queueName);
			return 0L;
		}
	}
}
}
