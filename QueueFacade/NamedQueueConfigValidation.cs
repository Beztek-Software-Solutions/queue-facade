// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;

    /// <summary>
    /// Shared validation for providers that expose high / optional low / poison queue names.
    /// </summary>
    internal static class NamedQueueConfigValidation
    {
        public static void RequireNonEmpty(string value, string message)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(message);
            }
        }

        public static string ResolvePoisonQueue(
            string highPriorityQueue,
            string unprocessedQueue,
            Action<string> validateName)
        {
            string poison = string.IsNullOrWhiteSpace(unprocessedQueue)
                ? Constants.DefaultUnprocessedQueueName(highPriorityQueue, QueueNameValidator.MaxLength)
                : unprocessedQueue.Trim();
            validateName(poison);
            EnsurePoisonDistinct(poison, highPriorityQueue, lowPriorityQueue: null);
            return poison;
        }

        public static string ResolvePoisonQueue(
            string highPriorityQueue,
            string lowPriorityQueue,
            string unprocessedQueue,
            Action<string> validateName)
        {
            string poison = string.IsNullOrWhiteSpace(unprocessedQueue)
                ? Constants.DefaultUnprocessedQueueName(highPriorityQueue, QueueNameValidator.MaxLength)
                : unprocessedQueue.Trim();
            validateName(poison);
            EnsurePoisonDistinct(poison, highPriorityQueue, lowPriorityQueue);
            return poison;
        }

        public static void EnsurePoisonDistinct(string poison, string highPriorityQueue, string lowPriorityQueue)
        {
            if (string.Equals(poison, highPriorityQueue, StringComparison.Ordinal)
                || string.Equals(poison, lowPriorityQueue, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unprocessed (poison) queue name must differ from high/low priority queues");
            }
        }

        public static void ValidatePortableQueueName(string queueName) =>
            QueueNameValidator.ValidateQueueName(QueuePartition.ForValidation(queueName));
    }
}
