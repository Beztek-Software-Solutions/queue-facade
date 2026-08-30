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

        /// <summary>Unset / unsupported provider.</summary>
        None
    }
}
