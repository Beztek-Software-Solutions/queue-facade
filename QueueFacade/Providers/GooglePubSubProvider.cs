// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Google.Cloud.PubSub.V1;
    using Microsoft.Extensions.Logging;

internal sealed class GooglePubSubProvider : IQueueProvider
{
	private const int Zero = 0;

	private const int One = 1;

	private readonly ILogger logger;

	private readonly string projectId;

	private readonly string highTopic;

	private readonly string lowTopic;

	private readonly string poisonTopic;

	private readonly object sync = new object();

	private int created;

	private IPubSubOps ops;

	public bool HasLowPriorityQueue { get; }

	public int MaxMessageSize { get; }

	public int VisibilityTimeoutMilliseconds { get; }

	public int MaxMessageCountPerPoll { get; }

	internal GooglePubSubProvider(GooglePubSubProviderConfig config, ILogger logger = null)
	{
		this.logger = logger;
		VisibilityTimeoutMilliseconds = config.VisibilityTimeoutMilliseconds;
		projectId = config.ProjectId;
		highTopic = config.HighPriorityQueue;
		lowTopic = config.LowPriorityQueue;
		poisonTopic = config.UnprocessedQueue;
		HasLowPriorityQueue = !string.IsNullOrEmpty(lowTopic);
		MaxMessageSize = 262144;
		MaxMessageCountPerPoll = 100;
		if (!string.IsNullOrWhiteSpace(config.EmulatorHost))
		{
			Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", config.EmulatorHost);
		}
	}

	/// <summary>Test seam: inject ops and skip live client construction.</summary>
	internal GooglePubSubProvider(GooglePubSubProviderConfig config, IPubSubOps ops, ILogger logger = null)
		: this(config, logger)
	{
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
				ops = new PubSubOps(logger);
				EnsureTopicAndSubscription(highTopic);
				if (HasLowPriorityQueue)
				{
					EnsureTopicAndSubscription(lowTopic);
				}
				EnsureTopicAndSubscription(poisonTopic);
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
		string topic = ((useHighPriorityQueue || !HasLowPriorityQueue) ? highTopic : lowTopic);
		await ops.PublishAsync(projectId, topic, message).ConfigureAwait(continueOnCapturedContext: false);
		return true;
	}

	public async Task<bool> SendUnprocessedMessageAsync(string message)
	{
		CreateIfNotExists();
		await ops.PublishAsync(projectId, poisonTopic, message).ConfigureAwait(continueOnCapturedContext: false);
		return true;
	}

	public IList<object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string topicId = ((isHighPriorityQueue || !HasLowPriorityQueue) ? highTopic : lowTopic);
		return Pull(SubscriptionName(topicId), maxMessagesToRetrieve);
	}

	public int GetReceiveCount(object messageHook)
	{
		return (!(messageHook is ReceivedMessage { DeliveryAttempt: >0 } receivedMessage)) ? 1 : receivedMessage.DeliveryAttempt;
	}

	public IList<object> PeekUnprocessedMessages(int maxMessagesToRetrieve)
	{
		return ReceiveUnprocessedMessages(maxMessagesToRetrieve);
	}

	public IList<object> ReceiveUnprocessedMessages(int maxMessagesToRetrieve)
	{
		CreateIfNotExists();
		return Pull(SubscriptionName(poisonTopic), maxMessagesToRetrieve);
	}

	public async Task DeleteUnprocessedMessageAsync(object messageHook)
	{
		if (messageHook is ReceivedMessage m)
		{
			await ops.AcknowledgeAsync(SubscriptionName(poisonTopic), m.AckId).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public Task<long> GetApproximateUnprocessedQueueLength()
	{
		CreateIfNotExists();
		IList<object> list = Pull(SubscriptionName(poisonTopic), 1, ackDeadlineOnly: true);
		return Task.FromResult((list.Count > 0) ? Math.Max(1L, list.Count) : 0);
	}

	public string GetMessageBody(object messageHook)
	{
		return (messageHook is ReceivedMessage receivedMessage) ? receivedMessage.Message.Data.ToStringUtf8() : messageHook?.ToString();
	}

	public async Task DeleteMessageAsync(object messageHook, bool isHighPriorityQueue)
	{
		if (messageHook is ReceivedMessage m)
		{
			string topic = ((isHighPriorityQueue || !HasLowPriorityQueue) ? highTopic : lowTopic);
			await ops.AcknowledgeAsync(SubscriptionName(topic), m.AckId).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public Task<long> GetApproximateQueueLength(bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string topicId = ((isHighPriorityQueue || !HasLowPriorityQueue) ? highTopic : lowTopic);
		IList<object> list = Pull(SubscriptionName(topicId), 1, ackDeadlineOnly: true);
		return Task.FromResult((list.Count > 0) ? Math.Max(1L, list.Count) : 0);
	}

	private IList<object> Pull(SubscriptionName subscription, int max, bool ackDeadlineOnly = false)
	{
		int maxMessages = PubSubPullHelpers.ClampTake(max, MaxMessageCountPerPoll);
		int visibilityAckSeconds = PubSubPullHelpers.AckDeadlineSeconds(VisibilityTimeoutMilliseconds);
		return ops.Pull(subscription, maxMessages, ackDeadlineOnly, visibilityAckSeconds);
	}

	private void EnsureTopicAndSubscription(string topicId)
	{
		ops.EnsureTopicAndSubscription(projectId, topicId, Math.Max(10, VisibilityTimeoutMilliseconds / 1000));
	}

	private SubscriptionName SubscriptionName(string topicId)
	{
		return Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(projectId, topicId + "-sub");
	}
}
}
