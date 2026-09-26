// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;

    /// <summary>Configuration for RabbitMQ classic queues.</summary>
    public class RabbitMqProviderConfig : INamedQueueProviderConfig
    {
        public RabbitMqProviderConfig(
            string name,
            string amqpUri,
            string highPriorityQueue,
            string lowPriorityQueue = null,
            int visibilityTimeoutMilliseconds = 30000,
            string unprocessedQueue = null)
        {
            NamedQueueConfigValidation.RequireNonEmpty(name, "No name given");
            NamedQueueConfigValidation.RequireNonEmpty(amqpUri, "No AMQP URI given");
            NamedQueueConfigValidation.RequireNonEmpty(highPriorityQueue, "No queue name provided");
            NamedQueueConfigValidation.ValidatePortableQueueName(highPriorityQueue);
            if (lowPriorityQueue != null)
            {
                NamedQueueConfigValidation.ValidatePortableQueueName(lowPriorityQueue);
            }

            Name = name;
            AmqpUri = amqpUri;
            HighPriorityQueue = highPriorityQueue;
            LowPriorityQueue = lowPriorityQueue;
            UnprocessedQueue = NamedQueueConfigValidation.ResolvePoisonQueue(
                highPriorityQueue, lowPriorityQueue, unprocessedQueue, NamedQueueConfigValidation.ValidatePortableQueueName);
            VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
        }

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.RabbitMq;

        public string Name { get; set; }

        public int VisibilityTimeoutMilliseconds { get; set; }

        /// <summary>AMQP URI, e.g. <c>amqp://guest:guest@localhost:5672/</c>.</summary>
        public string AmqpUri { get; set; }

        public string HighPriorityQueue { get; set; }

        public string LowPriorityQueue { get; }

        public string UnprocessedQueue { get; }
    }
}
