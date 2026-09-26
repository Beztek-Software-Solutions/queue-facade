// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using Beztek.Facade.Queue.Providers;

    /// <summary>
    /// Configuration for the AWS SQS queue provider.
    /// Queue names use portable <see cref="QueueNameValidator"/> rules (same as Azure).
    /// </summary>
    public class SqsQueueProviderConfig : INamedQueueProviderConfig
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
        /// <param name="sessionToken">
        /// Optional STS session token for temporary credentials. Does not auto-refresh;
        /// prefer omitting keys so the default AWS credential chain (IAM roles) is used.
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
            string unprocessedQueue = null,
            string sessionToken = null)
        {
            RequireNonEmpty(name, "No name given");
            RequireRegionOrServiceUrl(region, serviceUrl);
            RequireNonEmpty(highPriorityQueue, "No queue name provided");

            ValidateNamedQueue(highPriorityQueue);
            if (lowPriorityQueue != null)
            {
                ValidateNamedQueue(lowPriorityQueue);
            }

            string poison = ResolvePoisonQueueName(highPriorityQueue, unprocessedQueue);
            EnsurePoisonDistinct(poison, highPriorityQueue, lowPriorityQueue);

            Name = name;
            Region = region ?? string.Empty;
            HighPriorityQueue = highPriorityQueue;
            LowPriorityQueue = lowPriorityQueue;
            UnprocessedQueue = poison;
            VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
            ServiceUrl = serviceUrl;
            AccessKeyId = accessKeyId;
            SecretAccessKey = secretAccessKey;
            SessionToken = sessionToken;
        }

        /// <inheritdoc />
        public QueueProviderType QueueProviderType { get; } = QueueProviderType.AwsSqs;

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public int VisibilityTimeoutMilliseconds { get; set; }

        /// <summary>AWS region system name (e.g. us-east-1).</summary>
        public string Region { get; set; }

        /// <summary>High-priority queue name.</summary>
        public string HighPriorityQueue { get; set; }

        /// <summary>Optional low-priority queue name.</summary>
        public string LowPriorityQueue { get; }

        /// <summary>Poison / dead-letter style queue for failed processing (per client, not global).</summary>
        public string UnprocessedQueue { get; }

        /// <summary>Custom SQS endpoint (LocalStack / VPC endpoint). Null = regional AWS.</summary>
        public string ServiceUrl { get; set; }

        /// <summary>Optional explicit access key; otherwise the default AWS credential chain is used.</summary>
        public string AccessKeyId { get; set; }

        /// <summary>Optional explicit secret key.</summary>
        public string SecretAccessKey { get; set; }

        /// <summary>
        /// Optional STS session token for temporary credentials. Does not auto-refresh;
        /// prefer omitting <see cref="AccessKeyId"/> / <see cref="SecretAccessKey"/> for IAM roles.
        /// </summary>
        public string SessionToken { get; set; }

        internal SqsClientCreator SqsClientCreator { get; set; } = new SqsClientCreator();

        private static void RequireNonEmpty(string value, string message)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(message);
            }
        }

        private static void RequireRegionOrServiceUrl(string region, string serviceUrl)
        {
            if (string.IsNullOrEmpty(serviceUrl) && string.IsNullOrEmpty(region))
            {
                throw new ArgumentException("No region given");
            }
        }

        private static void ValidateNamedQueue(string queueName) =>
            QueueNameValidator.ValidateQueueName(QueuePartition.ForValidation(queueName));

        private static string ResolvePoisonQueueName(string highPriorityQueue, string unprocessedQueue)
        {
            string poison = string.IsNullOrWhiteSpace(unprocessedQueue)
                ? Constants.DefaultUnprocessedQueueName(highPriorityQueue, QueueNameValidator.MaxLength)
                : unprocessedQueue.Trim();
            ValidateNamedQueue(poison);
            return poison;
        }

        private static void EnsurePoisonDistinct(string poison, string highPriorityQueue, string lowPriorityQueue)
        {
            if (string.Equals(poison, highPriorityQueue, StringComparison.Ordinal)
                || string.Equals(poison, lowPriorityQueue, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unprocessed (poison) queue name must differ from high/low priority queues");
            }
        }
    }
}
