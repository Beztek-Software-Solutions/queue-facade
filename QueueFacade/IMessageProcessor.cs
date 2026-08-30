// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Message processor to process Queue Messages.
    /// </summary>
    public interface IMessageProcessor
    {
        /// <summary>
        /// Processes a <see cref="Message"/> which has type information for deserialization.
        /// Throw an exception on failure (other than <see cref="System.ApplicationException"/>, which discards without poison).
        /// </summary>
        /// <param name="message"><see cref="Message"/> with message object and type information.</param>
        /// <returns>True if processed successfully; false to move the message to the poison queue immediately.</returns>
        Task<bool> Process(Message message);

        /// <summary>
        /// Processes a batch of <see cref="Message"/> instances.
        /// </summary>
        /// <param name="messageList">Messages to process.</param>
        /// <returns>Per-message success flags in the same order as <paramref name="messageList"/>.</returns>
        Task<List<bool>> Process(List<Message> messageList);
    }
}
