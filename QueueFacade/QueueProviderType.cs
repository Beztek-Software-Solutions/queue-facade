// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    /// <summary>Supported backing queue providers for <see cref="IQueueClient"/>.</summary>
    public enum QueueProviderType
    {
        /// <summary>Azure Queue Storage.</summary>
        AzureStorage,

        /// <summary>AWS SQS (standard queues).</summary>
        AwsSqs,

        /// <summary>In-process memory (tests / single instance).</summary>
        LocalMemory,

        /// <summary>Azure Service Bus queues.</summary>
        AzureServiceBus,

        /// <summary>RabbitMQ classic queues.</summary>
        RabbitMq,

        /// <summary>Google Cloud Pub/Sub (pull subscriptions).</summary>
        GooglePubSub,

        /// <summary>Redis lists with processing-list visibility.</summary>
        Redis,

        /// <summary>Valkey (Redis-compatible protocol; reuses Redis queue provider).</summary>
        Valkey,

        /// <summary>Dragonfly (Redis-compatible protocol; reuses Redis queue provider).</summary>
        Dragonfly,

        /// <summary>Apache ActiveMQ (also covers Amazon MQ ActiveMQ engines).</summary>
        ActiveMq,

        /// <summary>Beanstalkd tubes.</summary>
        Beanstalkd,

        /// <summary>Unset / unsupported provider.</summary>
        None
    }
}
