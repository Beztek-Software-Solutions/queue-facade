// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    /// <summary>Service Bus transport surface (mockable in unit tests).</summary>
    internal interface IAzureServiceBusOps
    {
        Task EnsureQueueAsync(string queueName, TimeSpan lockDuration);

        Task SendAsync(string queue, string body);

        Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveAsync(string queue, int max, TimeSpan wait);

        Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekAsync(string queue, int max);

        Task CompleteAsync(string queue, ServiceBusReceivedMessage message);

        Task<long> GetActiveMessageCountAsync(string queue);
    }
}
