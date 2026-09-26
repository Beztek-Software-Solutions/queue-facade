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

        private readonly string name;
        private readonly ILogger logger;

        // Flags if polling should continue or should stop
        private int pollingFlag = Zero;

        // Count of current active processes
        private int currActiveProcesses = 0;

        // Flags if polling is currently active. When pollingFlag is set false, this should soon become false as well.
        private bool isPolling;

        internal IQueueProvider queueProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueClient"/> class.
        /// </summary>
        /// <param name="name">Logical client name.</param>
        /// <param name="queueProvider">Backing queue provider.</param>
        /// <param name="logger">Optional logger.</param>
        internal QueueClient(string name, IQueueProvider queueProvider, ILogger logger = null)
        {
            this.name = name;
            this.queueProvider = queueProvider;
            this.logger = logger;
        }

        /// <inheritdoc />
        public string GetName()
        {
            return this.name;
        }

        /// <summary>
        /// Active dequeue settings. Retained so a stopped loop can be restarted with the same configuration.
        /// </summary>
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
                this.logger?.LogDebug($"Successfully enqueued {messages.Count} messages in EnqueueList()");
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
                this.logger?.LogDebug("Nothing to enqueue, return an empty list back");
                return new List<T>();
            }

            List<List<T>> postRequests = new List<List<T>>();
            List<Task<bool>> postResults = new List<Task<bool>>();
            ScheduleBatchedPosts(messages, useHighPriorityQueue, activityId, postRequests, postResults);
            return await CollectFailedBatchResultsAsync(postRequests, postResults).ConfigureAwait(false);
        }

        private void ScheduleBatchedPosts<T>(
            List<T> messages,
            bool useHighPriorityQueue,
            string activityId,
            List<List<T>> postRequests,
            List<Task<bool>> postResults)
        {
            int messageSize = MessageUtils.GetMessageSize(messages, activityId);
            this.logger?.LogDebug($"Requesting post {messages.Count} message(s), the size is {messageSize}");

            if (messageSize <= queueProvider.MaxMessageSize)
            {
                postRequests.Add(messages);
                postResults.Add(this.Enqueue<List<T>>(messages, useHighPriorityQueue, activityId));
                return;
            }

            ScheduleOversizedBatchSplits(messages, useHighPriorityQueue, activityId, postRequests, postResults);
        }

        private void ScheduleOversizedBatchSplits<T>(
            List<T> messages,
            bool useHighPriorityQueue,
            string activityId,
            List<List<T>> postRequests,
            List<Task<bool>> postResults)
        {
            if (messages.Count == 1)
            {
                MarkBatchAsFailed(messages, postRequests, postResults);
                return;
            }

            List<List<T>> subLists = new List<List<T>>
            {
                messages.GetRange(0, messages.Count / 2),
                messages.GetRange(messages.Count / 2, messages.Count - messages.Count / 2),
            };

            for (int index = 0; index < subLists.Count; index++)
            {
                List<T> currSubList = subLists[index];
                int subMessageSize = MessageUtils.GetMessageSize(currSubList, activityId);

                if (subMessageSize > queueProvider.MaxMessageSize)
                {
                    SplitOrFailOversizedSubList(currSubList, index, subLists, postRequests, postResults);
                    continue;
                }

                this.logger?.LogDebug($"Posting {currSubList.Count} message(s), the size is {subMessageSize}");
                postRequests.Add(currSubList);
                postResults.Add(this.Enqueue<List<T>>(currSubList, useHighPriorityQueue, activityId));
            }
        }

        private static void SplitOrFailOversizedSubList<T>(
            List<T> currSubList,
            int index,
            List<List<T>> subLists,
            List<List<T>> postRequests,
            List<Task<bool>> postResults)
        {
            int subListSize = currSubList.Count;
            if (subListSize == 1)
            {
                MarkBatchAsFailed(currSubList, postRequests, postResults);
                return;
            }

            subLists.Insert(index + 1, currSubList.GetRange(0, subListSize / 2));
            subLists.Insert(index + 2, currSubList.GetRange(subListSize / 2, subListSize - subListSize / 2));
        }

        private static void MarkBatchAsFailed<T>(
            List<T> batch,
            List<List<T>> postRequests,
            List<Task<bool>> postResults)
        {
            postRequests.Add(batch);
            postResults.Add(Task.FromResult(false));
        }

        private async Task<List<T>> CollectFailedBatchResultsAsync<T>(
            List<List<T>> postRequests,
            List<Task<bool>> postResults)
        {
            List<T> failedResults = new List<T>();
            for (int index = 0; index < postResults.Count; index++)
            {
                bool result = await postResults[index].ConfigureAwait(false);
                if (result)
                {
                    continue;
                }

                failedResults.AddRange(postRequests[index]);
                this.logger?.LogError("EnqueueLargeList() - the post failed");
            }

            return failedResults;
        }

        internal async Task<bool> EnqueueUnprocessedMessages<T>(T message, string activityId = null)
        {
            queueProvider.CreateIfNotExists();

            // Avoid double-wrapping when the processor already handed us a Message envelope.
            string messageStr = message is Message existing
                ? existing.ToString()
                : new Message(message, activityId).ToString();

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
        public virtual async Task DequeueAndProcess(int maxMessageRate, int maxAsynchronousProcesses, IMessageProcessor processor, CancellationToken cancellationToken, int batchSize = 1, int pollIntervalInMilliseconds = OneThousand, int maxProcessingAttempts = QueueDequeueConfig.DefaultMaxProcessingAttempts)
        {
            await this.DequeueAndProcess(maxMessageRate, maxAsynchronousProcesses, IQueueProcessorHandler.Default().AddProcessor(typeof(string), processor), cancellationToken, batchSize, pollIntervalInMilliseconds, maxProcessingAttempts).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task DequeueAndProcess(int maxMessageRate, int maxAsynchronousProcesses, IQueueProcessorHandler handler, CancellationToken cancellationToken, int batchSize = 1, int pollIntervalInMilliseconds = OneThousand, int maxProcessingAttempts = QueueDequeueConfig.DefaultMaxProcessingAttempts)
        {
            await DequeueAndProcess(new QueueDequeueConfig(maxMessageRate, maxAsynchronousProcesses, handler, cancellationToken, batchSize, pollIntervalInMilliseconds, maxProcessingAttempts)).ConfigureAwait(false);
        }

        /// <summary>
        /// Receive messages from the queue using the settings in <paramref name="queueDequeueConfig"/>.
        /// </summary>
        /// <param name="queueDequeueConfig">Dequeue rate, batching, processor, and attempt limits.</param>
        public virtual async Task DequeueAndProcess(QueueDequeueConfig queueDequeueConfig)
        {
            queueProvider.CreateIfNotExists();
            BeginExclusivePollingOrThrow();
            this.QueueDequeueConfig = queueDequeueConfig;

            List<object> unprocessedMessages = new List<object>(queueDequeueConfig.MaxMessagesPerPollingInterval);
            bool isMessagesHighPriority = true;
            int maxMessagesToRetrieve = Math.Min(
                queueDequeueConfig.MaxMessagesPerPollingInterval,
                queueProvider.MaxMessageCountPerPoll);

            while (this.pollingFlag == One && (!queueDequeueConfig.CancellationToken.IsCancellationRequested))
            {
                try
                {
                    this.isPolling = true;
                    isMessagesHighPriority = await RunOnePollingIntervalAsync(
                        queueDequeueConfig,
                        unprocessedMessages,
                        maxMessagesToRetrieve,
                        isMessagesHighPriority).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Safety net: keep polling alive; do not rethrow.
                    await HandleUnexpectedPollExceptionAsync(ex).ConfigureAwait(false);
                }
            }

            this.isPolling = false;
        }

        private void BeginExclusivePollingOrThrow()
        {
            if (Interlocked.Exchange(ref this.pollingFlag, One) == One)
            {
                this.logger?.LogError("DequeueAndProcess() - throw new InvalidOperationException");
                throw new InvalidOperationException("Dequeueing is already being done");
            }
        }

        private async Task<bool> RunOnePollingIntervalAsync(
            QueueDequeueConfig queueDequeueConfig,
            List<object> unprocessedMessages,
            int maxMessagesToRetrieve,
            bool isMessagesHighPriority)
        {
            DateTime startDateTime = DateTime.Now;
            int availableAsyncProcessSlots = queueDequeueConfig.MaxAsynchronousProcesses - currActiveProcesses;
            List<object> messageHookList = new List<object>();

            (int dispatched, bool isHighPriority) = await DispatchMessagesForIntervalAsync(
                queueDequeueConfig,
                unprocessedMessages,
                maxMessagesToRetrieve,
                availableAsyncProcessSlots,
                messageHookList,
                startDateTime,
                isMessagesHighPriority).ConfigureAwait(false);

            await PaceWhenSaturatedAsync(availableAsyncProcessSlots).ConfigureAwait(false);
            await FlushRemainingBatchAsync(messageHookList, isHighPriority).ConfigureAwait(false);
            await PaceUntilNextIntervalAsync(dispatched, startDateTime).ConfigureAwait(false);
            return isHighPriority;
        }

        private async Task<(int Dispatched, bool IsHighPriority)> DispatchMessagesForIntervalAsync(
            QueueDequeueConfig queueDequeueConfig,
            List<object> unprocessedMessages,
            int maxMessagesToRetrieve,
            int availableAsyncProcessSlots,
            List<object> messageHookList,
            DateTime startDateTime,
            bool isHighPriority)
        {
            int dispatched = 0;
            while (ShouldContinueDispatch(queueDequeueConfig, dispatched, availableAsyncProcessSlots))
            {
                if (unprocessedMessages.Count == 0 && currActiveProcesses < maxMessagesToRetrieve)
                {
                    isHighPriority = this.RefillUnprocessedMessages(maxMessagesToRetrieve, unprocessedMessages);
                }

                if (dispatched == 0 && unprocessedMessages.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(QueueDequeueConfig.PollIntervalMilliseconds)).ConfigureAwait(false);
                }

                if (unprocessedMessages.Count > 0)
                {
                    object messageHook = unprocessedMessages[0];
                    unprocessedMessages.RemoveAt(0);
                    DispatchFetchedMessage(messageHook, isHighPriority, messageHookList);
                    dispatched++;
                }

                if (ElapsedMillis(startDateTime) >= QueueDequeueConfig.PollIntervalMilliseconds)
                {
                    break;
                }
            }

            return (dispatched, isHighPriority);
        }

        private bool ShouldContinueDispatch(
            QueueDequeueConfig queueDequeueConfig,
            int dispatched,
            int availableAsyncProcessSlots)
        {
            return dispatched < queueDequeueConfig.MaxMessagesPerPollingInterval
                && availableAsyncProcessSlots > 0
                && this.pollingFlag == One
                && (!QueueDequeueConfig.CancellationToken.IsCancellationRequested);
        }

        private void DispatchFetchedMessage(object messageHook, bool isHighPriority, List<object> messageHookList)
        {
            if (QueueDequeueConfig.BatchSize == 1)
            {
                // Fire-and-forget: process on the thread pool without blocking the poll loop.
                _ = this.ProcessMessage(messageHook, isHighPriority);
                return;
            }

            messageHookList.Add(messageHook);
            if (messageHookList.Count < QueueDequeueConfig.BatchSize)
            {
                return;
            }

            List<object> clonedList = new List<object>(messageHookList);
            _ = this.ProcessMessageList(clonedList, isHighPriority);
            messageHookList.Clear();
        }

        private async Task PaceWhenSaturatedAsync(int availableAsyncProcessSlots)
        {
            if (availableAsyncProcessSlots > 0)
            {
                return;
            }

            this.logger?.LogDebug("PollMessage() - Reached max async process count of " + QueueDequeueConfig.MaxAsynchronousProcesses);
            await Task.Delay(TimeSpan.FromMilliseconds(QueueDequeueConfig.PollIntervalMilliseconds)).ConfigureAwait(false);
        }

        private async Task FlushRemainingBatchAsync(List<object> messageHookList, bool isHighPriority)
        {
            if (messageHookList.Count == 0)
            {
                return;
            }

            List<object> clonedList = new List<object>(messageHookList);
            await ProcessMessageList(clonedList, isHighPriority).ConfigureAwait(false);
        }

        private async Task PaceUntilNextIntervalAsync(int dispatched, DateTime startDateTime)
        {
            int diffInMillis = ElapsedMillis(startDateTime);
            bool messageLimitReached = dispatched >= QueueDequeueConfig.MaxMessagesPerPollingInterval;
            bool pollingIntervalReached = diffInMillis >= QueueDequeueConfig.PollIntervalMilliseconds;
            if (this.pollingFlag != One
                || QueueDequeueConfig.CancellationToken.IsCancellationRequested
                || pollingIntervalReached
                || !messageLimitReached)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(QueueDequeueConfig.PollIntervalMilliseconds - diffInMillis)).ConfigureAwait(false);
        }

        private async Task HandleUnexpectedPollExceptionAsync(Exception ex)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(QueueDequeueConfig.PollIntervalMilliseconds)).ConfigureAwait(false);
            this.logger?.LogError($"PollMessages() - {ex.StackTrace}");
        }

        private static int ElapsedMillis(DateTime startDateTime) =>
            Convert.ToInt32((DateTime.Now - startDateTime).TotalMilliseconds);

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

        /// <inheritdoc />
        public virtual async Task<long> GetApproximateUnprocessedQueueLength()
        {
            queueProvider.CreateIfNotExists();
            return await queueProvider.GetApproximateUnprocessedQueueLength().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual Task<IReadOnlyList<Message>> PeekUnprocessedMessagesAsync(int maxMessages = 32)
        {
            queueProvider.CreateIfNotExists();
            IList<object> hooks = queueProvider.PeekUnprocessedMessages(Math.Max(1, maxMessages));
            List<Message> results = new List<Message>();
            foreach (object hook in hooks)
            {
                string body = queueProvider.GetMessageBody(hook);
                if (string.IsNullOrEmpty(body))
                {
                    continue;
                }

                try
                {
                    results.Add(JsonSerializer.Deserialize<Message>(body));
                }
                catch (Exception ex)
                {
                    this.logger?.LogError(ex, "PeekUnprocessedMessagesAsync failed to deserialize poison payload");
                }
            }

            return Task.FromResult<IReadOnlyList<Message>>(results);
        }

        /// <inheritdoc />
        public virtual async Task<int> RequeueUnprocessedMessagesAsync(int maxMessages = 32, bool useHighPriorityQueue = true)
        {
            queueProvider.CreateIfNotExists();
            IList<object> hooks = queueProvider.ReceiveUnprocessedMessages(Math.Max(1, maxMessages));
            int requeued = 0;

            foreach (object hook in hooks)
            {
                string body = queueProvider.GetMessageBody(hook);
                if (string.IsNullOrEmpty(body))
                {
                    continue;
                }

                try
                {
                    Message message = JsonSerializer.Deserialize<Message>(body);
                    if (message != null)
                    {
                        // Fresh primary-queue delivery; receive-count starts over.
                        message.ProcessingAttempt = 0;
                        body = message.ToString();
                    }

                    bool sent = await queueProvider.SendMessageAsync(body, useHighPriorityQueue).ConfigureAwait(false);
                    if (sent)
                    {
                        await queueProvider.DeleteUnprocessedMessageAsync(hook).ConfigureAwait(false);
                        requeued++;
                    }
                    else
                    {
                        this.logger?.LogWarning("RequeueUnprocessedMessagesAsync: send to primary queue failed; leaving message on poison queue");
                    }
                }
                catch (Exception ex)
                {
                    this.logger?.LogError(ex, "RequeueUnprocessedMessagesAsync failed for one poison message");
                }
            }

            return requeued;
        }

        // Internal

        /// <summary>
        /// Processes the message using the provided IMessageProcessor.
        /// </summary>
        internal async Task ProcessMessage(object messageHook, bool isHighPriorityQueue)
        {
            string activityId = null;
            Activity currentActivity = new Activity("Queue-ProcessMessage");
            Message message = null;
            int attempt = 1;

            try
            {
                if (!TryParseIncomingMessage(messageHook, out message, out attempt, out activityId))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(activityId))
                {
                    currentActivity.SetParentId(activityId);
                }

                this.logger?.LogDebug($"Processing message for activityId: {activityId}, attempt: {attempt}/{this.QueueDequeueConfig.MaxProcessingAttempts}");

                bool processed = await RunProcessorAsync(message).ConfigureAwait(false);
                await FinalizeSingleMessageAsync(messageHook, isHighPriorityQueue, message, activityId, processed).ConfigureAwait(false);
            }
            catch (ApplicationException)
            {
                await DeleteAfterApplicationFailureAsync(messageHook, isHighPriorityQueue, activityId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await HandleSingleProcessExceptionAsync(messageHook, isHighPriorityQueue, message, activityId, attempt, ex).ConfigureAwait(false);
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
            if (!TryBuildBatch(messageHookList, out List<Message> messageList, out List<int> attempts))
            {
                // Errors during parse: leave the batch for visibility-timeout retry.
                return;
            }

            try
            {
                List<bool> processed = await RunBatchProcessorAsync(messageList).ConfigureAwait(false);
                await FinalizeBatchResultsAsync(messageHookList, isHighPriorityQueue, messageList, attempts, processed).ConfigureAwait(false);
                this.logger?.LogDebug($"Completed processing of Queue ProcessMessageList with {messageHookList.Count} messages");
            }
            catch (Exception ex)
            {
                this.logger?.LogError(ex, $"Queue ProcessMessageList Exception");
                await PoisonBatchAtMaxAttemptsAsync(messageHookList, isHighPriorityQueue, messageList, attempts).ConfigureAwait(false);
            }
        }

        private bool TryParseIncomingMessage(object messageHook, out Message message, out int attempt, out string activityId)
        {
            message = null;
            attempt = 1;
            activityId = null;

            string messageStr = queueProvider.GetMessageBody(messageHook);
            if (string.IsNullOrEmpty(messageStr))
            {
                this.logger?.LogError("Message was empty or null.");
                return false;
            }

            message = JsonSerializer.Deserialize<Message>(messageStr);
            activityId = message?.ActivityId;
            attempt = Math.Max(1, queueProvider.GetReceiveCount(messageHook));
            if (message != null)
            {
                message.ProcessingAttempt = attempt;
            }

            return true;
        }

        private async Task<bool> RunProcessorAsync(Message message)
        {
            Interlocked.Increment(ref currActiveProcesses);
            try
            {
                return await this.QueueDequeueConfig.ProcessorHandler.Process(message).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref currActiveProcesses);
            }
        }

        private async Task<List<bool>> RunBatchProcessorAsync(List<Message> messageList)
        {
            Interlocked.Increment(ref currActiveProcesses);
            try
            {
                return await this.QueueDequeueConfig.ProcessorHandler.Process(messageList).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref currActiveProcesses);
            }
        }

        private async Task FinalizeSingleMessageAsync(
            object messageHook,
            bool isHighPriorityQueue,
            Message message,
            string activityId,
            bool processed)
        {
            if (!processed)
            {
                bool sentFlag = await this.EnqueueUnprocessedMessages(message, activityId).ConfigureAwait(false);
                this.logger?.LogWarning($"Message processing returned false; moved to poison queue (sent={sentFlag}). activityId: {activityId}");
            }

            await queueProvider.DeleteMessageAsync(messageHook, isHighPriorityQueue).ConfigureAwait(false);
        }

        private async Task DeleteAfterApplicationFailureAsync(object messageHook, bool isHighPriorityQueue, string activityId)
        {
            this.logger?.LogError($"Application could not process message. Message being deleted from queue. activityId: {activityId}");
            try
            {
                await queueProvider.DeleteMessageAsync(messageHook, isHighPriorityQueue).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                this.logger?.LogError(e, e.Message);
            }
        }

        private async Task HandleSingleProcessExceptionAsync(
            object messageHook,
            bool isHighPriorityQueue,
            Message message,
            string activityId,
            int attempt,
            Exception ex)
        {
            this.logger?.LogError(ex, $"Queue ProcessMessage Exception (attempt {attempt}/{this.QueueDequeueConfig?.MaxProcessingAttempts})");
            if (attempt < (this.QueueDequeueConfig?.MaxProcessingAttempts ?? QueueDequeueConfig.DefaultMaxProcessingAttempts))
            {
                // Leave message — visibility timeout (~30s) will make it reappear and increment receive count
                return;
            }

            try
            {
                if (message != null)
                {
                    bool sentFlag = await this.EnqueueUnprocessedMessages(message, activityId).ConfigureAwait(false);
                    this.logger?.LogWarning($"Max processing attempts reached; moved to poison queue (sent={sentFlag}). activityId: {activityId}");
                }

                await queueProvider.DeleteMessageAsync(messageHook, isHighPriorityQueue).ConfigureAwait(false);
            }
            catch (Exception poisonEx)
            {
                this.logger?.LogError(poisonEx, "Failed to move message to poison queue after max attempts");
            }
        }

        private bool TryBuildBatch(List<object> messageHookList, out List<Message> messageList, out List<int> attempts)
        {
            messageList = new List<Message>();
            attempts = new List<int>();

            try
            {
                foreach (object messageHook in messageHookList)
                {
                    if (!TryAppendBatchMessage(messageHook, messageList, attempts))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                this.logger?.LogError(e, e.Message);
                return false;
            }
        }

        private bool TryAppendBatchMessage(object messageHook, List<Message> messageList, List<int> attempts)
        {
            try
            {
                string messageStr = queueProvider.GetMessageBody(messageHook);
                if (string.IsNullOrEmpty(messageStr))
                {
                    this.logger?.LogError("Message was empty or null.");
                    return false;
                }

                Message message = JsonSerializer.Deserialize<Message>(messageStr);
                int attempt = Math.Max(1, queueProvider.GetReceiveCount(messageHook));
                if (message != null)
                {
                    message.ProcessingAttempt = attempt;
                }

                attempts.Add(attempt);
                messageList.Add(message);
                this.logger?.LogDebug(
                    $"Processing message for activityId: {message?.ActivityId}, attempt: {attempt}/{this.QueueDequeueConfig.MaxProcessingAttempts}");
                return true;
            }
            catch (Exception e)
            {
                this.logger?.LogError(e, e.Message);
                return false;
            }
        }

        private async Task FinalizeBatchResultsAsync(
            List<object> messageHookList,
            bool isHighPriorityQueue,
            List<Message> messageList,
            List<int> attempts,
            List<bool> processed)
        {
            for (int index = 0; index < processed.Count; index++)
            {
                Message unwrappedMessage = messageList[index];
                bool success = processed[index];
                object messageHook = messageHookList[index];

                try
                {
                    if (!success)
                    {
                        bool sentFlag = await this.EnqueueUnprocessedMessages(unwrappedMessage).ConfigureAwait(false);
                        this.logger?.LogWarning($"Message processing returned false; moved to poison queue (sent={sentFlag}). activityId: {unwrappedMessage.ActivityId}");
                    }

                    await queueProvider.DeleteMessageAsync(messageHook, isHighPriorityQueue).ConfigureAwait(false);
                }
                catch (ApplicationException)
                {
                    await DeleteAfterApplicationFailureAsync(messageHook, isHighPriorityQueue, unwrappedMessage.ActivityId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.logger?.LogError(ex, $"Queue ProcessMessageList per-message Exception");
                    await MaybePoisonAtMaxAttemptsAsync(messageHook, isHighPriorityQueue, unwrappedMessage, attempts[index]).ConfigureAwait(false);
                }
            }
        }

        private async Task MaybePoisonAtMaxAttemptsAsync(
            object messageHook,
            bool isHighPriorityQueue,
            Message message,
            int attempt)
        {
            if (attempt < this.QueueDequeueConfig.MaxProcessingAttempts)
            {
                return;
            }

            try
            {
                bool sentFlag = await this.EnqueueUnprocessedMessages(message).ConfigureAwait(false);
                this.logger?.LogWarning($"Max processing attempts reached; moved to poison queue (sent={sentFlag}). activityId: {message.ActivityId}");
                await queueProvider.DeleteMessageAsync(messageHook, isHighPriorityQueue).ConfigureAwait(false);
            }
            catch (Exception poisonEx)
            {
                this.logger?.LogError(poisonEx, "Failed to move message to poison queue after max attempts");
            }
        }

        private async Task PoisonBatchAtMaxAttemptsAsync(
            List<object> messageHookList,
            bool isHighPriorityQueue,
            List<Message> messageList,
            List<int> attempts)
        {
            int maxAttempts = this.QueueDequeueConfig?.MaxProcessingAttempts ?? QueueDequeueConfig.DefaultMaxProcessingAttempts;
            for (int index = 0; index < messageList.Count; index++)
            {
                if (attempts[index] < maxAttempts)
                {
                    continue;
                }

                try
                {
                    bool sentFlag = await this.EnqueueUnprocessedMessages(messageList[index]).ConfigureAwait(false);
                    this.logger?.LogWarning($"Max processing attempts reached after batch failure; moved to poison queue (sent={sentFlag}). activityId: {messageList[index]?.ActivityId}");
                    await queueProvider.DeleteMessageAsync(messageHookList[index], isHighPriorityQueue).ConfigureAwait(false);
                }
                catch (Exception poisonEx)
                {
                    this.logger?.LogError(poisonEx, "Failed to move message to poison queue after max attempts");
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
