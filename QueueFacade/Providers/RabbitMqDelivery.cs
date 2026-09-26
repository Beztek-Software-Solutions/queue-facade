// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections.Generic;

    /// <summary>RabbitMQ delivery-count header parsing (unit-tested without a broker).</summary>
    internal static class RabbitMqDelivery
    {
        internal const string AttemptHeader = "x-attempt";

        internal static int ParseAttempt(IDictionary<string, object> headers)
        {
            if (headers == null
                || !headers.TryGetValue(AttemptHeader, out object raw)
                || raw == null
                || !int.TryParse(raw.ToString(), out int parsed))
            {
                return 1;
            }

            return Math.Max(1, parsed);
        }

        internal static int ClampTake(int max, int maxPerPoll) =>
            Math.Min(Math.Max(max, 1), maxPerPoll);
    }
}
