// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;

    /// <summary>
    /// Configuration for the in-process LocalMemory queue provider (tests / single instance).
    /// </summary>
    public class LocalMemoryQueueProviderConfig : IQueueProviderConfig
    {
        /// <summary>
        /// Creates a LocalMemory provider configuration.
        /// </summary>
        /// <param name="name">Logical client name (factory cache key).</param>
        /// <param name="visibilityTimeoutMilliseconds">Visibility timeout for received messages.</param>
        public LocalMemoryQueueProviderConfig(string name, int visibilityTimeoutMilliseconds = 30000)
        {
            // Name validation
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("No name given");
            }

            this.Name = name;
            this.VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
        }

        /// <inheritdoc />
        public QueueProviderType QueueProviderType { get; } = QueueProviderType.LocalMemory;

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public int VisibilityTimeoutMilliseconds { get; set; }
    }
}
