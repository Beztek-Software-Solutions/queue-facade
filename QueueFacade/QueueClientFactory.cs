// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;
    using Queue.Providers;

    /// <summary>
    /// QueueClientFactory.
    /// </summary>
    public static class QueueClientFactory
    {
        private static readonly ConcurrentDictionary<string, IQueueClient> queueClientMap = new ConcurrentDictionary<string, IQueueClient>();
        private static readonly ConcurrentDictionary<string, IPartitionedQueueClient> partitionedClientMap =
            new ConcurrentDictionary<string, IPartitionedQueueClient>();

        /// <summary>
        /// Gets an instance of Queue Client based on the provider config provided.
        /// Supports Azure Queue Storage, AWS SQS, and LocalMemory (for testing / single-process).
        /// Queue names must be fully resolved (no <c>{partition}</c>); use
        /// <see cref="GetPartitionedQueueClient"/> for multi-tenant templates.
        /// </summary>
        public static IQueueClient GetQueueClient(IQueueProviderConfig queueProviderConfig, ILogger logger = null)
        {
            RejectUnresolvedPartitionTemplates(queueProviderConfig);

            string key = $"{queueProviderConfig.Name}:{queueProviderConfig.QueueProviderType}";
            if (!queueClientMap.TryGetValue(key, out IQueueClient result))
            {
                switch (queueProviderConfig.QueueProviderType)
                {
                    case QueueProviderType.AzureStorage:
                        AzureQueueProviderConfig config = (AzureQueueProviderConfig)queueProviderConfig;
                        IQueueProvider queueProvider = new AzureQueueProvider(config, logger);
                        result = new QueueClient(queueProviderConfig.Name, queueProvider, logger);
                        queueClientMap.GetOrAdd(key, result);
                        break;
                    case QueueProviderType.AwsSqs:
                        SqsQueueProviderConfig sqsConfig = (SqsQueueProviderConfig)queueProviderConfig;
                        queueProvider = new SqsQueueProvider(sqsConfig, logger);
                        result = new QueueClient(queueProviderConfig.Name, queueProvider, logger);
                        queueClientMap.GetOrAdd(key, result);
                        break;
                    case QueueProviderType.LocalMemory:
                        queueProvider = new LocalMemoryQueueProvider(logger, true, queueProviderConfig.VisibilityTimeoutMilliseconds);
                        result = new QueueClient(queueProviderConfig.Name, queueProvider, logger);
                        queueClientMap.GetOrAdd(key, result);
                        break;
                    default:
                        logger?.LogError($"Unknown Queue Provider: {queueProviderConfig.QueueProviderType}");
                        throw new NotSupportedException($"Unsupported Queue Provider: {queueProviderConfig.QueueProviderType}");
                }
            }
            return result;
        }

        /// <summary>
        /// Multi-tenant entry point: queue name templates include <c>{partition}</c> (e.g. customer id).
        /// Call <see cref="IPartitionedQueueClient.ForPartition"/> per tenant.
        /// </summary>
        public static IPartitionedQueueClient GetPartitionedQueueClient(
            IQueueProviderConfig templateConfig,
            ILogger logger = null)
        {
            if (templateConfig == null)
            {
                throw new ArgumentNullException(nameof(templateConfig));
            }

            // Validate this config even when a client is already cached under the same name.
            PartitionedQueueClient.ValidateTemplate(templateConfig);

            string key = $"partitioned:{templateConfig.Name}:{templateConfig.QueueProviderType}";
            return partitionedClientMap.GetOrAdd(
                key,
                _ => new PartitionedQueueClient(templateConfig, logger));
        }

        private static void RejectUnresolvedPartitionTemplates(IQueueProviderConfig config)
        {
            switch (config.QueueProviderType)
            {
                case QueueProviderType.AwsSqs:
                {
                    var sqs = (SqsQueueProviderConfig)config;
                    if (QueuePartition.ContainsToken(sqs.HighPriorityQueue)
                        || QueuePartition.ContainsToken(sqs.LowPriorityQueue)
                        || QueuePartition.ContainsToken(sqs.UnprocessedQueue))
                    {
                        throw new ArgumentException(
                            "Queue names contain {partition}; use QueueClientFactory.GetPartitionedQueueClient and ForPartition(customerId).");
                    }
                    break;
                }
                case QueueProviderType.AzureStorage:
                {
                    var azure = (AzureQueueProviderConfig)config;
                    if (QueuePartition.ContainsToken(azure.HighPriorityQueue)
                        || QueuePartition.ContainsToken(azure.LowPriorityQueue)
                        || QueuePartition.ContainsToken(azure.UnprocessedQueue))
                    {
                        throw new ArgumentException(
                            "Queue names contain {partition}; use QueueClientFactory.GetPartitionedQueueClient and ForPartition(customerId).");
                    }
                    break;
                }
            }
        }
    }
}
