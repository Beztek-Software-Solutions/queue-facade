// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    /// <summary>
    /// Resolves a <see cref="IQueueClient"/> per tenant/customer partition
    /// (separate cloud queues when names include <c>{partition}</c>).
    /// </summary>
    public interface IPartitionedQueueClient
    {
        /// <summary>Logical base name from the template config.</summary>
        string Name { get; }

        /// <summary>
        /// Returns a cached queue client for this partition (e.g. customer id).
        /// Creates underlying queues on first use via the provider's CreateIfNotExists.
        /// </summary>
        IQueueClient ForPartition(string partitionKey);
    }
}
