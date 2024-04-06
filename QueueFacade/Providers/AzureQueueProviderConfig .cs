// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;

    public class AzureQueueProviderConfig : IQueueProviderConfig
    {
        public AzureQueueProviderConfig(string name, string endpoint, string highPriorityQueue, string lowPriorityQueue = null, int visibilityTimeoutMilliseconds = 30000)
        {
            // Name validation
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("No name given");
            }

            // Endpoint validation
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentException("No endpoint given");
            }

            // Validation
            if (string.IsNullOrEmpty(highPriorityQueue))
            {
                throw new ArgumentException("No queue name provided");
            }

            AzureQueueNameValidator.ValidateQueueName(highPriorityQueue);

            if (lowPriorityQueue != null)
            {
                AzureQueueNameValidator.ValidateQueueName(lowPriorityQueue);
            }

            this.Name = name;
            this.Endpoint = endpoint;
            this.HighPriorityQueue = highPriorityQueue;
            this.LowPriorityQueue = lowPriorityQueue;
            this.VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
        }

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.AzureStorage;

        public string Name { get; set; }

        public int VisibilityTimeoutMilliseconds { get; set;}

        public string Endpoint { get; set; }

        public string HighPriorityQueue { get; set; }

        public string LowPriorityQueue { get; }

        internal AzureStorageClientCreator AzureStorageClientCreator { get; set; } = new AzureStorageClientCreator();
    }
}
