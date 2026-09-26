// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;

    /// <summary>
    /// Configuration for Google Cloud Pub/Sub pull subscriptions.
    /// </summary>
    /// <remarks>
    /// Pub/Sub is not a classic queue: peek is lease-based (same as receive), and approximate
    /// depth is a weak sample (0/1-style). Prefer Azure Queue / SQS / Service Bus / RabbitMQ
    /// when exact depth or non-destructive peek matter.
    /// </remarks>
    public class GooglePubSubProviderConfig : INamedQueueProviderConfig
    {
        public GooglePubSubProviderConfig(
            string name,
            string projectId,
            string highPriorityQueue,
            string lowPriorityQueue = null,
            int visibilityTimeoutMilliseconds = 30000,
            string unprocessedQueue = null,
            string emulatorHost = null)
        {
            NamedQueueConfigValidation.RequireNonEmpty(name, "No name given");
            NamedQueueConfigValidation.RequireNonEmpty(projectId, "No project id given");
            NamedQueueConfigValidation.RequireNonEmpty(highPriorityQueue, "No queue name provided");
            NamedQueueConfigValidation.ValidatePortableQueueName(highPriorityQueue);
            if (lowPriorityQueue != null)
            {
                NamedQueueConfigValidation.ValidatePortableQueueName(lowPriorityQueue);
            }

            Name = name;
            ProjectId = projectId;
            HighPriorityQueue = highPriorityQueue;
            LowPriorityQueue = lowPriorityQueue;
            UnprocessedQueue = NamedQueueConfigValidation.ResolvePoisonQueue(
                highPriorityQueue, lowPriorityQueue, unprocessedQueue, NamedQueueConfigValidation.ValidatePortableQueueName);
            VisibilityTimeoutMilliseconds = visibilityTimeoutMilliseconds;
            EmulatorHost = emulatorHost;
        }

        public QueueProviderType QueueProviderType { get; } = QueueProviderType.GooglePubSub;

        public string Name { get; set; }

        public int VisibilityTimeoutMilliseconds { get; set; }

        public string ProjectId { get; set; }

        /// <summary>Topic name used as the high-priority work queue (subscription = topic + "-sub").</summary>
        public string HighPriorityQueue { get; set; }

        public string LowPriorityQueue { get; }

        public string UnprocessedQueue { get; }

        /// <summary>Optional Pub/Sub emulator host (<c>host:port</c>). Sets <c>PUBSUB_EMULATOR_HOST</c> for this provider.</summary>
        public string EmulatorHost { get; set; }
    }
}
