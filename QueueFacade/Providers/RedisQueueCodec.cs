// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Globalization;
    using StackExchange.Redis;

    /// <summary>Encode/decode helpers for Redis ready and processing list payloads.</summary>
    internal static class RedisQueueCodec
    {
        internal static string EncodeReady(string body, int attempt) =>
            attempt.ToString(CultureInfo.InvariantCulture) + "|" + body;

        internal static string EncodeProcessing(string body, int attempt, long visibleAtUnixMs) =>
            visibleAtUnixMs.ToString(CultureInfo.InvariantCulture)
            + "|" + attempt.ToString(CultureInfo.InvariantCulture)
            + "|" + body;

        internal static bool TryParseReady(RedisValue value, out string body, out int attempt)
        {
            body = null;
            attempt = 1;
            if (value.IsNullOrEmpty)
            {
                return false;
            }

            string s = value.ToString();
            int idx = s.IndexOf('|');
            if (idx <= 0)
            {
                return false;
            }

            if (!int.TryParse(s.AsSpan(0, idx), NumberStyles.Integer, CultureInfo.InvariantCulture, out attempt))
            {
                return false;
            }

            body = s[(idx + 1)..];
            return true;
        }

        internal static bool TryParseProcessing(RedisValue value, out long visibleAt, out string body, out int attempt)
        {
            visibleAt = 0;
            body = null;
            attempt = 1;
            if (value.IsNullOrEmpty)
            {
                return false;
            }

            string s = value.ToString();
            int first = s.IndexOf('|');
            int second = first < 0 ? -1 : s.IndexOf('|', first + 1);
            if (first <= 0 || second <= first)
            {
                return false;
            }

            if (!long.TryParse(s.AsSpan(0, first), NumberStyles.Integer, CultureInfo.InvariantCulture, out visibleAt))
            {
                return false;
            }

            if (!int.TryParse(s.AsSpan(first + 1, second - first - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out attempt))
            {
                return false;
            }

            body = s[(second + 1)..];
            return true;
        }

        internal static bool IsExpired(long visibleAtUnixMs, long nowUnixMs) => nowUnixMs >= visibleAtUnixMs;

        /// <summary>
        /// Ready-list key. Hash tag <c>{queue}</c> keeps ready+proc on one Redis Cluster / ElastiCache slot.
        /// </summary>
        internal static string ReadyKey(string queue) => "qf:{" + queue + "}:ready";

        /// <summary>Processing-list key (same hash tag as <see cref="ReadyKey"/>).</summary>
        internal static string ProcKey(string queue) => "qf:{" + queue + "}:proc";
    }
}

