// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface defining the back-end requirements for the QueueClient abstraction
    /// </summary>
    internal interface IQueueProvider
    {
        /// <summary>
        /// Create the back-end queue if it does not exist
        /// </summary>
        void CreateIfNotExists();

        /// <summary>
        /// Defines the maximum number of message that can be read with a single polling call.
        /// </summary>
        int MaxMessageCountPerPoll { get; }

        /// <summary>
        /// The maximum allowed message size in bytes
        /// </summary>
        int MaxMessageSize { get; }

        /// <summary>
        /// Flags if this provider has a low priority queue in addition to a high priority queue
        /// </summary>
        bool HasLowPriorityQueue { get; }

        /// <summary>
        /// Allows the queue client to retreive the raw message from the message hook (possibly a wrapper by the back-end provider)
        /// </summary>
        /// <param name="messageHook"></param>
        /// <returns></returns>
        string GetMessageBody(object messageHook);

        /// <summary>
        /// Deletes a message from the back-end
        /// </summary>
        /// <param name="messageHook"></param>
        /// <param name="isHighPriorityQueue"></param>
        /// <returns></returns>
        Task DeleteMessageAsync(object messageHook, bool isHighPriorityQueue);

        /// <summary>
        /// Sends a message to the back-end queue
        /// </summary>
        /// <param name="message"></param>
        /// <param name="useHighPriorityQueue"></param>
        /// <returns></returns>
        Task<bool> SendMessageAsync(string message, bool useHighPriorityQueue);

        /// <summary>
        /// Sends failed messages to an "unprocessed message" queue (poison queue)
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        Task<bool> SendUnprocessedMessageAsync(string message);

        /// <summary>
        /// Retreives up to a maximum of maxMessagesToRetrieve messages. This should never exceed MaxMessageCountPerPoll messages.
        /// </summary>
        /// <param name="maxMessagesToRetrieve"></param>
        /// <param name="isHighPriorityQueue"></param>
        /// <returns></returns>
        IList<object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue);

        /// <summary>
        /// Query the current length of the queue
        /// <paramref name="isHighPriorityQueue"></paramref>
        /// </summary>
        Task<long> GetApproximateQueueLength(bool isHighPriorityQueue);
    }
}