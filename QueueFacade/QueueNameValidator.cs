// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Portable queue naming — common denominator of Azure Queue Storage and AWS SQS
    /// so the same names work when switching providers.
    /// <list type="bullet">
    /// <item>3–63 characters</item>
    /// <item>lowercase letters, digits, and single hyphens between segments</item>
    /// <item>no underscores, no uppercase, no consecutive hyphens</item>
    /// </list>
    /// </summary>
    public static class QueueNameValidator
    {
        public const int MinLength = 3;
        public const int MaxLength = 63;

        private static readonly Regex QueueNameRegex = new(
            "^[a-z0-9]+(-[a-z0-9]+)*$",
            RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);

        private static readonly HashSet<string> ReservedQueueNames = new(StringComparer.Ordinal)
        {
            "test",
        };

        /// <summary>
        /// Validates a fully resolved queue name (or a template after <see cref="QueuePartition.ForValidation"/>).
        /// </summary>
        public static void ValidateQueueName(string queueName)
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentException(Constants.ResourceNameEmpty);
            }

            if (queueName.EndsWith(".fifo", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, Constants.InvalidResourceName, queueName)
                    + " FIFO queues (.fifo) are not supported.");
            }

            if (queueName.Length < MinLength || queueName.Length > MaxLength)
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.InvariantCulture,
                    Constants.InvalidResourceNameLength,
                    MinLength,
                    MaxLength));
            }

            if (!QueueNameRegex.IsMatch(queueName))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.InvariantCulture, Constants.InvalidResourceName, queueName));
            }

            if (ReservedQueueNames.Contains(queueName))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.InvariantCulture,
                    Constants.InvalidResourceReservedName,
                    queueName));
            }
        }
    }
}
