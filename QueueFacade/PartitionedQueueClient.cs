// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;
    using Queue.Providers;

    /// <summary>
    /// Caches <see cref="IQueueClient"/> instances whose queue names embed a resolved partition key.
    /// </summary>
    internal sealed class PartitionedQueueClient : IPartitionedQueueClient
    {
        private readonly IQueueProviderConfig templateConfig;
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<string, IQueueClient> clients =
            new(StringComparer.Ordinal);

        internal PartitionedQueueClient(IQueueProviderConfig templateConfig, ILogger logger = null)
        {
            this.templateConfig = templateConfig ?? throw new ArgumentNullException(nameof(templateConfig));
            this.logger = logger;
            Name = templateConfig.Name;
            ValidateTemplate(templateConfig);
        }

        public string Name { get; }

        public IQueueClient ForPartition(string partitionKey)
        {
            string normalized = QueuePartition.NormalizePartitionKey(partitionKey);
            return clients.GetOrAdd(normalized, key =>
            {
                IQueueProviderConfig resolved = ResolveConfig(templateConfig, key);
                logger?.LogDebug(
                    "Creating partitioned queue client {BaseName} partition={Partition}",
                    Name,
                    key);
                return QueueClientFactory.GetQueueClient(resolved, logger);
            });
        }

        internal static void ValidateTemplate(IQueueProviderConfig config)
        {
            switch (config.QueueProviderType)
            {
                case QueueProviderType.AwsSqs:
                    var sqs = (SqsQueueProviderConfig)config;
                    if (!QueuePartition.ContainsToken(sqs.HighPriorityQueue))
                    {
                        throw new ArgumentException(
                            "Partitioned SQS config highPriorityQueue must include {partition}");
                    }
                    break;
                case QueueProviderType.AzureStorage:
                    var azure = (AzureQueueProviderConfig)config;
                    if (!QueuePartition.ContainsToken(azure.HighPriorityQueue))
                    {
                        throw new ArgumentException(
                            "Partitioned Azure config highPriorityQueue must include {partition}");
                    }
                    break;
                case QueueProviderType.LocalMemory:
                    // Partition is carried in the client Name only.
                    break;
                default:
                    throw new NotSupportedException(
                        $"Unsupported queue provider for partitions: {config.QueueProviderType}");
            }
        }

        private static IQueueProviderConfig ResolveConfig(IQueueProviderConfig template, string partition)
        {
            switch (template.QueueProviderType)
            {
                case QueueProviderType.AwsSqs:
                {
                    var t = (SqsQueueProviderConfig)template;
                    string high = QueuePartition.Resolve(t.HighPriorityQueue, partition);
                    string low = t.LowPriorityQueue == null
                        ? null
                        : QueuePartition.Resolve(t.LowPriorityQueue, partition);
                    string poison = QueuePartition.Resolve(t.UnprocessedQueue, partition);
                    var resolved = new SqsQueueProviderConfig(
                        name: $"{t.Name}:{partition}",
                        region: t.Region,
                        highPriorityQueue: high,
                        lowPriorityQueue: low,
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        serviceUrl: t.ServiceUrl,
                        accessKeyId: t.AccessKeyId,
                        secretAccessKey: t.SecretAccessKey,
                        unprocessedQueue: poison);
                    resolved.SqsClientCreator = t.SqsClientCreator;
                    return resolved;
                }
                case QueueProviderType.AzureStorage:
                {
                    var t = (AzureQueueProviderConfig)template;
                    string high = QueuePartition.Resolve(t.HighPriorityQueue, partition);
                    string low = t.LowPriorityQueue == null
                        ? null
                        : QueuePartition.Resolve(t.LowPriorityQueue, partition);
                    string poison = QueuePartition.Resolve(t.UnprocessedQueue, partition);
                    var resolved = new AzureQueueProviderConfig(
                        name: $"{t.Name}:{partition}",
                        endpoint: t.Endpoint,
                        highPriorityQueue: high,
                        lowPriorityQueue: low,
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        unprocessedQueue: poison);
                    resolved.AzureStorageClientCreator = t.AzureStorageClientCreator;
                    return resolved;
                }
                case QueueProviderType.LocalMemory:
                {
                    var t = (LocalMemoryQueueProviderConfig)template;
                    return new LocalMemoryQueueProviderConfig(
                        name: $"{t.Name}:{partition}",
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds);
                }
                default:
                    throw new NotSupportedException(
                        $"Unsupported queue provider for partitions: {template.QueueProviderType}");
            }
        }
    }
}
