// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Queue;

    public class TestMessageProcessor : AbstractMessageProcessor
    {
        private int processCount = 0;
        private int processListCount = 0;
        private bool throwException;

        public TestMessageProcessor(bool throwException = false)
        {
            this.throwException = throwException;
        }

        public override Task<bool> Process(Message message)
        {
            if (this.throwException)
            {
                throw new IOException("Exception forcibly thrown");
            }

            Interlocked.Add(ref processCount, 1);
            return Task.FromResult(true);
        }

        public override async Task<List<bool>> Process(List<Message> messageList)
        {
            Interlocked.Add(ref processListCount, 1);
            return await base.Process(messageList).ConfigureAwait(false);
        }

        /// <summary>
        /// Provides the number of messages that were sent for processing
        /// </summary>
        /// <returns></returns>
        public int GetProcessCount()
        {
            return processCount;
        }

        /// <summary>
        /// Provides the number of list messages that were sent for processing
        /// </summary>
        /// <returns></returns>
        public int GetProcessListCount()
        {
            return processListCount;
        }
    }
}
