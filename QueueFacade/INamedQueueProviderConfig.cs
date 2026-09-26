// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    /// <summary>Provider configs that expose primary / optional low / poison queue names.</summary>
    public interface INamedQueueProviderConfig : IQueueProviderConfig
    {
        string HighPriorityQueue { get; }

        string LowPriorityQueue { get; }

        string UnprocessedQueue { get; }
    }
}
