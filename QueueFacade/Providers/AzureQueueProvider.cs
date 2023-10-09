// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Queues;
    using Azure.Storage.Queues.Models;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// We use azure queue storage in this implementation
    /// </summary>
    internal class AzureQueueProvider : IQueueProvider
    {
        private const int ThirtyTwo = 32;
        private const int Zero = 0;
        private const int One = 1;

        private readonly ILogger logger;

        // Lock to protect the queue creation so that it blocks other calls
        private readonly object _syncLock = new object();

        // Flag so that CreateIfNotExists() is not called more than once.
        private int _queusCreatedFlag = Zero;

        internal readonly IList<QueueClient> _queueClients = new List<QueueClient>();
        internal readonly QueueClient unprocessedQueueClient = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureQueueProvider"/> class.
        /// We are using nullable parameters to avoid extra constructors. There is a use case in batching where they need to use a low priority queue for large batches
        /// and a high priority queue for single batches. We want this library to be extensible for that use case.
        /// Note: For those unfamiliar with C#, an optional parameter means one can use multiple constructors for multiple scenarios; you literally don't have to supply the optional parameter.
        /// </summary>
        /// <param name="azureQueueProviderConfig"></param>
        /// <param name="logger"></param>
        internal AzureQueueProvider(AzureQueueProviderConfig azureQueueProviderConfig, ILogger logger = null)
        {
            // Logger
            this.logger = logger;

            // StorageClientCreator
            this.azureStorageClientCreator = azureQueueProviderConfig.AzureStorageClientCreator;

            // High Priority Queue
            string highPriorityQueue = azureQueueProviderConfig.HighPriorityQueue.ToLower();
            logger?.LogInformation("highPriorityQueue: {highPriorityQueue}");
            QueueClient queueClient1 = this.CreateQueueClient(highPriorityQueue, azureQueueProviderConfig.Endpoint);
            this._queueClients.Add(queueClient1);
            this.MaxMessageSize = _queueClients[0].MessageMaxBytes;

            // Unprocessed Message QUeue
            logger?.LogInformation($"UnprocessedMessageQueue: {Constants.UnprocessedMessageQueue}");
            unprocessedQueueClient = this.CreateQueueClient(Constants.UnprocessedMessageQueue, azureQueueProviderConfig.Endpoint);

            // Low Priority Queue
            if (!string.IsNullOrEmpty(azureQueueProviderConfig.LowPriorityQueue))
            {
                string lowPriorityQueue = azureQueueProviderConfig.LowPriorityQueue.ToLower();
                logger?.LogInformation($"lowPriorityQueue: {lowPriorityQueue}");
                QueueClient queueClient2 = this.CreateQueueClient(lowPriorityQueue, azureQueueProviderConfig.Endpoint);
                this.HasLowPriorityQueue = true;
                this._queueClients.Add(queueClient2);
            }
        }

        public bool HasLowPriorityQueue { get; }

        public int MaxMessageSize { get; }

        public int MaxMessageCountPerPoll { get; } = ThirtyTwo;
        internal AzureStorageClientCreator azureStorageClientCreator { get; set; }

        // Overrides
        /// <summary>
        /// Create the required queues if they do not exist.
        /// </summary>
        public void CreateIfNotExists()
        {
            // Using the double-checked locking pattern
            if (this._queusCreatedFlag == Zero)
            {
                lock (this._syncLock)
                {
                    if (Interlocked.Exchange(ref this._queusCreatedFlag, One) == Zero)
                    {
                        try
                        {
                            foreach (QueueClient client in this._queueClients)
                            {
                                client.CreateIfNotExists();
                            }
                        }
                        catch
                        {
                            Interlocked.Exchange(ref this._queusCreatedFlag, Zero);
                            throw;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sends a message, to the high or low priority queue. If there is no
        /// low priority queue, then the second argument is ignored.
        /// </summary>
        public async Task<bool> SendMessageAsync(string message, bool useHighPriorityQueue)
        {
            QueueClient queueClient = this.GetQueueClient(useHighPriorityQueue);

            Azure.Response<SendReceipt> response = await queueClient.SendMessageAsync(message);

            return !string.IsNullOrEmpty(response.Value.MessageId);
        }

        /// <summary>
        /// Send unprocessed messages to the unprocessed queue, that will be told by tbd business process.
        /// </summary>
        /// <param name="message">A message that will be added into the UnprocessedMessageQueue</param>
        /// <returns></returns>
        public async Task<bool> SendUnprocessedMessageAsync(string message)
        {
            unprocessedQueueClient.CreateIfNotExists();

            Azure.Response<SendReceipt> response = await unprocessedQueueClient.SendMessageAsync(message);

            return !string.IsNullOrEmpty(response.Value.MessageId);
        }

        /// <summary>
        /// Gets up to maxMessagesToRetrieve messages from either the high or the low priority queue.
        /// The results are a list of QueueMessage objects.
        /// </summary>
        public IList<Object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue)
        {
            List<Object> messageHooks = new List<Object>();

            QueueClient queueClient = this.GetQueueClient(isHighPriorityQueue);

            QueueMessage[] queueMessages = queueClient.ReceiveMessages(maxMessagesToRetrieve);

            try
            {
                queueMessages = queueMessages.Where(q => q.DequeueCount == 1).ToArray();
            }
            catch (Exception e)
            {
                this.logger?.LogError(e, e.Message);
            }

            if (queueMessages != null)
                messageHooks.AddRange(queueMessages);

            return messageHooks;
        }

        /// <summary>
        /// Gets the message body from the messageHook (i.e. the QueueMessage).
        /// </summary>
        public string GetMessageBody(Object messageHook)
        {
            QueueMessage queueMessage = (QueueMessage)messageHook;
            return queueMessage.Body.ToString();
        }

        /// <summary>
        /// Deletes the given messageHook (i.e. the QueueMessage) from the queue.
        /// </summary>
        public async Task DeleteMessageAsync(Object messageHook, Boolean isHighPriorityQueue)
        {
            QueueClient queueClient = this.GetQueueClient(isHighPriorityQueue);

            QueueMessage queueMessage = (QueueMessage)messageHook;

            if (queueMessage == null) return; // so we don't run past the end of the queue.

            await queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt);
        }

        // Internal

        /// <summary>
        /// Returns the high priority or the low priority queue client.
        /// If there is no low priority queue, the value of the argument is ignored.
        /// </summary>
        private QueueClient GetQueueClient(bool isHighPriorityQueue)
        {
            return isHighPriorityQueue ? this._queueClients[0] : this._queueClients[^1];
        }

        internal QueueClient CreateQueueClient(string queueName, string endPoint)
        {
            return this.azureStorageClientCreator.CreateQueueClient(queueName, endPoint);
        }
    }
}