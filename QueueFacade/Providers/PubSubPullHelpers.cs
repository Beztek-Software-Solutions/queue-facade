// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Collections.Generic;
    using Google.Cloud.PubSub.V1;

    /// <summary>Pub/Sub pull response shaping (unit-tested without gRPC).</summary>
    internal static class PubSubPullHelpers
    {
        internal static int ClampTake(int max, int maxPerPoll) =>
            Math.Min(Math.Max(max, 1), maxPerPoll);

        internal static int AckDeadlineSeconds(int visibilityTimeoutMilliseconds) =>
            Math.Max(10, visibilityTimeoutMilliseconds / 1000);

        internal static List<object> ToHookList(IEnumerable<ReceivedMessage> messages)
        {
            var list = new List<object>();
            if (messages == null)
            {
                return list;
            }

            foreach (ReceivedMessage msg in messages)
            {
                if (msg != null)
                {
                    list.Add(msg);
                }
            }

            return list;
        }

        internal static List<string> CollectAckIds(IReadOnlyList<object> hooks)
        {
            var ackIds = new List<string>();
            if (hooks == null)
            {
                return ackIds;
            }

            foreach (object item in hooks)
            {
                if (item is ReceivedMessage rm && !string.IsNullOrEmpty(rm.AckId))
                {
                    ackIds.Add(rm.AckId);
                }
            }

            return ackIds;
        }

        /// <summary>
        /// Returns the ModifyAckDeadline seconds to apply, or null when nothing should be sent.
        /// <paramref name="releaseImmediately"/> maps to deadline 0 (used for depth sampling).
        /// </summary>
        internal static int? DeadlineSecondsForHooks(IReadOnlyList<object> hooks, bool releaseImmediately, int visibilityAckSeconds)
        {
            if (hooks == null || hooks.Count == 0)
            {
                return null;
            }

            return releaseImmediately ? 0 : visibilityAckSeconds;
        }
    }
}
