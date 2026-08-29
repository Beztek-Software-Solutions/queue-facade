// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Queue;

    /// <summary>
    /// Processor that always throws (or returns false) for retry/poison tests.
    /// </summary>
    public class ThrowingMessageProcessor : AbstractMessageProcessor
    {
        private readonly bool returnFalseInsteadOfThrow;
        private int processCount;

        public ThrowingMessageProcessor(bool returnFalseInsteadOfThrow = false)
        {
            this.returnFalseInsteadOfThrow = returnFalseInsteadOfThrow;
        }

        public int ProcessCount => this.processCount;

        public override Task<bool> Process(Message message)
        {
            Interlocked.Increment(ref this.processCount);
            if (this.returnFalseInsteadOfThrow)
            {
                return Task.FromResult(false);
            }

            throw new InvalidOperationException("forced processing failure");
        }

        public override Task<List<bool>> Process(List<Message> messageList)
        {
            Interlocked.Increment(ref this.processCount);
            if (this.returnFalseInsteadOfThrow)
            {
                List<bool> results = new List<bool>();
                foreach (Message _ in messageList)
                {
                    results.Add(false);
                }

                return Task.FromResult(results);
            }

            throw new InvalidOperationException("forced batch processing failure");
        }
    }
}
