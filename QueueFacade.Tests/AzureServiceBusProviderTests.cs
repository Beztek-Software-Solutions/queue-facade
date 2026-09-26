// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Beztek.Facade.Queue.Providers;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class AzureServiceBusProviderTests
    {
        private Mock<IAzureServiceBusOps> ops;
        private AzureServiceBusProvider provider;

        [SetUp]
        public void SetUp()
        {
            ops = new Mock<IAzureServiceBusOps>(MockBehavior.Strict);
            var config = new AzureServiceBusProviderConfig(
                "n",
                "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                "q-high",
                "q-low",
                visibilityTimeoutMilliseconds: 60_000);
            provider = new AzureServiceBusProvider(config, ops.Object);
        }

        private static ServiceBusReceivedMessage Msg(string body, int deliveryCount = 1) =>
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(body),
                deliveryCount: deliveryCount);

        [Test]
        public async Task SendMessageAsync_UsesHighQueue()
        {
            ops.Setup(o => o.SendAsync("q-high", "hi")).Returns(Task.CompletedTask);
            Assert.That(await provider.SendMessageAsync("hi", true), Is.True);
            ops.VerifyAll();
        }

        [Test]
        public async Task SendMessageAsync_UsesLowWhenRequested()
        {
            ops.Setup(o => o.SendAsync("q-low", "lo")).Returns(Task.CompletedTask);
            Assert.That(await provider.SendMessageAsync("lo", false), Is.True);
        }

        [Test]
        public async Task SendUnprocessed_UsesPoison()
        {
            ops.Setup(o => o.SendAsync("q-high-unprocessed", "bad")).Returns(Task.CompletedTask);
            Assert.That(await provider.SendUnprocessedMessageAsync("bad"), Is.True);
        }

        [Test]
        public void GetMessages_ReceivesFromHigh()
        {
            var msg = Msg("body", 4);
            ops.Setup(o => o.ReceiveAsync("q-high", 5, It.IsAny<TimeSpan>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage> { msg });
            IList<object> list = provider.GetMessages(5, true);
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(provider.GetMessageBody(list[0]), Is.EqualTo("body"));
            Assert.That(provider.GetReceiveCount(list[0]), Is.EqualTo(4));
        }

        [Test]
        public void GetReceiveCount_NonMessage_ReturnsOne()
        {
            Assert.That(provider.GetReceiveCount("x"), Is.EqualTo(1));
        }

        [Test]
        public void PeekAndReceiveUnprocessed()
        {
            var msg = Msg("p");
            ops.Setup(o => o.PeekAsync("q-high-unprocessed", 3))
                .ReturnsAsync(new List<ServiceBusReceivedMessage> { msg });
            ops.Setup(o => o.ReceiveAsync("q-high-unprocessed", 2, It.IsAny<TimeSpan>()))
                .ReturnsAsync(new List<ServiceBusReceivedMessage> { msg });

            Assert.That(provider.PeekUnprocessedMessages(3).Count, Is.EqualTo(1));
            Assert.That(provider.ReceiveUnprocessedMessages(2).Count, Is.EqualTo(1));
        }

        [Test]
        public async Task DeleteAndDepth()
        {
            var msg = Msg("x");
            ops.Setup(o => o.CompleteAsync("q-high", msg)).Returns(Task.CompletedTask);
            ops.Setup(o => o.CompleteAsync("q-high-unprocessed", msg)).Returns(Task.CompletedTask);
            ops.Setup(o => o.GetActiveMessageCountAsync("q-high")).ReturnsAsync(7);
            ops.Setup(o => o.GetActiveMessageCountAsync("q-high-unprocessed")).ReturnsAsync(2);

            await provider.DeleteMessageAsync(msg, true);
            await provider.DeleteUnprocessedMessageAsync(msg);
            Assert.That(await provider.GetApproximateQueueLength(true), Is.EqualTo(7));
            Assert.That(await provider.GetApproximateUnprocessedQueueLength(), Is.EqualTo(2));
        }

        [Test]
        public async Task Delete_NonMessage_NoOps()
        {
            await provider.DeleteMessageAsync("nope", true);
            await provider.DeleteUnprocessedMessageAsync("nope");
            ops.Verify(o => o.CompleteAsync(It.IsAny<string>(), It.IsAny<ServiceBusReceivedMessage>()), Times.Never);
        }

        [Test]
        public void GetMessageBody_FallbackToString()
        {
            Assert.That(provider.GetMessageBody(42), Is.EqualTo("42"));
        }

        [Test]
        public void CreateIfNotExists_WhenInjected_IsNoOp()
        {
            provider.CreateIfNotExists();
            ops.Verify(o => o.EnsureQueueAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
        }
    }
}
