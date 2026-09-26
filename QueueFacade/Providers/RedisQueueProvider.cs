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
    using StackExchange.Redis;

internal sealed class RedisQueueProvider : IQueueProvider
{
	internal sealed class RedisHook
	{
		internal string Queue { get; }

		internal string Body { get; }

		internal int Attempt { get; }

		internal RedisValue? ProcessingPayload { get; }

		internal RedisHook(string queue, string body, int attempt, RedisValue? processingPayload)
		{
			Queue = queue;
			Body = body;
			Attempt = attempt;
			ProcessingPayload = processingPayload;
		}
	}

	private const int Zero = 0;

	private const int One = 1;

	/// <summary>Bound reclaim ListRange so large processing lists cannot stall a poll tick.</summary>
	internal const int MaxReclaimScan = 512;

	private readonly ILogger logger;

	private readonly ConnectionMultiplexer mux;

	private readonly IDatabase db;

	private readonly string high;

	private readonly string low;

	private readonly string poison;

	private readonly object sync = new object();

	private int created;

	private int reclaimStarted;

	public bool HasLowPriorityQueue { get; }

	public int MaxMessageSize { get; }

	public int VisibilityTimeoutMilliseconds { get; }

	public int MaxMessageCountPerPoll { get; }

	internal RedisQueueProvider(RedisQueueProviderConfig config, ILogger logger = null)
	{
		this.logger = logger;
		VisibilityTimeoutMilliseconds = config.VisibilityTimeoutMilliseconds;
		high = config.HighPriorityQueue;
		low = config.LowPriorityQueue;
		poison = config.UnprocessedQueue;
		HasLowPriorityQueue = !string.IsNullOrEmpty(low);
		MaxMessageSize = 262144;
		MaxMessageCountPerPoll = 32;
		mux = ConnectionMultiplexer.Connect(config.Configuration);
		db = mux.GetDatabase();
	}

	/// <summary>Test seam: inject an <see cref="T:StackExchange.Redis.IDatabase" /> without opening a multiplexer.</summary>
	internal RedisQueueProvider(RedisQueueProviderConfig config, IDatabase database, ILogger logger = null)
	{
		this.logger = logger;
		VisibilityTimeoutMilliseconds = config.VisibilityTimeoutMilliseconds;
		high = config.HighPriorityQueue;
		low = config.LowPriorityQueue;
		poison = config.UnprocessedQueue;
		HasLowPriorityQueue = !string.IsNullOrEmpty(low);
		MaxMessageSize = 262144;
		MaxMessageCountPerPoll = 32;
		mux = null;
		db = database ?? throw new ArgumentNullException("database");
	}

	public void CreateIfNotExists()
	{
		if (created != 0)
		{
			return;
		}
		lock (sync)
		{
			if (Interlocked.Exchange(ref created, 1) == 0)
			{
				EnsureReclaimDaemon();
			}
		}
	}

	public async Task<bool> SendMessageAsync(string message, bool useHighPriorityQueue)
	{
		CreateIfNotExists();
		string key = RedisQueueCodec.ReadyKey((useHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		await db.ListLeftPushAsync(key, RedisQueueCodec.EncodeReady(message, 1)).ConfigureAwait(continueOnCapturedContext: false);
		return true;
	}

	public async Task<bool> SendUnprocessedMessageAsync(string message)
	{
		CreateIfNotExists();
		await db.ListLeftPushAsync(RedisQueueCodec.ReadyKey(poison), RedisQueueCodec.EncodeReady(message, 1)).ConfigureAwait(continueOnCapturedContext: false);
		return true;
	}

	public IList<object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string queue = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		return PopMany(queue, maxMessagesToRetrieve);
	}

	public int GetReceiveCount(object messageHook)
	{
		return (!(messageHook is RedisHook redisHook)) ? 1 : Math.Max(1, redisHook.Attempt);
	}

	public IList<object> PeekUnprocessedMessages(int maxMessagesToRetrieve)
	{
		CreateIfNotExists();
		int num = Math.Min(Math.Max(maxMessagesToRetrieve, 1), MaxMessageCountPerPoll);
		RedisValue[] array = db.ListRange(RedisQueueCodec.ReadyKey(poison), 0L, num - 1);
		List<object> list = new List<object>();
		RedisValue[] array2 = array;
		foreach (RedisValue value in array2)
		{
			if (RedisQueueCodec.TryParseReady(value, out var body, out var attempt))
			{
				list.Add(new RedisHook(poison, body, attempt, null));
			}
		}
		return list;
	}

	public IList<object> ReceiveUnprocessedMessages(int maxMessagesToRetrieve)
	{
		CreateIfNotExists();
		return PopMany(poison, maxMessagesToRetrieve);
	}

	public async Task DeleteUnprocessedMessageAsync(object messageHook)
	{
		RedisHook hook = messageHook as RedisHook;
		if (hook?.ProcessingPayload.HasValue ?? false)
		{
			await db.ListRemoveAsync(RedisQueueCodec.ProcKey(poison), hook.ProcessingPayload.Value, 1L).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task<long> GetApproximateUnprocessedQueueLength()
	{
		CreateIfNotExists();
		return await db.ListLengthAsync(RedisQueueCodec.ReadyKey(poison)).ConfigureAwait(continueOnCapturedContext: false);
	}

	public string GetMessageBody(object messageHook)
	{
		return (messageHook is RedisHook redisHook) ? redisHook.Body : messageHook?.ToString();
	}

	public async Task DeleteMessageAsync(object messageHook, bool isHighPriorityQueue)
	{
		if (TryGetProcessingHook(messageHook, out var hook))
		{
			string queue = ResolveQueue(isHighPriorityQueue);
			await db.ListRemoveAsync(RedisQueueCodec.ProcKey(queue), hook.ProcessingPayload.Value, 1L).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private bool TryGetProcessingHook(object messageHook, out RedisHook hook)
	{
		hook = messageHook as RedisHook;
		return hook != null && hook.ProcessingPayload.HasValue;
	}

	private string ResolveQueue(bool isHighPriorityQueue)
	{
		return (isHighPriorityQueue || !HasLowPriorityQueue) ? high : low;
	}

	public async Task<long> GetApproximateQueueLength(bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string queue = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		return await db.ListLengthAsync(RedisQueueCodec.ReadyKey(queue)).ConfigureAwait(continueOnCapturedContext: false);
	}

	private IList<object> PopMany(string queue, int max)
	{
		ReclaimExpired(queue);
		int num = Math.Min(Math.Max(max, 1), MaxMessageCountPerPoll);
		List<object> list = new List<object>();
		for (int i = 0; i < num; i++)
		{
			RedisValue value = db.ListRightPopLeftPush(RedisQueueCodec.ReadyKey(queue), RedisQueueCodec.ProcKey(queue));
			if (value.IsNullOrEmpty)
			{
				break;
			}
			if (!RedisQueueCodec.TryParseReady(value, out var body, out var attempt))
			{
				db.ListRemove(RedisQueueCodec.ProcKey(queue), value, 1L);
				continue;
			}
			long visibleAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + VisibilityTimeoutMilliseconds;
			string text = RedisQueueCodec.EncodeProcessing(body, attempt, visibleAtUnixMs);
			db.ListRemove(RedisQueueCodec.ProcKey(queue), value, 1L);
			db.ListLeftPush(RedisQueueCodec.ProcKey(queue), text);
			list.Add(new RedisHook(queue, body, attempt, text));
		}
		return list;
	}

	private void EnsureReclaimDaemon()
	{
		if (mux == null || Interlocked.Exchange(ref reclaimStarted, 1) != 0)
		{
			return;
		}
		Task.Run(async delegate
		{
			while (true)
			{
				try
				{
					ReclaimExpired(high);
					if (HasLowPriorityQueue)
					{
						ReclaimExpired(low);
					}
					ReclaimExpired(poison);
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					logger?.LogDebug(ex2, "Redis reclaim tick failed");
				}
				await Task.Delay(Math.Max(50, VisibilityTimeoutMilliseconds / 4)).ConfigureAwait(continueOnCapturedContext: false);
			}
		});
	}

	internal void ReclaimExpired(string queue)
	{
		// LPUSH puts newest at index 0; reclaim a bounded tail (oldest) so expiry stays fair.
		RedisValue[] array = db.ListRange(RedisQueueCodec.ProcKey(queue), -MaxReclaimScan, -1L);
		long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		RedisValue[] array2 = array;
		foreach (RedisValue value in array2)
		{
			TryReclaimOne(queue, value, nowUnixMs);
		}
	}

	private void TryReclaimOne(string queue, RedisValue value, long nowUnixMs)
	{
		if (!RedisQueueCodec.TryParseProcessing(value, out var visibleAt, out var body, out var attempt))
		{
			db.ListRemove(RedisQueueCodec.ProcKey(queue), value, 1L);
		}
		else if (RedisQueueCodec.IsExpired(visibleAt, nowUnixMs) && db.ListRemove(RedisQueueCodec.ProcKey(queue), value, 1L) > 0)
		{
			db.ListLeftPush(RedisQueueCodec.ReadyKey(queue), RedisQueueCodec.EncodeReady(body, attempt + 1));
		}
	}

}
}
