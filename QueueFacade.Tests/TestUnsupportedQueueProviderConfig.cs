// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using Queue;

    public class TestUnsupportedQueueProviderConfig : IQueueProviderConfig
    {
        public TestUnsupportedQueueProviderConfig(string name)
        {
            this.Name = name;
        }

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.None;

        public string Name { get; }
    }
}
