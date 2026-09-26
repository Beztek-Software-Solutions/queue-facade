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

internal sealed class BeanstalkdClient : IBeanstalkdClient
{
	private readonly TcpClient tcp;

	private readonly Stream stream;

	private readonly StreamReader reader;

	private readonly bool ownsStream;

	private readonly object gate = new object();

	internal BeanstalkdClient(string host, int port)
	{
		tcp = new TcpClient();
		tcp.Connect(host, port);
		stream = tcp.GetStream();
		ownsStream = true;
		reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 4096, leaveOpen: true);
	}

	/// <summary>Test seam: drive the text protocol over an in-memory duplex stream.</summary>
	internal BeanstalkdClient(Stream stream, bool ownsStream = true)
	{
		tcp = null;
		this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
		this.ownsStream = ownsStream;
		reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 4096, leaveOpen: true);
	}

	public void Dispose()
	{
		reader.Dispose();
		if (ownsStream)
		{
			stream.Dispose();
		}
		tcp?.Dispose();
	}

	public void Use(string tube)
	{
		ExpectOk(Send("use " + tube));
	}

	public void Watch(string tube)
	{
		ExpectWatching(Send("watch " + tube));
	}

	public void Ignore(string tube)
	{
		ExpectWatching(Send("ignore " + tube));
	}

	public ulong Put(int priority, int delaySeconds, int ttrSeconds, string body)
	{
		byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body);
		string s = $"put {priority} {delaySeconds} {ttrSeconds} {bytes.Length}\r\n";
		lock (gate)
		{
			WriteRaw(System.Text.Encoding.ASCII.GetBytes(s));
			WriteRaw(bytes);
			WriteRaw(System.Text.Encoding.ASCII.GetBytes("\r\n"));
			string text = reader.ReadLine() ?? throw new System.IO.IOException("Beanstalkd closed connection");
			if (!text.StartsWith("INSERTED ", StringComparison.Ordinal))
			{
				throw new System.IO.IOException("Beanstalkd put failed: " + text);
			}
			return ulong.Parse(text.AsSpan("INSERTED ".Length), CultureInfo.InvariantCulture);
		}
	}

	public BeanstalkdJob Reserve(int timeoutSeconds)
	{
		lock (gate)
		{
			WriteLine($"reserve-with-timeout {timeoutSeconds}");
			string line = reader.ReadLine();
			if (!BeanstalkdProtocol.TryParseJobStatus(line, "RESERVED ", out var id, out var byteCount))
			{
				return null;
			}
			return new BeanstalkdJob(id, BeanstalkdProtocol.ReadExactChars(reader, byteCount));
		}
	}

	public void Delete(ulong id)
	{
		ExpectOk(Send($"delete {id}"));
	}

	public void Release(ulong id, int priority, int delaySeconds)
	{
		ExpectReleased(Send($"release {id} {priority} {delaySeconds}"));
	}

	public void Bury(ulong id, int priority)
	{
		ExpectBuried(Send($"bury {id} {priority}"));
	}

	public bool KickJob(ulong id)
	{
		string text = Send($"kick-job {id}");
		return text.StartsWith("KICKED", StringComparison.Ordinal);
	}

	public BeanstalkdJob PeekBuried()
	{
		lock (gate)
		{
			WriteLine("peek-buried");
			string line = reader.ReadLine();
			if (!BeanstalkdProtocol.TryParseJobStatus(line, "FOUND ", out var id, out var byteCount))
			{
				return null;
			}
			return new BeanstalkdJob(id, BeanstalkdProtocol.ReadExactChars(reader, byteCount));
		}
	}

	public int StatsTubeCurrentJobsReady(string tube)
	{
		return BeanstalkdProtocol.ParseYamlIntField(ReadOkPayload("stats-tube " + tube, required: true), "current-jobs-ready", 0);
	}

	public int StatsJobReserves(ulong id)
	{
		return BeanstalkdProtocol.ParseYamlIntField(ReadOkPayload($"stats-job {id}", required: false), "reserves", 1);
	}

	private string ReadOkPayload(string command, bool required)
	{
		lock (gate)
		{
			WriteLine(command);
			string text = reader.ReadLine();
			if (!BeanstalkdProtocol.TryParseOkByteCount(text, out var byteCount))
			{
				if (required)
				{
					throw new System.IO.IOException("Beanstalkd stats failed: " + text);
				}
				return string.Empty;
			}
			return BeanstalkdProtocol.ReadExactChars(reader, byteCount);
		}
	}

	private string Send(string command)
	{
		lock (gate)
		{
			WriteLine(command);
			return reader.ReadLine() ?? throw new System.IO.IOException("Beanstalkd closed connection");
		}
	}

	private void WriteLine(string command)
	{
		WriteRaw(System.Text.Encoding.ASCII.GetBytes(command + "\r\n"));
	}

	private void WriteRaw(byte[] bytes)
	{
		stream.Write(bytes, 0, bytes.Length);
	}

	private static void ExpectOk(string line)
	{
		BeanstalkdProtocol.ExpectOk(line);
	}

	private static void ExpectWatching(string line)
	{
		BeanstalkdProtocol.ExpectWatching(line);
	}

	private static void ExpectReleased(string line)
	{
		BeanstalkdProtocol.ExpectReleased(line);
	}

	private static void ExpectBuried(string line)
	{
		BeanstalkdProtocol.ExpectBuried(line);
	}
}

internal sealed class BeanstalkdJob
{
	internal ulong Id { get; }

	internal string Body { get; }

	internal BeanstalkdJob(ulong id, string body)
	{
		Id = id;
		Body = body;
	}
}

internal sealed class BeanstalkdProvider : IQueueProvider
{
	internal sealed class BeanstalkdHook
	{
		internal ulong Id { get; }

		internal string Body { get; }

		internal int AttemptHint { get; }

		internal BeanstalkdHook(ulong id, string body, int attemptHint)
		{
			Id = id;
			Body = body;
			AttemptHint = attemptHint;
		}
	}

	private const int Zero = 0;

	private const int One = 1;

	private readonly ILogger logger;

	private readonly string host;

	private readonly int port;

	private readonly string high;

	private readonly string low;

	private readonly string poison;

	private readonly object sync = new object();

	private int created;

	private IBeanstalkdClient client;

	public bool HasLowPriorityQueue { get; }

	public int MaxMessageSize { get; }

	public int VisibilityTimeoutMilliseconds { get; }

	public int MaxMessageCountPerPoll { get; }

	internal BeanstalkdProvider(BeanstalkdProviderConfig config, ILogger logger = null)
	{
		this.logger = logger;
		VisibilityTimeoutMilliseconds = config.VisibilityTimeoutMilliseconds;
		host = config.Host;
		port = config.Port;
		high = config.HighPriorityQueue;
		low = config.LowPriorityQueue;
		poison = config.UnprocessedQueue;
		HasLowPriorityQueue = !string.IsNullOrEmpty(low);
		MaxMessageSize = 65536;
		MaxMessageCountPerPoll = 32;
	}

	/// <summary>Test seam: inject a client and skip TCP connect.</summary>
	internal BeanstalkdProvider(BeanstalkdProviderConfig config, IBeanstalkdClient client, ILogger logger = null)
		: this(config, logger)
	{
		this.client = client ?? throw new ArgumentNullException(nameof(client));
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
				client = new BeanstalkdClient(host, port);
				client.Use(high);
				client.Watch(high);
				try
				{
					client.Ignore("default");
				}
				catch (System.IO.IOException)
				{
				}
				logger?.LogDebug("Beanstalkd connected {Host}:{Port}", host, port);
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
		string tube = ((useHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		PutOnTube(tube, message);
		return Task.FromResult(result: true);
	}

	public Task<bool> SendUnprocessedMessageAsync(string message)
	{
		CreateIfNotExists();
		PutOnTube(poison, message);
		return Task.FromResult(result: true);
	}

	public IList<object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string tube = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		return ReserveMany(tube, maxMessagesToRetrieve);
	}

	public int GetReceiveCount(object messageHook)
	{
		if (messageHook is BeanstalkdHook beanstalkdHook)
		{
			try
			{
				return Math.Max(1, client.StatsJobReserves(beanstalkdHook.Id));
			}
			catch
			{
				return Math.Max(1, beanstalkdHook.AttemptHint);
			}
		}
		return 1;
	}

	public IList<object> PeekUnprocessedMessages(int maxMessagesToRetrieve)
	{
		CreateIfNotExists();
		return ReceiveUnprocessedMessages(maxMessagesToRetrieve);
	}

	public IList<object> ReceiveUnprocessedMessages(int maxMessagesToRetrieve)
	{
		CreateIfNotExists();
		return ReserveMany(poison, maxMessagesToRetrieve);
	}

	public Task DeleteUnprocessedMessageAsync(object messageHook)
	{
		if (messageHook is BeanstalkdHook beanstalkdHook)
		{
			client.Delete(beanstalkdHook.Id);
		}
		return Task.CompletedTask;
	}

	public Task<long> GetApproximateUnprocessedQueueLength()
	{
		CreateIfNotExists();
		return Task.FromResult((long)client.StatsTubeCurrentJobsReady(poison));
	}

	public string GetMessageBody(object messageHook)
	{
		return (messageHook is BeanstalkdHook beanstalkdHook) ? beanstalkdHook.Body : messageHook?.ToString();
	}

	public Task DeleteMessageAsync(object messageHook, bool isHighPriorityQueue)
	{
		if (messageHook is BeanstalkdHook beanstalkdHook)
		{
			client.Delete(beanstalkdHook.Id);
		}
		return Task.CompletedTask;
	}

	public Task<long> GetApproximateQueueLength(bool isHighPriorityQueue)
	{
		CreateIfNotExists();
		string tube = ((isHighPriorityQueue || !HasLowPriorityQueue) ? high : low);
		return Task.FromResult((long)client.StatsTubeCurrentJobsReady(tube));
	}

	private void PutOnTube(string tube, string body)
	{
		lock (sync)
		{
			client.Use(tube);
			int ttrSeconds = Math.Max(1, (VisibilityTimeoutMilliseconds + 999) / 1000);
			client.Put(1024, 0, ttrSeconds, body);
			client.Use(high);
		}
	}

	private IList<object> ReserveMany(string tube, int max)
	{
		int num = Math.Min(Math.Max(max, 1), MaxMessageCountPerPoll);
		List<object> list = new List<object>();
		lock (sync)
		{
			WatchOnly(tube);
			for (int i = 0; i < num; i++)
			{
				BeanstalkdJob beanstalkdJob = client.Reserve(0);
				if (beanstalkdJob == null)
				{
					break;
				}
				int attemptHint = 1;
				try
				{
					attemptHint = client.StatsJobReserves(beanstalkdJob.Id);
				}
				catch
				{
				}
				list.Add(new BeanstalkdHook(beanstalkdJob.Id, beanstalkdJob.Body, attemptHint));
			}
			WatchOnly(high);
		}
		return list;
	}

	private void WatchOnly(string tube)
	{
		client.Watch(tube);
		string[] array = new string[4] { high, low, poison, "default" };
		foreach (string text in array)
		{
			if (!string.IsNullOrEmpty(text) && !string.Equals(text, tube, StringComparison.Ordinal))
			{
				try
				{
					client.Ignore(text);
				}
				catch (System.IO.IOException)
				{
				}
			}
		}
	}
}
}
