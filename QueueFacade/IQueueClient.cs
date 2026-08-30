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
        /// Returns the name of this queue client.
        /// </summary>
        string GetName();

        /// <summary>
        /// Receive messages from the queue and pass them to <paramref name="processor"/>.
        /// </summary>
        /// <param name="maxMessageRate">The number of messages per second.</param>
        /// <param name="maxAsynchronousProcesses">The max allowed asynchronous processes being processed.</param>
        /// <param name="processor"><see cref="IMessageProcessor"/> to process messages.</param>
        /// <param name="cancellationToken">Stops the dequeue loop when cancelled.</param>
        /// <param name="batchSize">Messages per process batch.</param>
        /// <param name="pollIntervalInMilliseconds">Idle poll interval when the queue is empty.</param>
        /// <param name="maxProcessingAttempts">After this many receives, still-failing messages move to the poison queue.</param>
        Task DequeueAndProcess(int maxMessageRate, int maxAsynchronousProcesses, IMessageProcessor processor, CancellationToken cancellationToken, int batchSize = 1, int pollIntervalInMilliseconds = 1000, int maxProcessingAttempts = QueueDequeueConfig.DefaultMaxProcessingAttempts);

        /// <summary>
        /// Receive messages from the queue and pass them to processors in <paramref name="handler"/>.
        /// </summary>
        /// <param name="maxMessageRate">The number of messages per second.</param>
        /// <param name="maxAsynchronousProcesses">The max allowed asynchronous processes being processed.</param>
        /// <param name="handler">Handler that routes messages to typed processors.</param>
        /// <param name="cancellationToken">Stops the dequeue loop when cancelled.</param>
        /// <param name="batchSize">Messages per process batch.</param>
        /// <param name="pollIntervalInMilliseconds">Idle poll interval when the queue is empty.</param>
        /// <param name="maxProcessingAttempts">After this many receives, still-failing messages move to the poison queue.</param>
        Task DequeueAndProcess(int maxMessageRate, int maxAsynchronousProcesses, IQueueProcessorHandler handler, CancellationToken cancellationToken, int batchSize = 1, int pollIntervalInMilliseconds = 1000, int maxProcessingAttempts = QueueDequeueConfig.DefaultMaxProcessingAttempts);

        /// <summary>
        /// Adds a message of type <typeparamref name="T"/> to the queue.
        /// </summary>
        /// <typeparam name="T">Type of message.</typeparam>
        /// <param name="message">Message object.</param>
        /// <param name="useHighPriorityQueue">Set true if high priority queue is to be used.</param>
        /// <param name="activityId">ActivityId for distributed log tracing.</param>
        /// <returns>True if message is queued; False otherwise.</returns>
        Task<bool> Enqueue<T>(T message, bool useHighPriorityQueue, string activityId = null);

        /// <summary>
        /// Takes in a list of messages and posts each of them individually, returning
        /// a corresponding list of flags designating success for each message.
        /// </summary>
        /// <typeparam name="T">Type of message.</typeparam>
        /// <param name="messages">List of messages to be enqueued.</param>
        /// <param name="useHighPriorityQueue">Set true if high priority queue is to be used.</param>
        /// <param name="activityId">ActivityId for distributed log tracing.</param>
        /// <returns>List of flags designating success.</returns>
        Task<IList<bool>> Enqueue<T>(IList<T> messages, bool useHighPriorityQueue, string activityId = null);

        /// <summary>
        /// Takes a large list of objects of type <typeparamref name="T"/>, and posts them with a small
        /// number of queue messages, each within the queue message size limit. Thus a list
        /// containing 200 objects may get posted with (say) 4 messages with 50 objects each,
        /// ensuring that each of these messages are within the queue message size limit.
        /// </summary>
        /// <typeparam name="T">Type of message.</typeparam>
        /// <param name="messages">List of messages to be enqueued.</param>
        /// <param name="useHighPriorityQueue">Set true if high priority queue is to be used.</param>
        /// <param name="activityId">ActivityId for distributed log tracing.</param>
        /// <returns>List of objects that failed to get enqueued. This list will be empty if all get posted successfully.</returns>
        Task<List<T>> EnqueueBatchedMessages<T>(List<T> messages, bool useHighPriorityQueue, string activityId = null);

        /// <summary>
        /// Blocking method to stop dequeuing.
        /// </summary>
        Task<bool> StopDequeuing();

        /// <summary>
        /// Query the approximate current length of the queue. It may return a slightly larger-than-actual value.
        /// </summary>
        /// <param name="isHighPriorityQueue">True for the high-priority queue; false for low-priority.</param>
        Task<long> GetApproximateQueueLength(bool isHighPriorityQueue);

        /// <summary>
        /// Approximate depth of the unprocessed/poison queue.
        /// </summary>
        Task<long> GetApproximateUnprocessedQueueLength();

        /// <summary>
        /// Peek messages on the unprocessed/poison queue for troubleshooting (non-destructive on Azure;
        /// SQS receives with visibility timeout).
        /// </summary>
        /// <param name="maxMessages">Maximum number of messages to peek.</param>
        Task<IReadOnlyList<Message>> PeekUnprocessedMessagesAsync(int maxMessages = 32);

        /// <summary>
        /// Move up to <paramref name="maxMessages"/> poison/unprocessed messages back onto the primary queue
        /// so they can be processed again (receive-count / attempts reset on the new enqueue).
        /// </summary>
        /// <param name="maxMessages">Maximum number of poison messages to requeue.</param>
        /// <param name="useHighPriorityQueue">When true, requeue onto the high-priority queue.</param>
        /// <returns>Number of messages successfully requeued.</returns>
        Task<int> RequeueUnprocessedMessagesAsync(int maxMessages = 32, bool useHighPriorityQueue = true);
    }
}
