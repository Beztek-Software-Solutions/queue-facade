// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    public interface IQueueProviderConfig
    {
        string Name { get; }

        QueueProviderType QueueProviderType { get; }
    }
}
