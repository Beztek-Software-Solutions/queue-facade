// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Microsoft.Extensions.Logging;

internal sealed class AzureServiceBusProvider : IQueueProvider
{
	private const int Zero = 0;

	private const int One = 1;

	private readonly ILogger logger;

	private readonly IAzureServiceBusOps ops;

	private readonly string high;

	private readonly string low;

	private readonly string poison;

	private readonly object sync = new object();

	private int created;

	public bool HasLowPriorityQueue { get; }

	public int MaxMessageSize { get; }

	public int VisibilityTimeoutMilliseconds { get; }

	public int MaxMessageCountPerPoll { get; }

	internal AzureServiceBusProvider(AzureServiceBusProviderConfig config, ILogger logger = null)
	{
		this.logger = logger;
		VisibilityTimeoutMilliseconds = config.VisibilityTimeoutMilliseconds;
		high = config.HighPriorityQueue;
		low = config.LowPriorityQueue;
		poison = config.UnprocessedQueue;
		HasLowPriorityQueue = !string.IsNullOrEmpty(low);
		MaxMessageSize = 262144;
		MaxMessageCountPerPoll = 32;
		ops = new AzureServiceBusOps(config.ConnectionString, config.AdministrationConnectionString, logger);
	}

	/// <summary>Test seam: inject ops and skip live client construction.</summary>
	internal AzureServiceBusProvider(AzureServiceBusProviderConfig config, IAzureServiceBusOps ops, ILogger logger = null)
	{
		this.logger = logger;
		VisibilityTimeoutMilliseconds = config.VisibilityTimeoutMilliseconds;
		high = config.HighPriorityQueue;
		low = config.LowPriorityQueue;
		poison = config.UnprocessedQueue;
		HasLowPriorityQueue = !string.IsNullOrEmpty(low);
		MaxMessageSize = 262144;
		MaxMessageCountPerPoll = 32;
		this.ops = ops ?? throw new ArgumentNullException(nameof(ops));
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
				EnsureQueue(high);
				if (HasLowPriorityQueue)
				{
					EnsureQueue(low);
				}
				EnsureQueue(poison);
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
		await ops.SendAsync(queue, message).ConfigureAwait(continueOnCapturedContext: false);
		return true;
	}

	public async Task<bool> SendUnprocessedMessageAsync(string message)
	{
		CreateIfNotExists();
		await ops.SendAsync(poison, message).ConfigureAwait(continueOnCapturedContext: false);
		return true;
	}

	public IList<object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string queue = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		return Receive(queue, maxMessagesToRetrieve);
	}

	public int GetReceiveCount(object messageHook)
	{
		return (!(messageHook is ServiceBusReceivedMessage serviceBusReceivedMessage)) ? 1 : Math.Max(1, serviceBusReceivedMessage.DeliveryCount);
	}

	public IList<object> PeekUnprocessedMessages(int maxMessagesToRetrieve)
	{
		CreateIfNotExists();
		int maxMessages = Math.Min(Math.Max(maxMessagesToRetrieve, 1), MaxMessageCountPerPoll);
		IReadOnlyList<ServiceBusReceivedMessage> result = ops.PeekAsync(poison, maxMessages).GetAwaiter().GetResult();
		return new List<object>(result);
	}

	public IList<object> ReceiveUnprocessedMessages(int maxMessagesToRetrieve)
	{
		CreateIfNotExists();
		return Receive(poison, maxMessagesToRetrieve);
	}

	public async Task DeleteUnprocessedMessageAsync(object messageHook)
	{
		if (!(messageHook is ServiceBusReceivedMessage message))
		{
			return;
		}
		await ops.CompleteAsync(poison, message).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<long> GetApproximateUnprocessedQueueLength()
	{
		CreateIfNotExists();
		return await ops.GetActiveMessageCountAsync(poison).ConfigureAwait(continueOnCapturedContext: false);
	}

	public string GetMessageBody(object messageHook)
	{
		return (messageHook is ServiceBusReceivedMessage serviceBusReceivedMessage) ? serviceBusReceivedMessage.Body.ToString() : messageHook?.ToString();
	}

	public async Task DeleteMessageAsync(object messageHook, bool isHighPriorityQueue)
	{
		if (!(messageHook is ServiceBusReceivedMessage message))
		{
			return;
		}
		string queue = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		await ops.CompleteAsync(queue, message).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<long> GetApproximateQueueLength(bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string queue = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		return await ops.GetActiveMessageCountAsync(queue).ConfigureAwait(continueOnCapturedContext: false);
	}

	private IList<object> Receive(string queue, int max)
	{
		int maxMessages = Math.Min(Math.Max(max, 1), MaxMessageCountPerPoll);
		IReadOnlyList<ServiceBusReceivedMessage> result = ops.ReceiveAsync(queue, maxMessages, TimeSpan.FromMilliseconds(200)).GetAwaiter().GetResult();
		return new List<object>(result ?? Array.Empty<ServiceBusReceivedMessage>());
	}

	private void EnsureQueue(string queueName)
	{
		TimeSpan lockDuration = TimeSpan.FromMilliseconds(Math.Max(5000, Math.Min(VisibilityTimeoutMilliseconds, 300000)));
		ops.EnsureQueueAsync(queueName, lockDuration).GetAwaiter().GetResult();
	}
}
}
