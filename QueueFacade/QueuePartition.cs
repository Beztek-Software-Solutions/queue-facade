// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Multi-tenant queue naming: embed <c>{partition}</c> in queue name templates
    /// (typically a customer / tenant id) and resolve via <see cref="IPartitionedQueueClient"/>.
    /// Partition keys follow the same portable rules as <see cref="QueueNameValidator"/> (lowercase, hyphens).
    /// </summary>
    public static class QueuePartition
    {
        public const string Token = "{partition}";

        /// <summary>Sample segment used only when validating templates that contain <see cref="Token"/>.</summary>
        internal const string ValidationPlaceholder = "partition0";

        private static readonly Regex SafePartitionKey = new(
            "^[a-z0-9]+(-[a-z0-9]+)*$",
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

        public static bool ContainsToken(string value) =>
            !string.IsNullOrEmpty(value)
            && value.IndexOf(Token, StringComparison.Ordinal) >= 0;

        /// <summary>
        /// Normalizes a tenant/customer partition key for portable cloud queue names (always lowercase).
        /// </summary>
        public static string NormalizePartitionKey(string partitionKey)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
            {
                throw new ArgumentException("Partition key is required");
            }

            string key = partitionKey.Trim().ToLowerInvariant();

            if (key.Length < 1 || key.Length > QueueNameValidator.MaxLength)
            {
                throw new ArgumentException(
                    $"Partition key must be 1–{QueueNameValidator.MaxLength} characters");
            }

            if (!SafePartitionKey.IsMatch(key))
            {
                throw new ArgumentException(
                    "Partition key must be lowercase alphanumeric with single hyphens (no underscores); must start and end alphanumeric");
            }

            return key;
        }

        /// <summary>Obsolete overload; partition keys are always lowercased for portability.</summary>
        [Obsolete("Partition keys are always lowercased; use NormalizePartitionKey(string).")]
        public static string NormalizePartitionKey(string partitionKey, bool forceLowercase) =>
            NormalizePartitionKey(partitionKey);

        public static string Resolve(string template, string normalizedPartitionKey)
        {
            if (string.IsNullOrEmpty(template))
            {
                return template;
            }

            if (!ContainsToken(template))
            {
                return template;
            }

            return template.Replace(Token, normalizedPartitionKey, StringComparison.Ordinal);
        }

        /// <summary>
        /// Replaces <c>{partition}</c> with a placeholder so <see cref="QueueNameValidator"/> can run on templates.
        /// </summary>
        internal static string ForValidation(string templateOrName)
        {
            if (!ContainsToken(templateOrName))
            {
                return templateOrName;
            }

            return templateOrName.Replace(Token, ValidationPlaceholder, StringComparison.Ordinal);
        }
    }
}
