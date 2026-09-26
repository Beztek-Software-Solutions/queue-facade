// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using RabbitMQ.Client;

internal sealed class RabbitMqProvider : IQueueProvider
{
	internal sealed class RabbitMqHook
	{
		internal ulong DeliveryTag { get; }

		internal string Body { get; }

		internal int Attempt { get; }

		internal string Queue { get; }

		internal bool Completed { get; set; }

		internal RabbitMqHook(ulong deliveryTag, string body, int attempt, string queue)
		{
			DeliveryTag = deliveryTag;
			Body = body;
			Attempt = attempt;
			Queue = queue;
		}
	}

	internal const string DeliveryCountHeader = "x-attempt";

	private const int Zero = 0;

	private const int One = 1;

	private readonly ILogger logger;

	private readonly RabbitMQ.Client.ConnectionFactory factory;

	private readonly string high;

	private readonly string low;

	private readonly string poison;

	private readonly object sync = new object();

	private int created;

	private RabbitMQ.Client.IConnection connection;

	private IChannel channel;

	public bool HasLowPriorityQueue { get; }

	public int MaxMessageSize { get; }

	public int VisibilityTimeoutMilliseconds { get; }

	public int MaxMessageCountPerPoll { get; }

	internal RabbitMqProvider(RabbitMqProviderConfig config, ILogger logger = null)
	{
		this.logger = logger;
		VisibilityTimeoutMilliseconds = config.VisibilityTimeoutMilliseconds;
		high = config.HighPriorityQueue;
		low = config.LowPriorityQueue;
		poison = config.UnprocessedQueue;
		HasLowPriorityQueue = !string.IsNullOrEmpty(low);
		MaxMessageSize = 262144;
		MaxMessageCountPerPoll = 32;
		factory = new RabbitMQ.Client.ConnectionFactory
		{
			Uri = new Uri(config.AmqpUri)
		};
	}

	/// <summary>Test seam: inject a channel (skips connect/declare).</summary>
	internal RabbitMqProvider(RabbitMqProviderConfig config, IChannel channel, ILogger logger = null)
		: this(config, logger)
	{
		this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
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
				connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
				channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
				Declare(high);
				if (HasLowPriorityQueue)
				{
					Declare(low);
				}
				Declare(poison);
			}
			catch
			{
				Interlocked.Exchange(ref created, 0);
				throw;
			}
		}
	}

	public async Task<bool> SendMessageAsync(string message, bool useHighPriorityQueue)
	{
		CreateIfNotExists();
		string queue = ((useHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		await PublishAsync(queue, message, 1).ConfigureAwait(continueOnCapturedContext: false);
		return true;
	}

	public async Task<bool> SendUnprocessedMessageAsync(string message)
	{
		CreateIfNotExists();
		await PublishAsync(poison, message, 1).ConfigureAwait(continueOnCapturedContext: false);
		return true;
	}

	public IList<object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string queue = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		return BasicGetMany(queue, maxMessagesToRetrieve);
	}

	public int GetReceiveCount(object messageHook)
	{
		return (!(messageHook is RabbitMqHook rabbitMqHook)) ? 1 : Math.Max(1, rabbitMqHook.Attempt);
	}

	public IList<object> PeekUnprocessedMessages(int maxMessagesToRetrieve)
	{
		return ReceiveUnprocessedMessages(maxMessagesToRetrieve);
	}

	public IList<object> ReceiveUnprocessedMessages(int maxMessagesToRetrieve)
	{
		CreateIfNotExists();
		return BasicGetMany(poison, maxMessagesToRetrieve);
	}

	public async Task DeleteUnprocessedMessageAsync(object messageHook)
	{
		if (messageHook is RabbitMqHook hook)
		{
			hook.Completed = true;
			await channel.BasicAckAsync(hook.DeliveryTag, multiple: false).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task<long> GetApproximateUnprocessedQueueLength()
	{
		CreateIfNotExists();
		return (await channel.QueueDeclarePassiveAsync(poison).ConfigureAwait(continueOnCapturedContext: false)).MessageCount;
	}

	public string GetMessageBody(object messageHook)
	{
		return (messageHook is RabbitMqHook rabbitMqHook) ? rabbitMqHook.Body : messageHook?.ToString();
	}

	public async Task DeleteMessageAsync(object messageHook, bool isHighPriorityQueue)
	{
		if (messageHook is RabbitMqHook hook)
		{
			hook.Completed = true;
			await channel.BasicAckAsync(hook.DeliveryTag, multiple: false).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task<long> GetApproximateQueueLength(bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string queue = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		return (await channel.QueueDeclarePassiveAsync(queue).ConfigureAwait(continueOnCapturedContext: false)).MessageCount;
	}

	private async Task PublishAsync(string queue, string message, int attempt)
	{
		BasicProperties props = new BasicProperties
		{
			Persistent = true,
			Headers = new Dictionary<string, object> { ["x-attempt"] = attempt }
		};
		byte[] body = System.Text.Encoding.UTF8.GetBytes(message);
		await channel.BasicPublishAsync(string.Empty, queue, mandatory: false, props, body).ConfigureAwait(continueOnCapturedContext: false);
	}

	private IList<object> BasicGetMany(string queue, int max)
	{
		int num = RabbitMqDelivery.ClampTake(max, MaxMessageCountPerPoll);
		List<object> list = new List<object>();
		for (int i = 0; i < num; i++)
		{
			BasicGetResult result = channel.BasicGetAsync(queue, autoAck: false).GetAwaiter().GetResult();
			if (result == null)
			{
				break;
			}
			list.Add(ToHook(result, queue));
		}
		return list;
	}

	private RabbitMqHook ToHook(BasicGetResult got, string queue)
	{
		int attempt = RabbitMqDelivery.ParseAttempt(got.BasicProperties?.Headers);
		RabbitMqHook rabbitMqHook = new RabbitMqHook(got.DeliveryTag, System.Text.Encoding.UTF8.GetString(got.Body.ToArray()), attempt, queue);
		ScheduleVisibilityTimeout(rabbitMqHook);
		return rabbitMqHook;
	}

	private void ScheduleVisibilityTimeout(RabbitMqHook hook)
	{
		Task.Run(async delegate
		{
			try
			{
				await Task.Delay(Math.Max(50, VisibilityTimeoutMilliseconds)).ConfigureAwait(continueOnCapturedContext: false);
				if (!hook.Completed)
				{
					await channel.BasicAckAsync(hook.DeliveryTag, multiple: false).ConfigureAwait(continueOnCapturedContext: false);
					hook.Completed = true;
					await PublishAsync(hook.Queue, hook.Body, hook.Attempt + 1).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				logger?.LogDebug(ex2, "RabbitMQ visibility reclaim failed");
			}
		});
	}

	private void Declare(string queue)
	{
		channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false).GetAwaiter().GetResult();
	}
}
}
