// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Threading;

    /// <summary>
    /// Settings for an <see cref="IQueueClient"/> dequeue-and-process loop.
    /// </summary>
    public class QueueDequeueConfig
    {
        /// <summary>
        /// Default max delivery attempts before moving a message to the unprocessed/poison queue.
        /// </summary>
        public const int DefaultMaxProcessingAttempts = 5;

        /// <summary>
        /// Creates dequeue settings and validates that <paramref name="maxMessageRate"/> is compatible
        /// with <paramref name="pollIntervalInMilliseconds"/>.
        /// </summary>
        /// <param name="maxMessageRate">Target messages per second.</param>
        /// <param name="maxAsynchronousProcesses">Max concurrent in-flight process tasks.</param>
        /// <param name="handler">Routes messages to typed processors.</param>
        /// <param name="cancellationToken">Stops the dequeue loop when cancelled.</param>
        /// <param name="batchSize">Messages per process batch.</param>
        /// <param name="pollIntervalInMilliseconds">Idle poll interval when the queue is empty.</param>
        /// <param name="maxProcessingAttempts">Attempts before poison-queue move.</param>
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

        /// <summary>Routes messages to typed <see cref="IMessageProcessor"/> instances.</summary>
        public IQueueProcessorHandler ProcessorHandler { get; set; }

        /// <summary>Idle poll interval when the queue is empty, in milliseconds.</summary>
        public int PollIntervalMilliseconds { get; set; }

        /// <summary>Target messages processed per second.</summary>
        public int MaxMessageRate { get; set; }

        /// <summary>Max concurrent in-flight process tasks.</summary>
        public int MaxAsynchronousProcesses { get; set; }

        /// <summary>Messages grouped per process invocation.</summary>
        public int BatchSize { get; set; }

        /// <summary>Stops the dequeue loop when cancelled.</summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>Derived max messages allowed per poll interval from rate × interval.</summary>
        public int MaxMessagesPerPollingInterval { get; set; }

        /// <summary>
        /// After this many receives (including the first), a still-failing message is moved to the
        /// unprocessed/poison queue and removed from the primary queue so other work is not blocked.
        /// Visibility timeout (typically ~30s) governs delay between attempts.
        /// </summary>
        public int MaxProcessingAttempts { get; set; }
    }
}
