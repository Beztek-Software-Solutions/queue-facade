// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Beztek.Facade.Queue.Providers;
    using Google.Cloud.PubSub.V1;
    using Google.Protobuf;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class GooglePubSubProviderTests
    {
        private Mock<IPubSubOps> ops;
        private GooglePubSubProvider provider;

        [SetUp]
        public void SetUp()
        {
            ops = new Mock<IPubSubOps>(MockBehavior.Strict);
            var config = new GooglePubSubProviderConfig(
                "n",
                "proj",
                "q-high",
                "q-low",
                visibilityTimeoutMilliseconds: 60_000);
            provider = new GooglePubSubProvider(config, ops.Object);
        }

        private static ReceivedMessage Hook(string body, string ack = "a1", int attempt = 2) =>
            new ReceivedMessage
            {
                AckId = ack,
                DeliveryAttempt = attempt,
                Message = new PubsubMessage { Data = ByteString.CopyFromUtf8(body) }
            };

        [Test]
        public async Task SendMessageAsync_PublishesHigh()
        {
            ops.Setup(o => o.PublishAsync("proj", "q-high", "hi")).Returns(Task.CompletedTask);
            Assert.That(await provider.SendMessageAsync("hi", true), Is.True);
        }

        [Test]
        public async Task SendMessageAsync_PublishesLow()
        {
            ops.Setup(o => o.PublishAsync("proj", "q-low", "lo")).Returns(Task.CompletedTask);
            Assert.That(await provider.SendMessageAsync("lo", false), Is.True);
        }

        [Test]
        public async Task SendUnprocessed_PublishesPoison()
        {
            ops.Setup(o => o.PublishAsync("proj", "q-high-unprocessed", "bad")).Returns(Task.CompletedTask);
            Assert.That(await provider.SendUnprocessedMessageAsync("bad"), Is.True);
        }

        [Test]
        public void GetMessages_PullsSubscription()
        {
            var hook = Hook("body", attempt: 5);
            ops.Setup(o => o.Pull(
                    It.Is<SubscriptionName>(s => s.SubscriptionId == "q-high-sub"),
                    5,
                    false,
                    It.IsAny<int>()))
                .Returns(new List<object> { hook });

            IList<object> list = provider.GetMessages(5, true);
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(provider.GetMessageBody(list[0]), Is.EqualTo("body"));
            Assert.That(provider.GetReceiveCount(list[0]), Is.EqualTo(5));
        }

        [Test]
        public void GetReceiveCount_NonMessage_ReturnsOne()
        {
            Assert.That(provider.GetReceiveCount("x"), Is.EqualTo(1));
        }

        [Test]
        public void PeekReceiveUnprocessed_AndDepth()
        {
            var hook = Hook("p");
            ops.Setup(o => o.Pull(
                    It.Is<SubscriptionName>(s => s.SubscriptionId == "q-high-unprocessed-sub"),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<int>()))
                .Returns(new List<object> { hook });

            Assert.That(provider.PeekUnprocessedMessages(3).Count, Is.EqualTo(1));
            Assert.That(provider.ReceiveUnprocessedMessages(2).Count, Is.EqualTo(1));
            Assert.That(provider.GetApproximateUnprocessedQueueLength().Result, Is.EqualTo(1));
        }

        [Test]
        public void ApproximateLength_Empty_ReturnsZero()
        {
            ops.Setup(o => o.Pull(
                    It.IsAny<SubscriptionName>(),
                    1,
                    true,
                    It.IsAny<int>()))
                .Returns(new List<object>());
            Assert.That(provider.GetApproximateQueueLength(true).Result, Is.EqualTo(0));
        }

        [Test]
        public async Task DeleteAcks()
        {
            var hook = Hook("x", "ack-9");
            ops.Setup(o => o.AcknowledgeAsync(
                    It.Is<SubscriptionName>(s => s.SubscriptionId == "q-high-sub"),
                    "ack-9"))
                .Returns(Task.CompletedTask);
            ops.Setup(o => o.AcknowledgeAsync(
                    It.Is<SubscriptionName>(s => s.SubscriptionId == "q-high-unprocessed-sub"),
                    "ack-9"))
                .Returns(Task.CompletedTask);

            await provider.DeleteMessageAsync(hook, true);
            await provider.DeleteUnprocessedMessageAsync(hook);
            ops.VerifyAll();
        }

        [Test]
        public void GetMessageBody_Fallback()
        {
            Assert.That(provider.GetMessageBody(7), Is.EqualTo("7"));
        }
    }
}
