// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Google.Api.Gax;
    using Google.Cloud.PubSub.V1;
    using Google.Protobuf;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;

    [ExcludeFromCodeCoverage]
    internal sealed class PubSubOps : IPubSubOps
    {
        private readonly PublisherServiceApiClient publisher;
        private readonly SubscriberServiceApiClient subscriber;
        private readonly ILogger logger;

        internal PubSubOps(ILogger logger = null)
        {
            this.logger = logger;
            publisher = new PublisherServiceApiClientBuilder
            {
                EmulatorDetection = EmulatorDetection.EmulatorOrProduction
            }.Build();
            subscriber = new SubscriberServiceApiClientBuilder
            {
                EmulatorDetection = EmulatorDetection.EmulatorOrProduction
            }.Build();
        }

        /// <summary>Test seam.</summary>
        internal PubSubOps(PublisherServiceApiClient publisher, SubscriberServiceApiClient subscriber, ILogger logger = null)
        {
            this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            this.subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            this.logger = logger;
        }

        public void EnsureTopicAndSubscription(string projectId, string topicId, int ackDeadlineSeconds)
        {
            TopicName topicName = TopicName.FromProjectTopic(projectId, topicId);
            SubscriptionName subscriptionName = SubscriptionName.FromProjectSubscription(projectId, topicId + "-sub");
            try
            {
                publisher.CreateTopic(new Topic { TopicName = topicName });
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
            {
            }

            try
            {
                subscriber.CreateSubscription(new Subscription
                {
                    SubscriptionName = subscriptionName,
                    TopicAsTopicName = topicName,
                    AckDeadlineSeconds = ackDeadlineSeconds
                });
            }
            catch (RpcException ex2) when (ex2.StatusCode == StatusCode.AlreadyExists)
            {
            }

            logger?.LogDebug("Pub/Sub topic/subscription ensured: {Topic}", topicId);
        }

        public async Task PublishAsync(string projectId, string topicId, string message)
        {
            await publisher.PublishAsync(TopicName.FromProjectTopic(projectId, topicId), new PubsubMessage[1]
            {
                new PubsubMessage { Data = ByteString.CopyFromUtf8(message) }
            }).ConfigureAwait(false);
        }

        public IList<object> Pull(SubscriptionName subscription, int max, bool ackDeadlineOnly, int visibilityAckSeconds)
        {
            PullResponse pullResponse = subscriber.Pull(subscription, max);
            List<object> list = PubSubPullHelpers.ToHookList(pullResponse.ReceivedMessages);
            int? deadline = PubSubPullHelpers.DeadlineSecondsForHooks(list, ackDeadlineOnly, visibilityAckSeconds);
            if (deadline.HasValue)
            {
                List<string> ackIds = PubSubPullHelpers.CollectAckIds(list);
                if (ackIds.Count != 0)
                {
                    subscriber.ModifyAckDeadline(subscription, ackIds, deadline.Value);
                }
            }

            return list;
        }

        public async Task AcknowledgeAsync(SubscriptionName subscription, string ackId)
        {
            await subscriber.AcknowledgeAsync(subscription, new string[1] { ackId }).ConfigureAwait(false);
        }
    }
}
