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
            if (config.QueueProviderType == QueueProviderType.LocalMemory)
            {
                return;
            }

            if (!TryGetHighPriorityQueue(config, out string high))
            {
                throw new NotSupportedException(
                    $"Unsupported queue provider for partitions: {config.QueueProviderType}");
            }

            if (!QueuePartition.ContainsToken(high))
            {
                throw new ArgumentException(
                    $"Partitioned {config.QueueProviderType} config highPriorityQueue must include {{partition}}");
            }
        }

        internal static IQueueProviderConfig ResolveConfig(IQueueProviderConfig template, string partition)
        {
            string name = $"{template.Name}:{partition}";
            switch (template.QueueProviderType)
            {
                case QueueProviderType.AwsSqs:
                {
                    var t = (SqsQueueProviderConfig)template;
                    var resolved = new SqsQueueProviderConfig(
                        name: name,
                        region: t.Region,
                        highPriorityQueue: QueuePartition.Resolve(t.HighPriorityQueue, partition),
                        lowPriorityQueue: ResolveOptional(t.LowPriorityQueue, partition),
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        serviceUrl: t.ServiceUrl,
                        accessKeyId: t.AccessKeyId,
                        secretAccessKey: t.SecretAccessKey,
                        unprocessedQueue: QueuePartition.Resolve(t.UnprocessedQueue, partition),
                        sessionToken: t.SessionToken);
                    resolved.SqsClientCreator = t.SqsClientCreator;
                    return resolved;
                }
                case QueueProviderType.AzureStorage:
                {
                    var t = (AzureQueueProviderConfig)template;
                    var resolved = new AzureQueueProviderConfig(
                        name: name,
                        endpoint: t.Endpoint,
                        highPriorityQueue: QueuePartition.Resolve(t.HighPriorityQueue, partition),
                        lowPriorityQueue: ResolveOptional(t.LowPriorityQueue, partition),
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        unprocessedQueue: QueuePartition.Resolve(t.UnprocessedQueue, partition));
                    resolved.AzureStorageClientCreator = t.AzureStorageClientCreator;
                    return resolved;
                }
                case QueueProviderType.LocalMemory:
                {
                    var t = (LocalMemoryQueueProviderConfig)template;
                    return new LocalMemoryQueueProviderConfig(
                        name: name,
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds);
                }
                case QueueProviderType.AzureServiceBus:
                {
                    var t = (AzureServiceBusProviderConfig)template;
                    return new AzureServiceBusProviderConfig(
                        name: name,
                        connectionString: t.ConnectionString,
                        highPriorityQueue: QueuePartition.Resolve(t.HighPriorityQueue, partition),
                        lowPriorityQueue: ResolveOptional(t.LowPriorityQueue, partition),
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        unprocessedQueue: QueuePartition.Resolve(t.UnprocessedQueue, partition),
                        administrationConnectionString: t.AdministrationConnectionString);
                }
                case QueueProviderType.RabbitMq:
                {
                    var t = (RabbitMqProviderConfig)template;
                    return new RabbitMqProviderConfig(
                        name: name,
                        amqpUri: t.AmqpUri,
                        highPriorityQueue: QueuePartition.Resolve(t.HighPriorityQueue, partition),
                        lowPriorityQueue: ResolveOptional(t.LowPriorityQueue, partition),
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        unprocessedQueue: QueuePartition.Resolve(t.UnprocessedQueue, partition));
                }
                case QueueProviderType.GooglePubSub:
                {
                    var t = (GooglePubSubProviderConfig)template;
                    return new GooglePubSubProviderConfig(
                        name: name,
                        projectId: t.ProjectId,
                        highPriorityQueue: QueuePartition.Resolve(t.HighPriorityQueue, partition),
                        lowPriorityQueue: ResolveOptional(t.LowPriorityQueue, partition),
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        unprocessedQueue: QueuePartition.Resolve(t.UnprocessedQueue, partition),
                        emulatorHost: t.EmulatorHost);
                }
                case QueueProviderType.Redis:
                {
                    var t = (RedisQueueProviderConfig)template;
                    return new RedisQueueProviderConfig(
                        name: name,
                        configuration: t.Configuration,
                        highPriorityQueue: QueuePartition.Resolve(t.HighPriorityQueue, partition),
                        lowPriorityQueue: ResolveOptional(t.LowPriorityQueue, partition),
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        unprocessedQueue: QueuePartition.Resolve(t.UnprocessedQueue, partition));
                }
                case QueueProviderType.Valkey:
                {
                    var t = (ValkeyQueueProviderConfig)template;
                    return new ValkeyQueueProviderConfig(
                        name: name,
                        configuration: t.Configuration,
                        highPriorityQueue: QueuePartition.Resolve(t.HighPriorityQueue, partition),
                        lowPriorityQueue: ResolveOptional(t.LowPriorityQueue, partition),
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        unprocessedQueue: QueuePartition.Resolve(t.UnprocessedQueue, partition));
                }
                case QueueProviderType.Dragonfly:
                {
                    var t = (DragonflyQueueProviderConfig)template;
                    return new DragonflyQueueProviderConfig(
                        name: name,
                        configuration: t.Configuration,
                        highPriorityQueue: QueuePartition.Resolve(t.HighPriorityQueue, partition),
                        lowPriorityQueue: ResolveOptional(t.LowPriorityQueue, partition),
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        unprocessedQueue: QueuePartition.Resolve(t.UnprocessedQueue, partition));
                }
                case QueueProviderType.ActiveMq:
                {
                    var t = (ActiveMqProviderConfig)template;
                    return new ActiveMqProviderConfig(
                        name: name,
                        brokerUri: t.BrokerUri,
                        highPriorityQueue: QueuePartition.Resolve(t.HighPriorityQueue, partition),
                        lowPriorityQueue: ResolveOptional(t.LowPriorityQueue, partition),
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        unprocessedQueue: QueuePartition.Resolve(t.UnprocessedQueue, partition),
                        userName: t.UserName,
                        password: t.Password);
                }
                case QueueProviderType.Beanstalkd:
                {
                    var t = (BeanstalkdProviderConfig)template;
                    return new BeanstalkdProviderConfig(
                        name: name,
                        host: t.Host,
                        port: t.Port,
                        highPriorityQueue: QueuePartition.Resolve(t.HighPriorityQueue, partition),
                        lowPriorityQueue: ResolveOptional(t.LowPriorityQueue, partition),
                        visibilityTimeoutMilliseconds: t.VisibilityTimeoutMilliseconds,
                        unprocessedQueue: QueuePartition.Resolve(t.UnprocessedQueue, partition));
                }
                default:
                    throw new NotSupportedException(
                        $"Unsupported queue provider for partitions: {template.QueueProviderType}");
            }
        }

        private static string ResolveOptional(string template, string partition) =>
            template == null ? null : QueuePartition.Resolve(template, partition);

        internal static bool TryGetHighPriorityQueue(IQueueProviderConfig config, out string high)
        {
            high = config is INamedQueueProviderConfig named ? named.HighPriorityQueue : null;
            return high != null;
        }
    }
}
