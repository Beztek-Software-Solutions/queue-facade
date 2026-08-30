// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler to combine multiple <see cref="IMessageProcessor"/> instances for different message types.
    /// </summary>
    public interface IQueueProcessorHandler
    {
        /// <summary>
        /// Processes a single <see cref="Message"/>.
        /// </summary>
        /// <param name="message">Message to process.</param>
        /// <returns>True if processed successfully by at least one of the processors.</returns>
        Task<bool> Process(Message message);

        /// <summary>
        /// Processes a batch of <see cref="Message"/> instances.
        /// </summary>
        /// <param name="messages">Messages to process.</param>
        /// <returns>Per-message success flags.</returns>
        Task<List<bool>> Process(List<Message> messages);

        /// <summary>
        /// Adds an <see cref="IMessageProcessor"/> that can handle messages of <paramref name="messageType"/>.
        /// </summary>
        /// <param name="messageType">Type of message payload.</param>
        /// <param name="processor">Message processor.</param>
        /// <returns>This handler for chaining.</returns>
        IQueueProcessorHandler AddProcessor(Type messageType, IMessageProcessor processor);

        /// <summary>
        /// Default implementation of <see cref="IQueueProcessorHandler"/>.
        /// </summary>
        /// <returns>A new <see cref="DefaultProcessorHandler"/>.</returns>
        static IQueueProcessorHandler Default() => new DefaultProcessorHandler();
    }
}
