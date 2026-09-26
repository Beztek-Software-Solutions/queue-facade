// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using Beztek.Facade.Queue.Providers;

    /// <summary>
    /// Configuration for the Azure Queue Storage provider.
    /// Queue names use portable <see cref="QueueNameValidator"/> rules (same as SQS).
    /// </summary>
    public class AzureQueueProviderConfig : INamedQueueProviderConfig
    {
        /// <summary>
        /// Creates an Azure Queue Storage provider configuration.
        /// </summary>
        /// <param name="name">Logical client name (factory cache key).</param>
        /// <param name="endpoint">Azure Storage connection string or queue endpoint.</param>
        /// <param name="highPriorityQueue">Queue name for high-priority messages (portable naming).</param>
        /// <param name="lowPriorityQueue">Optional low-priority queue name.</param>
        /// <param name="visibilityTimeoutMilliseconds">Visibility timeout applied on receive.</param>
        /// <param name="unprocessedQueue">
        /// Poison queue name. Default: <c>{highPriorityQueue}-unprocessed</c>.
        /// </param>
        public AzureQueueProviderConfig(
            string name,
            string endpoint,
            string highPriorityQueue,
            string lowPriorityQueue = null,
            int visibilityTimeoutMilliseconds = 30000,
            string unprocessedQueue = null)
        {
            // Name validation
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("No name given");
            }

            // Endpoint validation
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentException("No endpoint given");
            }

            // Validation
            if (string.IsNullOrEmpty(highPriorityQueue))
            {
                throw new ArgumentException("No queue name provided");
            }

            AzureQueueNameValidator.ValidateQueueName(QueuePartition.ForValidation(highPriorityQueue));

            if (lowPriorityQueue != null)
            {
                AzureQueueNameValidator.ValidateQueueName(QueuePartition.ForValidation(lowPriorityQueue));
            }

            string poison = string.IsNullOrWhiteSpace(unprocessedQueue)
                ? Constants.DefaultUnprocessedQueueName(highPriorityQueue, QueueNameValidator.MaxLength)
                : unprocessedQueue.Trim();
            AzureQueueNameValidator.ValidateQueueName(QueuePartition.ForValidation(poison));
            if (string.Equals(poison, highPriorityQueue, StringComparison.Ordinal)
                || string.Equals(poison, lowPriorityQueue, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unprocessed (poison) queue name must differ from high/low priority queues");
            }

            this.Name = name;
            this.Endpoint = endpoint;
            this.HighPriorityQueue = highPriorityQueue;
            this.LowPriorityQueue = lowPriorityQueue;
            this.UnprocessedQueue = poison;
            this.VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
        }

        /// <inheritdoc />
        public QueueProviderType QueueProviderType { get; } = QueueProviderType.AzureStorage;

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public int VisibilityTimeoutMilliseconds { get; set; }

        /// <summary>Azure Storage connection string or queue endpoint.</summary>
        public string Endpoint { get; set; }

        /// <summary>High-priority queue name.</summary>
        public string HighPriorityQueue { get; set; }

        /// <summary>Optional low-priority queue name.</summary>
        public string LowPriorityQueue { get; }

        /// <summary>Poison queue name. Default: <c>{highPriorityQueue}-unprocessed</c>.</summary>
        public string UnprocessedQueue { get; }

        internal AzureStorageClientCreator AzureStorageClientCreator { get; set; } = new AzureStorageClientCreator();
    }
}
