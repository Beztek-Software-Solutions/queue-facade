// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Threading;

    public class QueueDequeueConfig
    {
        /// <summary>
        /// Default max delivery attempts before moving a message to the unprocessed/poison queue.
        /// </summary>
        public const int DefaultMaxProcessingAttempts = 5;

        public QueueDequeueConfig(
            int maxMessageRate,
            int maxAsynchronousProcesses,
            IQueueProcessorHandler handler,
            CancellationToken cancellationToken,
            int batchSize,
            int pollIntervalInMilliseconds,
            int maxProcessingAttempts = DefaultMaxProcessingAttempts)
        {
            // Validation: ensure that the polling interval is not so small that even an ingestion rate of 1 per polling interval is too much for the max message rate.
            this.MaxMessagesPerPollingInterval = (maxMessageRate * pollIntervalInMilliseconds) / 1000;
            if (this.MaxMessagesPerPollingInterval == 0)
            {
                throw new ArgumentException($"Max message rate of {maxMessageRate} is too small for the polling interval: {pollIntervalInMilliseconds} milliseconds");
            }

            if (maxProcessingAttempts < 1)
            {
                throw new ArgumentException($"{nameof(maxProcessingAttempts)} must be at least 1", nameof(maxProcessingAttempts));
            }

            this.ProcessorHandler = handler;
            this.MaxMessageRate = maxMessageRate;
            this.MaxAsynchronousProcesses = maxAsynchronousProcesses;
            this.BatchSize = batchSize;
            this.PollIntervalMilliseconds = pollIntervalInMilliseconds;
            this.CancellationToken = cancellationToken;
            this.MaxProcessingAttempts = maxProcessingAttempts;
        }

        public IQueueProcessorHandler ProcessorHandler { get; set; }
        public int PollIntervalMilliseconds { get; set; }
        public int MaxMessageRate { get; set; }
        public int MaxAsynchronousProcesses { get; set; }
        public int BatchSize { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public int MaxMessagesPerPollingInterval { get; set; }

        /// <summary>
        /// After this many receives (including the first), a still-failing message is moved to the
        /// unprocessed/poison queue and removed from the primary queue so other work is not blocked.
        /// Visibility timeout (typically ~30s) governs delay between attempts.
        /// </summary>
        public int MaxProcessingAttempts { get; set; }
    }
}
