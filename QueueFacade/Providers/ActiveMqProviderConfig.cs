// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;

    /// <summary>Configuration for Apache ActiveMQ (including Amazon MQ ActiveMQ engines).</summary>
    public class ActiveMqProviderConfig : INamedQueueProviderConfig
    {
        public ActiveMqProviderConfig(
            string name,
            string brokerUri,
            string highPriorityQueue,
            string lowPriorityQueue = null,
            int visibilityTimeoutMilliseconds = 30000,
            string unprocessedQueue = null,
            string userName = "admin",
            string password = "admin")
        {
            NamedQueueConfigValidation.RequireNonEmpty(name, "No name given");
            NamedQueueConfigValidation.RequireNonEmpty(brokerUri, "No broker URI given");
            NamedQueueConfigValidation.RequireNonEmpty(highPriorityQueue, "No queue name provided");
            NamedQueueConfigValidation.ValidatePortableQueueName(highPriorityQueue);
            if (lowPriorityQueue != null)
            {
                NamedQueueConfigValidation.ValidatePortableQueueName(lowPriorityQueue);
            }

            Name = name;
            BrokerUri = brokerUri;
            HighPriorityQueue = highPriorityQueue;
            LowPriorityQueue = lowPriorityQueue;
            UnprocessedQueue = NamedQueueConfigValidation.ResolvePoisonQueue(
                highPriorityQueue, lowPriorityQueue, unprocessedQueue, NamedQueueConfigValidation.ValidatePortableQueueName);
            VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
            UserName = userName;
            Password = password;
        }

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.ActiveMq;

        public string Name { get; set; }

        public int VisibilityTimeoutMilliseconds { get; set; }

        /// <summary>NMS broker URI, e.g. <c>tcp://localhost:61616</c>.</summary>
        public string BrokerUri { get; set; }

        public string HighPriorityQueue { get; set; }

        public string LowPriorityQueue { get; }

        public string UnprocessedQueue { get; }

        public string UserName { get; set; }

        public string Password { get; set; }
    }
}
