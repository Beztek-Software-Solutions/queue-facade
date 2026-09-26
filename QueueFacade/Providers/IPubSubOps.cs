// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Google.Cloud.PubSub.V1;

    /// <summary>Pub/Sub transport surface (mockable in unit tests).</summary>
    internal interface IPubSubOps
    {
        void EnsureTopicAndSubscription(string projectId, string topicId, int ackDeadlineSeconds);

        Task PublishAsync(string projectId, string topicId, string message);

        IList<object> Pull(SubscriptionName subscription, int max, bool ackDeadlineOnly, int visibilityAckSeconds);

        Task AcknowledgeAsync(SubscriptionName subscription, string ackId);
    }
}
