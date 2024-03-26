// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A Queue Client for producing or consuming messages from a backend queue facade.
    /// </summary>
    public interface IQueueClient
    {
        /// <summary>
        /// Receive messages from queue and pass to <see cref="processor"/>
        /// </summary>
        /// <param name="maxMessageRate">The number of messages per second.</param>
        /// <param name="maxAsynchronousProcesses">The max allowed asynchronous processes being processed</param>
        /// <param name="processor"><seealso cref="IMessageProcessor"/> to process messages.</param>
        public Task DequeueAndProcess(int maxMessageRate, int maxAsynchronousProcesses, IMessageProcessor processor, CancellationToken cancellationToken, int batchSize = 1, int pollIntervalInMilliseconds = 1000);

        /// <summary>
        /// Receive messages from queue and pass to processors in the <see cref="handler"/>
        /// </summary>
        /// <param name="maxMessageRate">The number of messages per second.</param>
        /// <param name="maxAsynchronousProcesses">The max allowed asynchronous processes being processed</param>
        /// <param name="handler">Handler to allow processors to process messages.</param>
        public Task DequeueAndProcess(int maxMessageRate, int maxAsynchronousProcesses, IQueueProcessorHandler handler, CancellationToken cancellationToken, int batchSize = 1, int pollIntervalInMilliseconds = 1000);

        /// <summary>
        /// Adds a message of type <see cref="T"/> to queue.
        /// </summary>
        /// <typeparam name="T">Type of message.</typeparam>
        /// <param name="message">Message object.</param>
        /// <param name="useHighPriorityQueue">Set true if high priority queue is to be used.</param>
        /// <param name="activityId">ActivityId for distributed log tracing.</param>
        /// <returns>True if message is queued; False otherwise.</returns>
        public Task<bool> Enqueue<T>(T message, bool useHighPriorityQueue, string activityId = null);

        /// <summary>
        /// Takes in a list of messages and posts each of them individually, returning
        /// a corresponding list of flags designating success for each message.
        /// </summary>
        /// <param name="messages">List of messages to be enqueued</param>
        /// <param name="useHighPriorityQueue"></param>
        /// <param name="activityId">ActivityId for distributed log tracing.</param>
        /// <returns>List of flags designating success.</returns>
        public Task<IList<bool>> Enqueue<T>(IList<T> messages, bool useHighPriorityQueue, string activityId = null);

        /// <summary>
        /// Takes a large list objects of type <see cref="T"/>, and posts them with a small
        /// number of queue messages, each within the queue message size limit. Thus a list
        /// containing 200 objects, may get posted with (say) 4 messages with 50 objects each,
        /// ensuring that each of these messages are within the queue message size limit
        /// </summary>
        /// <typeparam name="T">Type of message.</typeparam>
        /// <param name="messages">List of messages to be enqueued.</param>
        /// <param name="useHighPriorityQueue">Set true if high priority queue is to be used.</param>
        /// <param name="activityId">ActivityId for distributed log tracing.</param>
        /// <returns>List of objects that failed to get enqueued. This list will be empty if all get posted successfully.</returns>
        public Task<List<T>> EnqueueBatchedMessages<T>(List<T> messages, bool useHighPriorityQueue, string activityId = null);

        /// <summary>
        /// Blocking method to stop Dequeuing.
        /// </summary>
        public Task<bool> StopDequeuing();

        /// <summary>
        /// Query the approximate current length of the queue. It may send a slightly larger-than-actual value
        /// <paramref name="isHighPriorityQueue"></paramref>
        /// </summary>
        public Task<long> GetApproximateQueueLength(bool isHighPriorityQueue);
    }
}