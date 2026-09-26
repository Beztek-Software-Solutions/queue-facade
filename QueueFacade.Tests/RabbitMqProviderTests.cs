// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Beztek.Facade.Queue.Providers;
    using Moq;
    using NUnit.Framework;
    using RabbitMQ.Client;

    [TestFixture]
    public class RabbitMqProviderTests
    {
        private Mock<IChannel> channel;
        private RabbitMqProvider provider;

        [SetUp]
        public void SetUp()
        {
            channel = new Mock<IChannel>(MockBehavior.Strict);
            var config = new RabbitMqProviderConfig(
                "n",
                "amqp://guest:guest@localhost:5672/",
                "q-high",
                visibilityTimeoutMilliseconds: 60_000);
            provider = new RabbitMqProvider(config, channel.Object);
        }

        [Test]
        public async Task SendMessageAsync_Publishes()
        {
            channel.Setup(c => c.BasicPublishAsync(
                    string.Empty,
                    "q-high",
                    false,
                    It.IsAny<BasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask());

            Assert.That(await provider.SendMessageAsync("hi", true), Is.True);
        }

        [Test]
        public void GetMessages_BuildsHooksFromBasicGet()
        {
            var props = new Mock<IReadOnlyBasicProperties>(MockBehavior.Loose);
            props.SetupGet(p => p.Headers).Returns(new Dictionary<string, object> { ["x-attempt"] = "2" });
            byte[] body = Encoding.UTF8.GetBytes("payload");
            var got = new BasicGetResult(9, false, "ex", "rk", 1, props.Object, body);

            channel.SetupSequence(c => c.BasicGetAsync("q-high", false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(got)
                .ReturnsAsync((BasicGetResult)null);

            IList<object> msgs = provider.GetMessages(5, true);
            Assert.That(msgs.Count, Is.EqualTo(1));
            Assert.That(provider.GetMessageBody(msgs[0]), Is.EqualTo("payload"));
            Assert.That(provider.GetReceiveCount(msgs[0]), Is.EqualTo(2));
        }

        [Test]
        public async Task DeleteMessageAsync_Acks()
        {
            var hook = new RabbitMqProvider.RabbitMqHook(11, "b", 1, "q-high");
            channel.Setup(c => c.BasicAckAsync(11UL, false, It.IsAny<CancellationToken>()))
                .Returns(new ValueTask());
            await provider.DeleteMessageAsync(hook, true);
            Assert.That(hook.Completed, Is.True);
        }

        [Test]
        public async Task GetApproximateQueueLength_UsesPassiveDeclare()
        {
            var ok = new QueueDeclareOk("q-high", 5, 0);

            channel.Setup(c => c.QueueDeclarePassiveAsync("q-high", It.IsAny<CancellationToken>()))
                .ReturnsAsync(ok);

            Assert.That(await provider.GetApproximateQueueLength(true), Is.EqualTo(5));
        }

        [Test]
        public async Task SendUnprocessed_AndPoisonDepth()
        {
            channel.Setup(c => c.BasicPublishAsync(
                    string.Empty,
                    "q-high-unprocessed",
                    false,
                    It.IsAny<BasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask());
            Assert.That(await provider.SendUnprocessedMessageAsync("bad"), Is.True);

            var ok = new QueueDeclareOk("q-high-unprocessed", 3, 0);
            channel.Setup(c => c.QueueDeclarePassiveAsync("q-high-unprocessed", It.IsAny<CancellationToken>()))
                .ReturnsAsync(ok);
            Assert.That(await provider.GetApproximateUnprocessedQueueLength(), Is.EqualTo(3));
        }

        [Test]
        public void PeekAndReceiveUnprocessed()
        {
            channel.Setup(c => c.BasicGetAsync("q-high-unprocessed", false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BasicGetResult)null);
            Assert.That(provider.PeekUnprocessedMessages(2).Count, Is.EqualTo(0));
            Assert.That(provider.ReceiveUnprocessedMessages(2).Count, Is.EqualTo(0));
        }

        [Test]
        public async Task DeleteUnprocessed_Acks()
        {
            var hook = new RabbitMqProvider.RabbitMqHook(12, "b", 1, "q-high-unprocessed");
            channel.Setup(c => c.BasicAckAsync(12UL, false, It.IsAny<CancellationToken>()))
                .Returns(new ValueTask());
            await provider.DeleteUnprocessedMessageAsync(hook);
            Assert.That(hook.Completed, Is.True);
        }

        [Test]
        public void GetReceiveCount_NonHook_ReturnsOne()
        {
            Assert.That(provider.GetReceiveCount("x"), Is.EqualTo(1));
        }

        [Test]
        public void GetMessageBody_Fallback()
        {
            Assert.That(provider.GetMessageBody(9), Is.EqualTo("9"));
        }

        [Test]
        public async Task Delete_NonHook_NoAck()
        {
            await provider.DeleteMessageAsync("nope", true);
            channel.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

    }
}