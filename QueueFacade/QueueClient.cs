// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Queue.Providers;

    /// <summary>
    /// Abstract base implementation of Queue Client.
    /// </summary>
    public class QueueClient : IQueueClient
    {
        // We use constants to avoid "magic strings"
        private const int OneThousand = 1000;
        private const int Zero = 0;
        private const int One = 1;
        private const int MaxSendMessageRetryCount = 3;

        private readonly ILogger logger;

        // Flags if polling should continue or should stop
        private int pollingFlag = Zero;

        // Count of current active processes
        private int currActiveProcesses = 0;

        // Flags if polling is currently active. When pollingFlag is set false, this should soon become false as well.
        private bool isPolling;

        internal IQueueProvider queueProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureQueueProvider"/> class.
        /// We are using nullable parameters to avoid extra constructors. There is a use case in batching where they need to use
        /// a low priority queue for large batches and a high priority queue for single batches. We want this library to be extensible for that use case.
        /// The Queue Name needs to be less than 25 characters, no spaces, only alphanumeric to begin and start.
        /// Note: For those unfamiliar with C#, an optional parameter means one can use multiple constructors for multiple scenarios; you literally don't have to supply the optional parameter.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="highPriorityQueue"></param>
        /// <param name="lowPriorityQueue"></param>
        internal QueueClient(IQueueProvider queueProvider, ILogger logger = null)
        {
            this.queueProvider = queueProvider;
            this.logger = logger;
        }

        // When we stop dequeueing, we should be able to restart dequeing, so we will need to know that was set
        public QueueDequeueConfig QueueDequeueConfig { get; set; }

        /// <inheritdoc />
        public virtual async Task<IList<bool>> Enqueue<T>(IList<T> messages, bool useHighPriorityQueue, string activityId = null)
        {
            queueProvider.CreateIfNotExists();

            // First enqueue all the messages asynchronously
            IList<Task<bool>> asyncResponses = messages.Select(message => Enqueue(message, useHighPriorityQueue, activityId)).ToList();

            // Then save the corresponding results after waiting for the responses.
            IList<bool> results = new List<bool>();
            int numErrors = 0;
            for (int index = 0; index < asyncResponses.Count; index++)
            {
                Task<bool> asyncResponse = asyncResponses[index];

                bool currResult = false;
                try
                {
                    currResult = await asyncResponse.ConfigureAwait(false);
                    if (!currResult)
                    {
                        numErrors++;
                        this.logger?.LogError($"EnqueueList() - failure at index {index}");
                    }
                }
                catch (Exception ex)
                {
                    numErrors++;
                    this.logger?.LogError($"EnqueueList() - failure at index {index} - {ex.StackTrace}");
                }
                results.Add(currResult);
            }

            // Log summary of call results
            if (numErrors > 0)
            {
                this.logger?.LogError($"EnqueueList() had {numErrors} error(s) out of {messages.Count}");
            }
            else
            {
                this.logger?.LogInformation($"Successfully enqueued {messages.Count} messages in EnqueueList()");
            }

            // Return the list of
            return results;
        }

        /// <inheritdoc />
        public virtual async Task<bool> Enqueue<T>(T message, bool useHighPriorityQueue, string activityId = null)
        {
            Message currMessage = new Message(message, activityId);

            queueProvider.CreateIfNotExists();

            string messageStr = currMessage.ToString();

            if (messageStr.Length > queueProvider.MaxMessageSize)
            {
                this.logger?.LogError("Enqueue() - throw new OverflowException");

                throw new OverflowException("Message length cannot exceed 64kb");
            }

            bool sentFlag = false;
            for (int tryNumber = 1; tryNumber <= MaxSendMessageRetryCount && !sentFlag; tryNumber++)
            {
                sentFlag = await queueProvider.SendMessageAsync(messageStr, useHighPriorityQueue).ConfigureAwait(false);

                if (!sentFlag)
                {
                    this.logger?.LogError($"Enqueue() - failed: attempt #{tryNumber}");
                }
            }
            return sentFlag;
        }

        /// <inheritdoc />
        public async Task<List<T>> EnqueueBatchedMessages<T>(List<T> messages, bool useHighPriorityQueue, string activityId = null)
        {
            queueProvider.CreateIfNotExists();

            if (messages == null || messages.Count == 0)
            {
                // nothing to enqueue, so return an empty list back
                this.logger?.LogInformation("Nothing to enqueue, return an empty list back");
                return new List<T>();
            }

            // Keep track of all the sublists that we post
            List<List<T>> postRequests = new List<List<T>>();
            // Keep track of the corresponding results of each post that correlates with postRequests, for audit purposes
            List<Task<bool>> postResults = new List<Task<bool>>();

            // Get the message size if the entire list is posted in a single message
            int messageSize = MessageUtils.GetMessageSize(messages, activityId);

            this.logger?.LogInformation($"Requesting post {messages.Count} message(s), the size is {messageSize}");

            // Check that we can post post all of the objects in the list with a single message
            if (messageSize <= queueProvider.MaxMessageSize)
            {
                // Since we can post all the messages, let us do so.
                postRequests.Add(messages);
                postResults.Add(this.Enqueue<List<T>>(messages, useHighPriorityQueue, activityId));
            }
            else
            {
                // Since all the messages in the object list cannot be posted in a single message, break down the original list
                // into two approximately equally sized sublists keep them in the lists array
                List<List<T>> subLists = new List<List<T>>();
                int subListSize = messages.Count;

                if (subListSize == 1)
                {
                    // There is only one message, and it is too large, so add it to the failed results list
                    postRequests.Add(messages);
                    postResults.Add(Task.FromResult(false));
                }
                else
                {
                    subLists.Add(messages.GetRange(0, subListSize / 2));
                    subLists.Add(messages.GetRange(subListSize / 2, subListSize - subListSize / 2));

                    // Iterate through the lists array to process each sublist
                    for (int index = 0; index < subLists.Count; index++)
                    {
                        List<T> currSubList = subLists[index];

                        // Get the message size if all the objects in the sublist were posted in one message
                        int subMessageSize = MessageUtils.GetMessageSize(currSubList, activityId);

                        // Check if the message size is within the max message size limit
                        if (subMessageSize > queueProvider.MaxMessageSize)
                        {
                            // Since the size of the single message from the sublist is still too large, handle it 
                            subListSize = currSubList.Count;

                            if (subListSize == 1)
                            {
                                // There is only one message, and it is too large, so add it to the failed results list
                                postRequests.Add(currSubList);
                                postResults.Add(Task.FromResult(false));
                            }
                            else
                            {
                                // Break down the large list into two smaller lists and add them to the subLists to be processed next
                                subLists.Insert(index + 1, currSubList.GetRange(0, subListSize / 2));
                                subLists.Insert(index + 2, currSubList.GetRange(subListSize / 2, subListSize - subListSize / 2));
                            }

                            // Get out of this iteration, since these broken sublists will be start to be processed in the next iteration
                            continue;
                        }

                        // Since we have come here, the subList from the current iteration can be posted in a single message.
                        this.logger?.LogInformation($"Posting {currSubList.Count} message(s), the size is {subMessageSize}");

                        // Posting all the messages in the current subList, and tracking success asynchronously
                        postRequests.Add(currSubList);
                        postResults.Add(this.Enqueue<List<T>>(currSubList, useHighPriorityQueue, activityId));
                    }
                }
            }

            // Read all the results from the asynchronous list posts, and track failed lists
            List<T> failedResults = new List<T>();
            for (int index = 0; index < postResults.Count; index++)
            {
                bool result = await postResults[index].ConfigureAwait(false);
                if (result) continue;
                // Since the post failed, add to failed Results
                failedResults.AddRange(postRequests[index]);
                this.logger?.LogError("EnqueueLargeList() - the post failed");
            }

            // This result list contains all unposted objects, so return it.
            return failedResults;
        }

        internal async Task<bool> EnqueueUnprocessedMessages<T>(T message, string activityId = null)
        {
            queueProvider.CreateIfNotExists();

            Message currMessage = new Message(message, activityId);

            string messageStr = currMessage.ToString();

            if (messageStr.Length > queueProvider.MaxMessageSize)
            {
                this.logger?.LogError("EnqueueUnprocessedMessages() - throw new OverflowException");

                throw new OverflowException("Message length cannot exceed 64kb");
            }

            bool sentFlag = false;
            for (int tryNumber = 1; tryNumber <= MaxSendMessageRetryCount && !sentFlag; tryNumber++)
            {
                sentFlag = await queueProvider.SendUnprocessedMessageAsync(messageStr).ConfigureAwait(false);

                if (!sentFlag)
                {
                    this.logger?.LogError($"EnqueueUnprocessedMessages() - failed: attempt #{tryNumber}");
                }
            }
            return sentFlag;
        }

        /// <inheritdoc />
        public virtual async Task DequeueAndProcess(int maxMessageRate, int maxAsynchronousProcesses, IMessageProcessor processor, CancellationToken cancellationToken, int batchSize = 1, int pollIntervalInMilliseconds = OneThousand)
        {
            await this.DequeueAndProcess(maxMessageRate, maxAsynchronousProcesses, IQueueProcessorHandler.Default().AddProcessor(typeof(string), processor), cancellationToken, batchSize, pollIntervalInMilliseconds).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task DequeueAndProcess(int maxMessageRate, int maxAsynchronousProcesses, IQueueProcessorHandler handler, CancellationToken cancellationToken, int batchSize = 1, int pollIntervalInMilliseconds = OneThousand)
        {
            await DequeueAndProcess(new QueueDequeueConfig(maxMessageRate, maxAsynchronousProcesses, handler, cancellationToken, batchSize, pollIntervalInMilliseconds)).ConfigureAwait(false);
        }

        public virtual async Task DequeueAndProcess(QueueDequeueConfig queueDequeueConfig)
        {
            queueProvider.CreateIfNotExists();

            // One indicates the method is already in use
            if (Interlocked.Exchange(ref this.pollingFlag, One) == One)
            {
                this.logger?.LogError("DequeueAndProcess() - throw new InvalidOperationException");
                throw new InvalidOperationException("Dequeueing is already being done");
            }

            this.QueueDequeueConfig = queueDequeueConfig;

            // keeps messages when read from backend queue
            List<object> unprocessedMessages = new List<object>(queueDequeueConfig.MaxMessagesPerPollingInterval);

            bool isMessagesHighPriority = true;

            // If the queue provider has a smaller max message count per poll, we need to restrict our max messages to this number
            int maxMessagesToRetrieve = Math.Min(queueDequeueConfig.MaxMessagesPerPollingInterval, queueProvider.MaxMessageCountPerPoll);

            long currIteration = 0;
            while (this.pollingFlag == One && (!queueDequeueConfig.CancellationToken.IsCancellationRequested))
            {
                currIteration++;
                try
                {
                    // Flag that polling has started
                    this.isPolling = true;

                    DateTime startDateTime = DateTime.Now;
                    int diffInMillis = 0;

                    int i = 0;
                    int availableAsyncProcessSlots = queueDequeueConfig.MaxAsynchronousProcesses - currActiveProcesses;

                    List<object> messageHookList = new List<object>();

                    while (i < queueDequeueConfig.MaxMessagesPerPollingInterval && availableAsyncProcessSlots > 0 && this.pollingFlag == One && (!QueueDequeueConfig.CancellationToken.IsCancellationRequested))
                    {
                        // Populate unprocessed messages list if empty
                        if (unprocessedMessages.Count == 0 && currActiveProcesses < maxMessagesToRetrieve)
                        {
                            isMessagesHighPriority = this.RefillUnprocessedMessages(maxMessagesToRetrieve, unprocessedMessages);
                        }

                        // Slow down by pollIntervalInMilliseconds to avoid a race condition if there is no message in either queue
                        if (i == 0 && unprocessedMessages.Count == 0)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(QueueDequeueConfig.PollIntervalMilliseconds)).ConfigureAwait(false);
                        }

                        if (unprocessedMessages.Count > 0)
                        {
                            object messageHook = unprocessedMessages[0];

                            unprocessedMessages.RemoveAt(0);

                            if (QueueDequeueConfig.BatchSize == 1)
                                // Start a thread pool to manage threads for PollMessages. Additional threads may or may not be created
                                //Note: do not await. We want this to run in a separte thread asynchronous to this flow
                                _ = this.ProcessMessage(messageHook, isMessagesHighPriority);
                            else
                            {
                                // Add the message hook to the list, and process it when the list reaches the configured batch size
                                messageHookList.Add(messageHook);
                                if (messageHookList.Count >= QueueDequeueConfig.BatchSize)
                                {
                                    // process the list of messages in messageHookList (using a copy to ensure that it is not cleared before invocation)
                                    List<object> clonedList = new List<object>(messageHookList);

                                    // Do not await. We want this to run in a separte thread asynchronous to this flow
                                    _ = this.ProcessMessageList(clonedList, isMessagesHighPriority);

                                    // Reassign the messageHookList to a new empty list
                                    messageHookList = new List<object>();
                                }
                            }

                            i++;
                        }

                        diffInMillis = Convert.ToInt32((DateTime.Now - startDateTime).TotalMilliseconds);

                        if (diffInMillis >= QueueDequeueConfig.PollIntervalMilliseconds)
                        {
                            break;
                        }
                    }

                    if (availableAsyncProcessSlots <= 0)
                    {
                        // Sleep for pollIntervalInMilliseconds before proceeding, to avoid a race condition
                        await Task.Delay(TimeSpan.FromMilliseconds(QueueDequeueConfig.PollIntervalMilliseconds)).ConfigureAwait(false);
                        this.logger?.LogError("PollMessage() - Reached max async process count of " + QueueDequeueConfig.MaxAsynchronousProcesses);
                    }

                    // process the remaining list of messages since we are either exiting the loop or have completed a polling interval
                    if (messageHookList.Count > 0)
                    {
                        // process the list of messages in messageHookList (using a copy to ensure that it is not cleared before invocation)
                        List<object> clonedList = new List<object>(messageHookList);
                        await ProcessMessageList(clonedList, isMessagesHighPriority).ConfigureAwait(false);
                    }

                    bool messageLimitReached = i >= QueueDequeueConfig.MaxMessagesPerPollingInterval;
                    bool pollingIntervalReached = diffInMillis >= QueueDequeueConfig.PollIntervalMilliseconds;
                    if (this.pollingFlag == One && (!QueueDequeueConfig.CancellationToken.IsCancellationRequested) && (!pollingIntervalReached) && messageLimitReached)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(QueueDequeueConfig.PollIntervalMilliseconds - diffInMillis)).ConfigureAwait(false);
                    }
                }

                // We should never come here - catch-all safety net. This is to prevent the polling from exiting unexpectedly.
                catch (Exception ex)
                {
                    // Sleep for pollIntervalInMilliseconds to avoid a race condition
                    await Task.Delay(TimeSpan.FromMilliseconds(QueueDequeueConfig.PollIntervalMilliseconds)).ConfigureAwait(false);

                    this.logger?.LogError($"PollMessages() - {ex.StackTrace}");
                    // We do not rethrow exception here otherwise queue polling will stop.
                }
            }

            this.isPolling = false;
        }

        /// <inheritdoc />
        public virtual async Task<bool> StopDequeuing()
        {
            // Zero indicates the method is not in use
            if (Interlocked.Exchange(ref this.pollingFlag, Zero) == Zero)
            {
                this.logger?.LogError("StopDequeuing() - throw InvalidOperationException()");
                throw new InvalidOperationException("Client is not dequeuing");
            }

            // Block until polling stops - should not be much longer than the poll interval that was set while dequeing
            int numRetries = (this.QueueDequeueConfig.PollIntervalMilliseconds + 1) / 10;
            int currRetry = 0;
            while (currRetry <= numRetries)
            {
                // sleep for 10ms and check if polling is over
                await Task.Delay(TimeSpan.FromMilliseconds(this.QueueDequeueConfig.PollIntervalMilliseconds)).ConfigureAwait(false);

                // Return when we have confirmed that polling is complete
                if (!this.isPolling)
                {
                    return true;
                }

                currRetry++;
            }

            // PollingCounter should have finished by now, but it hasn't so return false
            return false;
        }

        /// <inheritdoc />
        public virtual async Task<long> GetApproximateQueueLength(bool isHighPriorityQueue)
        {
            return await queueProvider.GetApproximateQueueLength(isHighPriorityQueue);
        }

        // Internal

        /// <summary>
        /// Processes the message using the provided IMessageProcessor.
        /// </summary>
        internal async Task ProcessMessage(object messageHook, bool isHighPriorityQueue)
        {
            string activityId = null;
            Activity currentActivity = new Activity("Queue-ProcessMessage");

            try
            {
                string messageStr = queueProvider.GetMessageBody(messageHook);

                if (string.IsNullOrEmpty(messageStr))
                {
                    this.logger?.LogError("Message was empty or null.");
                    return;
                }

                Message message = JsonSerializer.Deserialize<Message>(messageStr);
                activityId = message?.ActivityId;

                if (!string.IsNullOrWhiteSpace(activityId))
                {
                    currentActivity.SetParentId(activityId);
                }

                // Placeholder for distributed tracing
                this.logger?.LogInformation($"Processing message for activityId: {activityId}");

                Boolean processed = false;

                // Process the message and keep track of the active process count
                Interlocked.Increment(ref currActiveProcesses);
                try
                {
                    processed = await this.QueueDequeueConfig.ProcessorHandler.Process(message).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref currActiveProcesses);
                }

                if (!processed)
                {
                    bool sentFlag = this.EnqueueUnprocessedMessages(message, activityId).Result;

                    this.logger?.LogWarning($"Message is not processed correctly and moved to the unprocessedmessagequeue with status = {sentFlag}. Potential loss of message detected. activityId: {activityId}");
                }

                await queueProvider.DeleteMessageAsync(messageHook, isHighPriorityQueue).ConfigureAwait(false);
            }
            catch (ApplicationException)
            {
                this.logger?.LogError($"Application could not process message. Message being deleted from queue. activityId: {activityId}");
                try
                {
                    await queueProvider.DeleteMessageAsync(messageHook, isHighPriorityQueue).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    this.logger?.LogError(e, e.Message);
                    // If we get exception during delete, we do not want to throw it away here otherwise it will cause MessageProcessor thread to get killed.
                }
            }
            catch (Exception ex)
            {
                this.logger?.LogError(ex, $"Queue ProcessMessage Exception");
                //If we throw exception here the Message processor running in thread will crash and application will stop receiving messages from queue.
            }
            finally
            {
                currentActivity.Stop();
            }
        }

        /// <summary>
        /// Processes the message list using the provided IMessageProcessor.
        /// </summary>
        internal async Task ProcessMessageList(List<object> messageHookList, bool isHighPriorityQueue)
        {
            bool hasError = false;
            List<Message> messageList = new List<Message>();
            try
            {
                foreach (object messageHook in messageHookList)
                {
                    try
                    {
                        string messageStr = queueProvider.GetMessageBody(messageHook);

                        if (string.IsNullOrEmpty(messageStr))
                        {
                            this.logger?.LogError("Message was empty or null.");
                            hasError = true;
                            break;
                        }

                        Message message = JsonSerializer.Deserialize<Message>(messageStr);
                        string activityId = message?.ActivityId;
                        messageList.Add(message);

                        // Placeholder for distributed tracing
                        this.logger?.LogInformation($"Processing message for activityId: {activityId}");
                    }
                    catch (Exception e)
                    {
                        this.logger?.LogError(e, e.Message);
                        // If we get exception during delete, we do not want to throw it here but send approprate response
                        hasError = true;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                this.logger?.LogError(e, e.Message);
                // If we get exception during delete, we do not want to throw it away here but send approprate response
                hasError = true;
            }

            // If there are errors we do not want to go through this block, but rather let the entire list get reprocessed
            if (!hasError)
            {
                List<bool> processed;
                try
                {

                    // Process the message and keep track of the active process count
                    Interlocked.Increment(ref currActiveProcesses);
                    try
                    {
                        processed = await this.QueueDequeueConfig.ProcessorHandler.Process(messageList).ConfigureAwait(false);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref currActiveProcesses);
                    }

                    // Iterate through individual messages to check status
                    for (int index = 0; index < processed.Count; index++)
                    {
                        Message unwrappedMessage = messageList[index];
                        bool success = processed[index];
                        object messageHook = messageHookList[index];

                        try
                        {
                            if (!success)
                            {
                                bool sentFlag = this.EnqueueUnprocessedMessages(unwrappedMessage).Result;
                                this.logger?.LogWarning($"Message is not processed correctly and moved to the unprocessedmessagequeue with status = {sentFlag}. Potential loss of message detected. activityId: {unwrappedMessage.ActivityId}");
                            }

                            await queueProvider.DeleteMessageAsync(messageHook, isHighPriorityQueue).ConfigureAwait(false);
                        }
                        catch (ApplicationException)
                        {
                            this.logger?.LogError($"Application could not process message. Message being deleted from queue. activityId: {unwrappedMessage.ActivityId}");
                            try
                            {
                                await queueProvider.DeleteMessageAsync(messageHook, isHighPriorityQueue).ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                this.logger?.LogError(e, e.Message);
                                // Do not rethrow to stay in the loop
                            }
                        }
                        catch (Exception ex)
                        {
                            this.logger?.LogError(ex, $"Queue ProcessMessage Exception");
                            // Do not rethrow to stay in the loop
                        }
                    }

                    this.logger?.LogInformation($"Completed processing of Queue ProcessMessageList with {messageHookList.Count} messages");
                }
                catch (Exception ex)
                {
                    this.logger?.LogError(ex, $"Queue ProcessMessageList Exception");
                    //If we throw exception here the Message processor running in thread will crash and application will stop receiving messages from queue.
                }
            }
        }

        /// <summary>
        /// Refill from the high priority queue if there are any messages in it, otherwise the low priority queue
        /// Returns the QueueClient from the queue that messages came from, otherwise null.
        /// </summary>
        internal bool RefillUnprocessedMessages(int maxMessagesToRetrieve, List<object> messageHooks)
        {
            bool isHighPriorityQueue = true;

            IList<object> currMessageHooks = queueProvider.GetMessages(maxMessagesToRetrieve, isHighPriorityQueue);

            if (currMessageHooks.Count == 0 && queueProvider.HasLowPriorityQueue)
            {
                isHighPriorityQueue = false;

                currMessageHooks = queueProvider.GetMessages(maxMessagesToRetrieve, isHighPriorityQueue);
            }

            messageHooks.AddRange(currMessageHooks);

            return isHighPriorityQueue;
        }
    }
}