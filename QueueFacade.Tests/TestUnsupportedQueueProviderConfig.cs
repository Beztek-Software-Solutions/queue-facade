// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using Queue;

    public class TestUnsupportedQueueProviderConfig : IQueueProviderConfig
    {
        public TestUnsupportedQueueProviderConfig(string name, int visibilityTimeoutMilliseconds=30000)
        {
            this.Name = name;
            this.VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
        }

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.None;

        public string Name { get; }

        public int VisibilityTimeoutMilliseconds { get;}
    }
}
