// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    /// <summary>
    /// Configuration for a Valkey-backed queue. Valkey speaks the Redis protocol,
    /// so this reuses <see cref="Providers.RedisQueueProvider"/>.
    /// </summary>
    public class ValkeyQueueProviderConfig : RedisQueueProviderConfig
    {
        /// <param name="name">Logical client name (factory cache key).</param>
        /// <param name="configuration">StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
        /// <param name="highPriorityQueue">Queue name for high-priority messages.</param>
        /// <param name="lowPriorityQueue">Optional low-priority queue name.</param>
        /// <param name="visibilityTimeoutMilliseconds">Visibility timeout for processing-list reclaim.</param>
        /// <param name="unprocessedQueue">Optional poison queue name.</param>
        public ValkeyQueueProviderConfig(
            string name,
            string configuration,
            string highPriorityQueue,
            string lowPriorityQueue = null,
            int visibilityTimeoutMilliseconds = 30000,
            string unprocessedQueue = null)
            : base(name, configuration, highPriorityQueue, lowPriorityQueue, visibilityTimeoutMilliseconds, unprocessedQueue)
        {
            QueueProviderType = QueueProviderType.Valkey;
        }
    }
}
