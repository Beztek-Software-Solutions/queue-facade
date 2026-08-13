// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;

    /// <summary>
    /// Configuration for the AWS SQS queue provider.
    /// Queue names use portable <see cref="QueueNameValidator"/> rules (same as Azure).
    /// </summary>
    public class SqsQueueProviderConfig : IQueueProviderConfig
    {
        /// <param name="name">Logical client name (factory cache key).</param>
        /// <param name="region">AWS region system name (e.g. us-east-1). Required unless <paramref name="serviceUrl"/> is set.</param>
        /// <param name="highPriorityQueue">Queue name for high-priority messages (portable naming).</param>
        /// <param name="lowPriorityQueue">Optional low-priority queue name.</param>
        /// <param name="visibilityTimeoutMilliseconds">Visibility timeout applied on receive (converted to seconds for SQS).</param>
        /// <param name="serviceUrl">Optional custom endpoint (e.g. LocalStack http://localhost:4566).</param>
        /// <param name="accessKeyId">Optional explicit access key; otherwise the default AWS credential chain is used.</param>
        /// <param name="secretAccessKey">Optional explicit secret key.</param>
        /// <param name="unprocessedQueue">
        /// Poison queue name. Default: <c>{highPriorityQueue}-unprocessed</c> so multiple apps in one AWS account do not collide.
        /// </param>
        public SqsQueueProviderConfig(
            string name,
            string region,
            string highPriorityQueue,
            string lowPriorityQueue = null,
            int visibilityTimeoutMilliseconds = 30000,
            string serviceUrl = null,
            string accessKeyId = null,
            string secretAccessKey = null,
            string unprocessedQueue = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("No name given");
            }

            if (string.IsNullOrEmpty(serviceUrl) && string.IsNullOrEmpty(region))
            {
                throw new ArgumentException("No region given");
            }

            if (string.IsNullOrEmpty(highPriorityQueue))
            {
                throw new ArgumentException("No queue name provided");
            }

            QueueNameValidator.ValidateQueueName(QueuePartition.ForValidation(highPriorityQueue));
            if (lowPriorityQueue != null)
            {
                QueueNameValidator.ValidateQueueName(QueuePartition.ForValidation(lowPriorityQueue));
            }

            string poison = string.IsNullOrWhiteSpace(unprocessedQueue)
                ? Constants.DefaultUnprocessedQueueName(highPriorityQueue, QueueNameValidator.MaxLength)
                : unprocessedQueue.Trim();
            QueueNameValidator.ValidateQueueName(QueuePartition.ForValidation(poison));
            if (string.Equals(poison, highPriorityQueue, StringComparison.Ordinal)
                || string.Equals(poison, lowPriorityQueue, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unprocessed (poison) queue name must differ from high/low priority queues");
            }

            Name = name;
            Region = region ?? string.Empty;
            HighPriorityQueue = highPriorityQueue;
            LowPriorityQueue = lowPriorityQueue;
            UnprocessedQueue = poison;
            VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
            ServiceUrl = serviceUrl;
            AccessKeyId = accessKeyId;
            SecretAccessKey = secretAccessKey;
        }

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.AwsSqs;

        public string Name { get; set; }

        public int VisibilityTimeoutMilliseconds { get; set; }

        public string Region { get; set; }

        public string HighPriorityQueue { get; set; }

        public string LowPriorityQueue { get; }

        /// <summary>Poison / dead-letter style queue for failed processing (per client, not global).</summary>
        public string UnprocessedQueue { get; }

        /// <summary>Custom SQS endpoint (LocalStack / VPC endpoint). Null = regional AWS.</summary>
        public string ServiceUrl { get; set; }

        public string AccessKeyId { get; set; }

        public string SecretAccessKey { get; set; }

        internal SqsClientCreator SqsClientCreator { get; set; } = new SqsClientCreator();
    }
}
