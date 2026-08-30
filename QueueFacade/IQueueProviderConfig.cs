// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    /// <summary>
    /// Configuration marker for a queue provider; passed to <see cref="QueueClientFactory.GetQueueClient"/>.
    /// </summary>
    public interface IQueueProviderConfig
    {
        /// <summary>Logical client name (factory cache key).</summary>
        string Name { get; }

        /// <summary>Visibility timeout applied on receive, in milliseconds.</summary>
        int VisibilityTimeoutMilliseconds { get; }

        /// <summary>Provider kind.</summary>
        QueueProviderType QueueProviderType { get; }
    }
}
