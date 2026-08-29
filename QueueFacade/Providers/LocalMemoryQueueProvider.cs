// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    internal class LocalMemoryQueueProvider : IQueueProvider
    {
        public const int SixtyFourKiloBytes = 65536;
        public const int OneThousand = 1000;

        private ILogger logger;
        private readonly List<string> highPriorityQueue;
        private readonly List<string> lowPriorityQueue;
        private readonly List<string> unprocessedQueue;
        private readonly ConcurrentDictionary<string, DateTime> highPriorityProcessingQueue;
        private readonly ConcurrentDictionary<string, DateTime> lowPriorityProcessingQueue;
        internal readonly ConcurrentDictionary<string, int> receiveCounts;

        internal LocalMemoryQueueProvider(ILogger logger = null, bool implementUnhide = true, int visibilityTimeoutMilliseconds = 30000)
        {
            this.logger = logger;
            this.highPriorityQueue = new List<string>();
            this.lowPriorityQueue = new List<string>();
            this.unprocessedQueue = new List<string>();
            this.highPriorityProcessingQueue = new ConcurrentDictionary<string, DateTime>();
            this.lowPriorityProcessingQueue = new ConcurrentDictionary<string, DateTime>();
            this.receiveCounts = new ConcurrentDictionary<string, int>();
            if (implementUnhide)
            {
                _ = this.UnhideMessagesDaemon(true);
                _ = this.UnhideMessagesDaemon(false);
                this.VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
            }
        }

        public int MaxMessageSize { get; set; } = SixtyFourKiloBytes;

        public int MaxMessageCountPerPoll { get; set; } = OneThousand;

        public bool HasLowPriorityQueue { get; set; } = true;

        internal int VisibilityTimeoutMilliseconds { get; set; } = 30000;

        internal int UhhideCheckPeriodMilliseconds { get; set; } = 1000;

        internal bool enqueueFailFlag { get; set; } = false;

        public void CreateIfNotExists()
        {
            // Nothing to do here. The constructor took care of this!
        }

        public async Task DeleteMessageAsync(object messageHook, bool isHighPriorityQueue)
        {
            string key = messageHook.ToString();
            if (isHighPriorityQueue)
            {
                this.highPriorityProcessingQueue.TryRemove(key, out DateTime value);
            }
            else
            {
                this.lowPriorityProcessingQueue.TryRemove(key, out DateTime value);
            }

            this.receiveCounts.TryRemove(key, out _);
        }

        public string GetMessageBody(object messageHook)
        {
            return messageHook.ToString();
        }

        public int GetReceiveCount(object messageHook)
        {
            string key = messageHook?.ToString();
            if (key == null)
            {
                return 1;
            }

            return this.receiveCounts.TryGetValue(key, out int count) ? Math.Max(1, count) : 1;
        }

        public IList<object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue)
        {
            List<object> result = new List<object>();
            if (isHighPriorityQueue)
            {
                result.AddRange(this.highPriorityQueue.GetRange(0, Math.Min(highPriorityQueue.Count, maxMessagesToRetrieve)));
                foreach (object message in new List<object>(result))
                {
                    string key = message.ToString();
                    DateTime dateTime = DateTime.Now.AddMilliseconds(this.VisibilityTimeoutMilliseconds);
                    highPriorityProcessingQueue.AddOrUpdate(key, dateTime, (Key, OldValue) => dateTime);
                    receiveCounts.AddOrUpdate(key, 1, (_, prior) => prior + 1);
                }
                highPriorityQueue.RemoveAll(i => result.Contains(i));
            }
            else
            {
                result.AddRange(this.lowPriorityQueue.GetRange(0, Math.Min(lowPriorityQueue.Count, maxMessagesToRetrieve)));
                foreach (object message in new List<object>(result))
                {
                    string key = message.ToString();
                    DateTime dateTime = DateTime.Now.AddMilliseconds(this.VisibilityTimeoutMilliseconds);
                    lowPriorityProcessingQueue.AddOrUpdate(key, dateTime, (Key, OldValue) => dateTime);
                    receiveCounts.AddOrUpdate(key, 1, (_, prior) => prior + 1);
                }
                lowPriorityQueue.RemoveAll(i => result.Contains(i));
            }

            return result;
        }

        public IList<object> PeekUnprocessedMessages(int maxMessagesToRetrieve)
        {
            int take = Math.Min(Math.Max(maxMessagesToRetrieve, 0), this.unprocessedQueue.Count);
            return this.unprocessedQueue.GetRange(0, take).Cast<object>().ToList();
        }

        public IList<object> ReceiveUnprocessedMessages(int maxMessagesToRetrieve)
        {
            // LocalMemory: same as peek — Requeue removes explicitly via DeleteUnprocessedMessageAsync.
            return PeekUnprocessedMessages(maxMessagesToRetrieve);
        }

        public async Task DeleteUnprocessedMessageAsync(object messageHook)
        {
            string body = messageHook?.ToString();
            if (body != null)
            {
                this.unprocessedQueue.Remove(body);
            }
        }

        public async Task<bool> SendMessageAsync(string message, bool useHighPriorityQueue)
        {
            if (!enqueueFailFlag)
            {
                if (useHighPriorityQueue)
                {
                    this.highPriorityQueue.Add(message);
                }
                else
                {
                    this.lowPriorityQueue.Add(message);
                }
            }

            return !enqueueFailFlag;
        }

        public async Task<bool> SendUnprocessedMessageAsync(string message)
        {
            if (!enqueueFailFlag)
            {
                this.unprocessedQueue.Add(message);
            }

            return !enqueueFailFlag;
        }

        public async Task<int> GetNumUnprocessedMessages()
        {
            return this.unprocessedQueue.Count;
        }

        public async Task<long> GetApproximateUnprocessedQueueLength()
        {
            return this.unprocessedQueue.Count;
        }

        public async Task<long> GetApproximateQueueLength(bool isHighPriorityQueue)
        {
            return (long)GetNumMessages(isHighPriorityQueue);
        }

        // Internal

        internal int GetNumMessages(bool isHighPriorityQueue)
        {
            if (isHighPriorityQueue)
            {
                return this.highPriorityQueue.Count;
            }
            else
            {
                return this.lowPriorityQueue.Count;
            }
        }

        internal int GetNumProcessingMessages(bool isHighPriorityQueue)
        {
            if (isHighPriorityQueue)
            {
                return this.highPriorityProcessingQueue.Count;
            }
            else
            {
                return this.lowPriorityProcessingQueue.Count;
            }
        }

        // Private

        // Unhide processing messages after the hide time period
        private async Task UnhideMessagesDaemon(bool isHighPriorityQueue)
        {
            await Task.Run(async () => {
                while (true)
                {
                    logger?.LogTrace("UnhideMessagesDaemon - running");
                    try
                    {
                        ConcurrentDictionary<string, DateTime> processingDictionary = isHighPriorityQueue ? highPriorityProcessingQueue : lowPriorityProcessingQueue;
                        List<string> visibleQueue = isHighPriorityQueue ? highPriorityQueue : lowPriorityQueue;
                        foreach (KeyValuePair<string, DateTime> entry in new Dictionary<string, DateTime>(processingDictionary))
                        {
                            if (DateTime.Now > entry.Value)
                            {
                                string key = entry.Key;
                                visibleQueue.Add(key);
                                processingDictionary.TryRemove(key, out DateTime value);
                                logger?.LogTrace($"Unhiding key {key}");
                            }
                        }

                        await Task.Delay(UhhideCheckPeriodMilliseconds).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Exception trying to unhide messages");
                    }
                }
            }).ConfigureAwait(false);
        }
    }
}
