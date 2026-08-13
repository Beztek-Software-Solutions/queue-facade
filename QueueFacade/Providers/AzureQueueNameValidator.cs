// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    /// <summary>
    /// Legacy name — delegates to portable <see cref="QueueNameValidator"/>.
    /// </summary>
    internal static class AzureQueueNameValidator
    {
        public static void ValidateQueueName(string queueName) =>
            QueueNameValidator.ValidateQueueName(queueName);
    }
}
