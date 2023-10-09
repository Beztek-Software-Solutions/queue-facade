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
        /// Processes <see cref="Message"/> which has Type information for deserialization. It should throw an exception if failure
        /// </summary>
        /// <param name="message"><seealso cref="Message"/> with message object and type information.</param>
        public Task<bool> Process(Message message);

        public Task<List<bool>> Process(List<Message> messageList);
    }
}
