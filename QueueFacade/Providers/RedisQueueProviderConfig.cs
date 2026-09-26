// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;

    /// <summary>Configuration for Redis list-backed queues.</summary>
    public class RedisQueueProviderConfig : INamedQueueProviderConfig
    {
        public RedisQueueProviderConfig(
            string name,
            string configuration,
            string highPriorityQueue,
            string lowPriorityQueue = null,
            int visibilityTimeoutMilliseconds = 30000,
            string unprocessedQueue = null)
        {
            NamedQueueConfigValidation.RequireNonEmpty(name, "No name given");
            NamedQueueConfigValidation.RequireNonEmpty(configuration, "No Redis configuration given");
            NamedQueueConfigValidation.RequireNonEmpty(highPriorityQueue, "No queue name provided");
            NamedQueueConfigValidation.ValidatePortableQueueName(highPriorityQueue);
            if (lowPriorityQueue != null)
            {
                NamedQueueConfigValidation.ValidatePortableQueueName(lowPriorityQueue);
            }

            Name = name;
            Configuration = configuration;
            HighPriorityQueue = highPriorityQueue;
            LowPriorityQueue = lowPriorityQueue;
            UnprocessedQueue = NamedQueueConfigValidation.ResolvePoisonQueue(
                highPriorityQueue, lowPriorityQueue, unprocessedQueue, NamedQueueConfigValidation.ValidatePortableQueueName);
            VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
        }

        /// <summary>Provider kind. Overridden by Valkey/Dragonfly subclasses.</summary>
        public virtual QueueProviderType QueueProviderType { get; protected set; } = QueueProviderType.Redis;

        public string Name { get; set; }

        public int VisibilityTimeoutMilliseconds { get; set; }

        /// <summary>StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</summary>
        public string Configuration { get; set; }

        public string HighPriorityQueue { get; set; }

        public string LowPriorityQueue { get; }

        public string UnprocessedQueue { get; }
    }
}
