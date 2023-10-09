// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using Azure.Storage.Queues;

    internal class AzureStorageClientCreator
    {
        internal virtual QueueClient CreateQueueClient(string queueName, string endPoint)
        {
            QueueClient qclient = null;
            try
            {
                qclient = new QueueClient(endPoint, queueName);
            }
            catch
            {
                throw new ArgumentException($"Invalid combination of the endpoint and queueName: Endpoint {endPoint} - Queue Name: {queueName}");
            }

            return qclient;
        }
    }
}
