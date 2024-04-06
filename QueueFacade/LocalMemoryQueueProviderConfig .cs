// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;

    public class LocalMemoryQueueProviderConfig : IQueueProviderConfig
    {
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

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.LocalMemory;

        public string Name { get; set; }
        public int  VisibilityTimeoutMilliseconds { get; set; }
    }
}
