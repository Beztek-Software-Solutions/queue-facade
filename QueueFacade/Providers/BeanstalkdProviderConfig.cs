// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;

    /// <summary>Configuration for Beanstalkd tubes.</summary>
    public class BeanstalkdProviderConfig : INamedQueueProviderConfig
    {
        public BeanstalkdProviderConfig(
            string name,
            string host,
            int port,
            string highPriorityQueue,
            string lowPriorityQueue = null,
            int visibilityTimeoutMilliseconds = 30000,
            string unprocessedQueue = null)
        {
            NamedQueueConfigValidation.RequireNonEmpty(name, "No name given");
            NamedQueueConfigValidation.RequireNonEmpty(host, "No host given");
            NamedQueueConfigValidation.RequireNonEmpty(highPriorityQueue, "No queue name provided");
            NamedQueueConfigValidation.ValidatePortableQueueName(highPriorityQueue);
            if (lowPriorityQueue != null)
            {
                NamedQueueConfigValidation.ValidatePortableQueueName(lowPriorityQueue);
            }

            if (port <= 0 || port > 65535)
            {
                throw new ArgumentException("Invalid Beanstalkd port");
            }

            Name = name;
            Host = host;
            Port = port;
            HighPriorityQueue = highPriorityQueue;
            LowPriorityQueue = lowPriorityQueue;
            UnprocessedQueue = NamedQueueConfigValidation.ResolvePoisonQueue(
                highPriorityQueue, lowPriorityQueue, unprocessedQueue, NamedQueueConfigValidation.ValidatePortableQueueName);
            VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
        }

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.Beanstalkd;

        public string Name { get; set; }

        public int VisibilityTimeoutMilliseconds { get; set; }

        public string Host { get; set; }

        public int Port { get; set; }

        public string HighPriorityQueue { get; set; }

        public string LowPriorityQueue { get; }

        public string UnprocessedQueue { get; }
    }
}
