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
        /// Queue names must be fully resolved (no <c>{partition}</c>); use
        /// <see cref="GetPartitionedQueueClient"/> for multi-tenant templates.
        /// </summary>
        public static IQueueClient GetQueueClient(IQueueProviderConfig queueProviderConfig, ILogger logger = null)
        {
            RejectUnresolvedPartitionTemplates(queueProviderConfig);

            string key = $"{queueProviderConfig.Name}:{queueProviderConfig.QueueProviderType}";
            if (!queueClientMap.TryGetValue(key, out IQueueClient result))
            {
                IQueueProvider queueProvider = CreateProvider(queueProviderConfig, logger);
                result = new QueueClient(queueProviderConfig.Name, queueProvider, logger);
                queueClientMap.GetOrAdd(key, result);
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

            PartitionedQueueClient.ValidateTemplate(templateConfig);

            string key = $"partitioned:{templateConfig.Name}:{templateConfig.QueueProviderType}";
            return partitionedClientMap.GetOrAdd(
                key,
                _ => new PartitionedQueueClient(templateConfig, logger));
        }

        private static IQueueProvider CreateProvider(IQueueProviderConfig queueProviderConfig, ILogger logger)
        {
            switch (queueProviderConfig.QueueProviderType)
            {
                case QueueProviderType.AzureStorage:
                    return new AzureQueueProvider((AzureQueueProviderConfig)queueProviderConfig, logger);
                case QueueProviderType.AwsSqs:
                    return new SqsQueueProvider((SqsQueueProviderConfig)queueProviderConfig, logger);
                case QueueProviderType.LocalMemory:
                    return new LocalMemoryQueueProvider(logger, true, queueProviderConfig.VisibilityTimeoutMilliseconds);
                case QueueProviderType.AzureServiceBus:
                    return new AzureServiceBusProvider((AzureServiceBusProviderConfig)queueProviderConfig, logger);
                case QueueProviderType.RabbitMq:
                    return new RabbitMqProvider((RabbitMqProviderConfig)queueProviderConfig, logger);
                case QueueProviderType.GooglePubSub:
                    return new GooglePubSubProvider((GooglePubSubProviderConfig)queueProviderConfig, logger);
                case QueueProviderType.Redis:
                case QueueProviderType.Valkey:
                case QueueProviderType.Dragonfly:
                    return new RedisQueueProvider((RedisQueueProviderConfig)queueProviderConfig, logger);
                case QueueProviderType.ActiveMq:
                    return new ActiveMqProvider((ActiveMqProviderConfig)queueProviderConfig, logger);
                case QueueProviderType.Beanstalkd:
                    return new BeanstalkdProvider((BeanstalkdProviderConfig)queueProviderConfig, logger);
                default:
                    logger?.LogError($"Unknown Queue Provider: {queueProviderConfig.QueueProviderType}");
                    throw new NotSupportedException($"Unsupported Queue Provider: {queueProviderConfig.QueueProviderType}");
            }
        }

        private static void RejectUnresolvedPartitionTemplates(IQueueProviderConfig config)
        {
            if (!TryGetPrimaryQueueNames(config, out string high, out string low, out string poison))
            {
                return;
            }

            if (AnyContainsPartitionToken(high, low, poison))
            {
                throw new ArgumentException(
                    "Queue names contain {partition}; use QueueClientFactory.GetPartitionedQueueClient and ForPartition(customerId).");
            }
        }

        internal static bool TryGetPrimaryQueueNames(
            IQueueProviderConfig config,
            out string high,
            out string low,
            out string poison)
        {
            if (config is INamedQueueProviderConfig named)
            {
                high = named.HighPriorityQueue;
                low = named.LowPriorityQueue;
                poison = named.UnprocessedQueue;
                return true;
            }

            high = null;
            low = null;
            poison = null;
            return false;
        }

        private static bool AnyContainsPartitionToken(string high, string low, string poison) =>
            QueuePartition.ContainsToken(high)
            || QueuePartition.ContainsToken(low)
            || QueuePartition.ContainsToken(poison);
    }
}
