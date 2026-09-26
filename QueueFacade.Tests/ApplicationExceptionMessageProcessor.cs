// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Queue;

    /// <summary>Processor that throws <see cref="ApplicationException"/> (delete-without-poison path).</summary>
    public class ApplicationExceptionMessageProcessor : AbstractMessageProcessor
    {
        public override Task<bool> Process(Message message)
        {
            throw new ApplicationException("application rejected message");
        }

        public override Task<List<bool>> Process(List<Message> messageList)
        {
            throw new ApplicationException("application rejected batch");
        }
    }
}
