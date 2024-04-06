// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;
    using Queue.Providers;

    /// <summary>
    /// QueueClientFactory.
    /// </summary>
    public static class QueueClientFactory
    {
        private static readonly ConcurrentDictionary<string, IQueueClient> queueClientMap = new ConcurrentDictionary<string, IQueueClient>();

        /// <summary>
        /// Gets an instance of Queue Client based on the provider config provided. Currently this supports Azure, and LocalMemory (for testing purposes)
        /// </summary>
        /// <param name="queueProviderConfig"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static IQueueClient GetQueueClient(IQueueProviderConfig queueProviderConfig, ILogger logger = null)
        {
            string key = $"{queueProviderConfig.Name}:{queueProviderConfig.QueueProviderType}";
            if (!queueClientMap.TryGetValue(key, out IQueueClient result))
            {
                switch (queueProviderConfig.QueueProviderType)
                {
                    case QueueProviderType.AzureStorage:
                        AzureQueueProviderConfig config = (AzureQueueProviderConfig)queueProviderConfig;
                        IQueueProvider queueProvider = new AzureQueueProvider(config, logger);
                        result = new QueueClient(queueProvider, logger);
                        queueClientMap.GetOrAdd(key, result);  // Returns the new value, or the existing value if the key exists.
                        break;
                    case QueueProviderType.LocalMemory:
                        queueProvider = new LocalMemoryQueueProvider(logger, true, queueProviderConfig.VisibilityTimeoutMilliseconds);
                        result = new QueueClient(queueProvider, logger);
                        queueClientMap.GetOrAdd(key, result);  // Returns the new value, or the existing value if the key exists.
                        break;
                    default:
                        logger?.LogError($"Unknown Queue Provider: {queueProviderConfig.QueueProviderType}");
                        throw new System.NotSupportedException($"Unsupported Queue Provider: {queueProviderConfig.QueueProviderType}");
                }
            }
            return result;
        }
    }
}