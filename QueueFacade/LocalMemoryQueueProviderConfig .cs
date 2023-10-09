// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;

    public class LocalMemoryQueueProviderConfig : IQueueProviderConfig
    {
        public LocalMemoryQueueProviderConfig(string name)
        {
            // Name validation
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("No name given");
            }

            this.Name = name;
        }

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.LocalMemory;

        public string Name { get; }
    }
}
