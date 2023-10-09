// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Out of box implementation of IQueueProcessorHandler.
    /// </summary>
    public class DefaultProcessorHandler : IQueueProcessorHandler
    {
        /// <summary>
        /// Holds message type to processor mapping.
        /// </summary>
        private readonly Dictionary<string, IMessageProcessor> processors = new Dictionary<string, IMessageProcessor>();

        /// <inheritdoc />
        public IQueueProcessorHandler AddProcessor(Type messageType, IMessageProcessor processor)
        {
            processors.TryAdd(messageType.ToString(), processor);
            return this;
        }

        /// <inheritdoc />
        public async Task<bool> Process(Message message)
        {
            if (processors.TryGetValue(message.MessageType, out IMessageProcessor processor))
            {
                await processor.Process(message).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        public async Task<List<bool>> Process(List<Message> messages)
        {
            if (messages.Count > 0 && (processors.TryGetValue(messages[0].MessageType, out IMessageProcessor processor)))
            {
                return await processor.Process(messages).ConfigureAwait(false);
            }

            return new List<bool>();
        }
    }
}
