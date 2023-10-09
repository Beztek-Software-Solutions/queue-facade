// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Provides a standard set of errors that could be thrown from the client library.
    /// </summary>
    internal static class AzureQueueNameValidator
    {
        private static readonly RegexOptions RegexOptions = RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant;

        // Azure validaion constants
        private const int AzureQueueNameMinLength = 3;
        private const int AzureQueueNameMaxLength = 63;
        private static readonly Regex AzureQueueNameRegex = new Regex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions);

        private static readonly List<string> ReservedQueueNames = new List<string>() { "test" };

        /// <summary>
        /// Checks if a queue name is valid. We will choose the common denominator requirements for all providers to validate queue names consistently
        /// </summary>
        /// <param name="queueName">A string representing the queue name to validate.</param>
        public static void ValidateQueueName(string queueName)
        {
            // The following are requirments for Azure Queue Storage

            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentException(Constants.ResourceNameEmpty);
            }

            if (queueName.Length < AzureQueueNameMinLength || queueName.Length > AzureQueueNameMaxLength)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Constants.InvalidResourceNameLength, AzureQueueNameMinLength, AzureQueueNameMaxLength));
            }

            if (!AzureQueueNameRegex.IsMatch(queueName))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Constants.InvalidResourceName, queueName));
            }

            if (ReservedQueueNames.Contains(queueName))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    Constants.InvalidResourceReservedName, queueName));
            }
        }
    }
}