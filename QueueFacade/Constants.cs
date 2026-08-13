// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;

    /// <summary>
    /// Queue Facade Constants
    /// </summary>
    internal static class Constants
    {
        public const string InvalidResourceName = "Invalid queue name. {0}";
        public const string InvalidResourceNameLength = "Invalid queue name length. The queue name must be between {0} and {1} characters long.";
        public const string InvalidResourceReservedName = "Invalid {0} name. This {0} name is reserved.";
        public const string ResourceNameEmpty = "Invalid queue name. The queue name may not be null, empty, or whitespace only.";

        /// <summary>Legacy global poison queue name (prefer <see cref="DefaultUnprocessedQueueName"/>).</summary>
        public const string UnprocessedMessageQueue = "unprocessedmessagequeue";

        public const string UnprocessedQueueSuffix = "-unprocessed";

        /// <summary>
        /// Per-client poison queue: <c>{highPriorityQueue}-unprocessed</c>, truncated to <paramref name="maxLength"/>.
        /// Avoids colliding when multiple apps share one cloud account.
        /// </summary>
        public static string DefaultUnprocessedQueueName(string highPriorityQueue, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(highPriorityQueue))
            {
                throw new ArgumentException(ResourceNameEmpty);
            }

            string candidate = highPriorityQueue + UnprocessedQueueSuffix;
            if (candidate.Length <= maxLength)
            {
                return candidate;
            }

            int maxHigh = maxLength - UnprocessedQueueSuffix.Length;
            if (maxHigh < 1)
            {
                throw new ArgumentException(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    InvalidResourceNameLength,
                    1,
                    maxLength));
            }

            return highPriorityQueue.Substring(0, maxHigh) + UnprocessedQueueSuffix;
        }
    }
}
