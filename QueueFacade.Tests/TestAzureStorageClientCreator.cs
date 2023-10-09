// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using Azure.Storage.Queues;
    using Queue.Providers;

    internal class TestAzureStorageClientCreator : AzureStorageClientCreator
    {
        private QueueClient queueClient;

        internal TestAzureStorageClientCreator(QueueClient queueClient)
        {
            this.queueClient = queueClient;
        }

        internal override QueueClient CreateQueueClient(string endPoint, string queueName)
        {
            return queueClient;
        }
    }
}