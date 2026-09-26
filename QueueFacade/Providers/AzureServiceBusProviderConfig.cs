// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;

    /// <summary>Configuration for Azure Service Bus queues.</summary>
    public class AzureServiceBusProviderConfig : INamedQueueProviderConfig
    {
        public AzureServiceBusProviderConfig(
            string name,
            string connectionString,
            string highPriorityQueue,
            string lowPriorityQueue = null,
            int visibilityTimeoutMilliseconds = 30000,
            string unprocessedQueue = null,
            string administrationConnectionString = null)
        {
            NamedQueueConfigValidation.RequireNonEmpty(name, "No name given");
            NamedQueueConfigValidation.RequireNonEmpty(connectionString, "No connection string given");
            NamedQueueConfigValidation.RequireNonEmpty(highPriorityQueue, "No queue name provided");
            NamedQueueConfigValidation.ValidatePortableQueueName(highPriorityQueue);
            if (lowPriorityQueue != null)
            {
                NamedQueueConfigValidation.ValidatePortableQueueName(lowPriorityQueue);
            }

            Name = name;
            ConnectionString = connectionString;
            AdministrationConnectionString = administrationConnectionString;
            HighPriorityQueue = highPriorityQueue;
            LowPriorityQueue = lowPriorityQueue;
            UnprocessedQueue = NamedQueueConfigValidation.ResolvePoisonQueue(
                highPriorityQueue, lowPriorityQueue, unprocessedQueue, NamedQueueConfigValidation.ValidatePortableQueueName);
            VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
        }

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.AzureServiceBus;

        public string Name { get; set; }

        public int VisibilityTimeoutMilliseconds { get; set; }

        /// <summary>Data-plane (AMQP) connection string.</summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Optional admin-plane connection string for <c>ServiceBusAdministrationClient</c>.
        /// Required for the local emulator (HTTP port 5300); omit in Azure where one CS covers both.
        /// </summary>
        public string AdministrationConnectionString { get; set; }

        public string HighPriorityQueue { get; set; }

        public string LowPriorityQueue { get; }

        public string UnprocessedQueue { get; }
    }
}
