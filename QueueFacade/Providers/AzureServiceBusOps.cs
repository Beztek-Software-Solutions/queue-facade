// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using Microsoft.Extensions.Logging;

    [ExcludeFromCodeCoverage]
    internal sealed class AzureServiceBusOps : IAzureServiceBusOps
    {
        private readonly ServiceBusClient client;
        private readonly ServiceBusAdministrationClient admin;
        private readonly ILogger logger;

        internal AzureServiceBusOps(string connectionString, string administrationConnectionString, ILogger logger = null)
        {
            client = new ServiceBusClient(connectionString);
            admin = new ServiceBusAdministrationClient(
                string.IsNullOrEmpty(administrationConnectionString) ? connectionString : administrationConnectionString);
            this.logger = logger;
        }

        public async Task EnsureQueueAsync(string queueName, TimeSpan lockDuration)
        {
            if (await admin.QueueExistsAsync(queueName).ConfigureAwait(false))
            {
                return;
            }

            await admin.CreateQueueAsync(new CreateQueueOptions(queueName)
            {
                LockDuration = lockDuration,
                MaxDeliveryCount = 10
            }).ConfigureAwait(false);
            logger?.LogDebug("Service Bus queue ensured: {Queue}", queueName);
        }

        public async Task SendAsync(string queue, string body)
        {
            await using ServiceBusSender sender = client.CreateSender(queue);
            await sender.SendMessageAsync(new ServiceBusMessage(body)).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveAsync(string queue, int max, TimeSpan wait)
        {
            await using ServiceBusReceiver receiver = client.CreateReceiver(queue, new ServiceBusReceiverOptions
            {
                PrefetchCount = 0,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });
            return await receiver.ReceiveMessagesAsync(max, wait).ConfigureAwait(false)
                ?? Array.Empty<ServiceBusReceivedMessage>();
        }

        public async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekAsync(string queue, int max)
        {
            await using ServiceBusReceiver receiver = client.CreateReceiver(queue);
            return await receiver.PeekMessagesAsync(max).ConfigureAwait(false)
                ?? Array.Empty<ServiceBusReceivedMessage>();
        }

        public async Task CompleteAsync(string queue, ServiceBusReceivedMessage message)
        {
            await using ServiceBusReceiver receiver = client.CreateReceiver(queue);
            await receiver.CompleteMessageAsync(message).ConfigureAwait(false);
        }

        public async Task<long> GetActiveMessageCountAsync(string queue)
        {
            QueueRuntimeProperties props = await admin.GetQueueRuntimePropertiesAsync(queue).ConfigureAwait(false);
            return props.ActiveMessageCount;
        }
    }
}
