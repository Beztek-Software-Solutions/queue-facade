// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// AWS SQS implementation of <see cref="IQueueProvider"/>.
    /// High / optional low priority queues plus a shared unprocessed (poison) queue.
    /// </summary>
    internal class SqsQueueProvider : IQueueProvider
    {
        /// <summary>SQS ReceiveMessage max batch size.</summary>
        private const int SqsMaxMessagesPerReceive = 10;

        /// <summary>SQS standard queue max payload (256 KiB).</summary>
        private const int SqsMaxMessageBytes = 262_144;

        private const int Zero = 0;
        private const int One = 1;

        private readonly ILogger logger;
        private readonly IAmazonSQS sqs;
        private readonly string highPriorityQueueName;
        private readonly string lowPriorityQueueName;
        private readonly string unprocessedQueueName;
        private readonly object syncLock = new();
        private int queuesCreatedFlag = Zero;

        /// <summary>Resolved queue URLs: [0]=high, optional [1]=low.</summary>
        private readonly List<string> queueUrls = new();

        private string unprocessedQueueUrl;

        internal SqsQueueProvider(SqsQueueProviderConfig config, ILogger logger = null)
        {
            this.logger = logger;
            VisibilityTimeoutMilliseconds = config.VisibilityTimeoutMilliseconds;
            highPriorityQueueName = config.HighPriorityQueue;
            lowPriorityQueueName = config.LowPriorityQueue;
            unprocessedQueueName = config.UnprocessedQueue;
            HasLowPriorityQueue = !string.IsNullOrEmpty(lowPriorityQueueName);
            MaxMessageSize = SqsMaxMessageBytes;
            MaxMessageCountPerPoll = SqsMaxMessagesPerReceive;
            sqs = config.SqsClientCreator.CreateClient(config);
        }

        public bool HasLowPriorityQueue { get; }

        public int MaxMessageSize { get; }

        public int VisibilityTimeoutMilliseconds { get; }

        public int MaxMessageCountPerPoll { get; }

        internal IAmazonSQS SqsClient => sqs;

        public void CreateIfNotExists()
        {
            if (queuesCreatedFlag != Zero)
            {
                return;
            }

            lock (syncLock)
            {
                if (Interlocked.Exchange(ref queuesCreatedFlag, One) != Zero)
                {
                    return;
                }

                try
                {
                    queueUrls.Clear();
                    queueUrls.Add(EnsureQueueUrl(highPriorityQueueName));
                    if (HasLowPriorityQueue)
                    {
                        queueUrls.Add(EnsureQueueUrl(lowPriorityQueueName));
                    }

                    unprocessedQueueUrl = EnsureQueueUrl(unprocessedQueueName);
                }
                catch
                {
                    Interlocked.Exchange(ref queuesCreatedFlag, Zero);
                    queueUrls.Clear();
                    unprocessedQueueUrl = null;
                    throw;
                }
            }
        }

        public async Task<bool> SendMessageAsync(string message, bool useHighPriorityQueue)
        {
            CreateIfNotExists();
            var response = await sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = GetQueueUrl(useHighPriorityQueue),
                MessageBody = message,
            }).ConfigureAwait(false);

            return !string.IsNullOrEmpty(response.MessageId);
        }

        public async Task<bool> SendUnprocessedMessageAsync(string message)
        {
            CreateIfNotExists();
            var response = await sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = unprocessedQueueUrl,
                MessageBody = message,
            }).ConfigureAwait(false);

            return !string.IsNullOrEmpty(response.MessageId);
        }

        public IList<object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue)
        {
            CreateIfNotExists();
            int take = Math.Min(Math.Max(maxMessagesToRetrieve, 1), MaxMessageCountPerPoll);
            int visibilitySeconds = Math.Max(1, (VisibilityTimeoutMilliseconds + 999) / 1000);

            var response = sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = GetQueueUrl(isHighPriorityQueue),
                MaxNumberOfMessages = take,
                VisibilityTimeout = visibilitySeconds,
                MessageSystemAttributeNames = new List<string> { "ApproximateReceiveCount" },
                MessageAttributeNames = new List<string> { "All" },
            }).GetAwaiter().GetResult();

            var messages = response.Messages ?? new List<Message>();
            try
            {
                // Mirror Azure provider: only first delivery (ApproximateReceiveCount == 1).
                messages = messages
                    .Where(m =>
                    {
                        if (m.Attributes == null
                            || !m.Attributes.TryGetValue("ApproximateReceiveCount", out var countStr)
                            || !int.TryParse(countStr, out var count))
                        {
                            return true;
                        }

                        return count == 1;
                    })
                    .ToList();
            }
            catch (Exception e)
            {
                logger?.LogError(e, e.Message);
            }

            return messages.Cast<object>().ToList();
        }

        public string GetMessageBody(object messageHook)
        {
            var message = (Message)messageHook;
            return message?.Body;
        }

        public async Task DeleteMessageAsync(object messageHook, bool isHighPriorityQueue)
        {
            CreateIfNotExists();
            var message = (Message)messageHook;
            if (message == null || string.IsNullOrEmpty(message.ReceiptHandle))
            {
                return;
            }

            await sqs.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = GetQueueUrl(isHighPriorityQueue),
                ReceiptHandle = message.ReceiptHandle,
            }).ConfigureAwait(false);
        }

        public async Task<long> GetApproximateQueueLength(bool isHighPriorityQueue)
        {
            CreateIfNotExists();
            var response = await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                QueueUrl = GetQueueUrl(isHighPriorityQueue),
                AttributeNames = new List<string> { "ApproximateNumberOfMessages" },
            }).ConfigureAwait(false);

            if (response.Attributes != null
                && response.Attributes.TryGetValue("ApproximateNumberOfMessages", out var countStr)
                && long.TryParse(countStr, out var count))
            {
                return count;
            }

            return 0;
        }

        private string GetQueueUrl(bool isHighPriorityQueue)
        {
            return isHighPriorityQueue || !HasLowPriorityQueue || queueUrls.Count < 2
                ? queueUrls[0]
                : queueUrls[^1];
        }

        private string EnsureQueueUrl(string queueName)
        {
            try
            {
                var created = sqs.CreateQueueAsync(new CreateQueueRequest { QueueName = queueName })
                    .GetAwaiter().GetResult();
                logger?.LogDebug("SQS queue ensured: {QueueName} -> {QueueUrl}", queueName, created.QueueUrl);
                return created.QueueUrl;
            }
            catch (QueueNameExistsException)
            {
                var existing = sqs.GetQueueUrlAsync(new GetQueueUrlRequest { QueueName = queueName })
                    .GetAwaiter().GetResult();
                return existing.QueueUrl;
            }
            catch (QueueDeletedRecentlyException)
            {
                // Rare race after delete; surface clearly.
                throw;
            }
        }
    }
}
