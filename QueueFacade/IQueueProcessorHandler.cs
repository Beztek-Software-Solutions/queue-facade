// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler to combine multiple IMessageProcessor(s) to process different Type of messages.
    /// </summary>
    public interface IQueueProcessorHandler
    {
        /// <summary>
        /// Processes an <see cref="Message"/>.
        /// </summary>
        /// <param name="message"></param>
        /// <returns>True if processed successfully by at least one of the Processors.</returns>
        Task<bool> Process(Message message);

        Task<List<bool>> Process(List<Message> messages);

        /// <summary>
        /// Adds a <see cref="IMessageProcessor"/> that can handle <see cref="messageType"/> of message objects.
        /// </summary>
        /// <param name="messageType">Type of message</param>
        /// <param name="processor">Message processor</param>
        /// <returns></returns>
        IQueueProcessorHandler AddProcessor(Type messageType, IMessageProcessor processor);

        /// <summary>
        /// Default implementation of <see cref="IQueueProcessorHandler"/>
        /// </summary>
        /// <returns>IQueueProcessorHandler</returns>
        static IQueueProcessorHandler Default() => new DefaultProcessorHandler();
    }
}