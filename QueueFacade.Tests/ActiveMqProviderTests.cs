// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Apache.NMS;
    using Beztek.Facade.Queue.Providers;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class ActiveMqProviderTests
    {
        private Mock<ISession> session;
        private Mock<IQueue> queue;
        private ActiveMqProvider provider;

        [SetUp]
        public void SetUp()
        {
            session = new Mock<ISession>(MockBehavior.Strict);
            queue = new Mock<IQueue>(MockBehavior.Loose);
            var config = new ActiveMqProviderConfig("n", "tcp://localhost:61616", "q-high", "q-low");
            provider = new ActiveMqProvider(config, session.Object);
        }

        [Test]
        public async Task SendMessageAsync_SendsPersistentText()
        {
            var producer = new Mock<IMessageProducer>(MockBehavior.Strict);
            var text = new Mock<ITextMessage>(MockBehavior.Loose);
            session.Setup(s => s.GetQueue("q-high")).Returns(queue.Object);
            session.Setup(s => s.CreateProducer(queue.Object)).Returns(producer.Object);
            session.Setup(s => s.CreateTextMessage("hi")).Returns(text.Object);
            producer.SetupSet(p => p.DeliveryMode = MsgDeliveryMode.Persistent);
            producer.Setup(p => p.Send(text.Object));
            producer.Setup(p => p.Dispose());

            Assert.That(await provider.SendMessageAsync("hi", true), Is.True);
            producer.Verify(p => p.Send(text.Object), Times.Once);
        }

        [Test]
        public void GetMessages_ReceivesUntilNull()
        {
            var consumer = new Mock<IMessageConsumer>(MockBehavior.Strict);
            var msg = new Mock<ITextMessage>(MockBehavior.Loose);
            msg.SetupGet(m => m.Text).Returns("payload");
            var props = new Mock<IPrimitiveMap>(MockBehavior.Loose);
            props.Setup(p => p.GetInt("JMSXDeliveryCount")).Returns(2);
            msg.SetupGet(m => m.Properties).Returns(props.Object);

            session.Setup(s => s.GetQueue("q-high")).Returns(queue.Object);
            session.Setup(s => s.CreateConsumer(queue.Object)).Returns(consumer.Object);
            consumer.SetupSequence(c => c.Receive(It.IsAny<TimeSpan>()))
                .Returns(msg.Object)
                .Returns((IMessage)null);
            consumer.Setup(c => c.Dispose());

            IList<object> list = provider.GetMessages(5, true);
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(provider.GetMessageBody(list[0]), Is.EqualTo("payload"));
            Assert.That(provider.GetReceiveCount(list[0]), Is.EqualTo(2));
        }

        [Test]
        public async Task DeleteMessageAsync_Acknowledges()
        {
            var msg = new Mock<IMessage>(MockBehavior.Strict);
            msg.Setup(m => m.Acknowledge());
            await provider.DeleteMessageAsync(msg.Object, true);
            msg.Verify(m => m.Acknowledge(), Times.Once);
        }

        [Test]
        public async Task GetApproximateQueueLength_BrowsersCount()
        {
            var browser = new Mock<IQueueBrowser>(MockBehavior.Strict);
            var list = new ArrayList { new object(), new object(), new object() };
            session.Setup(s => s.GetQueue("q-high")).Returns(queue.Object);
            session.Setup(s => s.CreateBrowser(queue.Object)).Returns(browser.Object);
            browser.Setup(b => b.GetEnumerator()).Returns(list.GetEnumerator());
            browser.Setup(b => b.Dispose());

            Assert.That(await provider.GetApproximateQueueLength(true), Is.EqualTo(3));
        }

        [Test]
        public async Task SendUnprocessed_AndDepthPoison()
        {
            var producer = new Mock<IMessageProducer>(MockBehavior.Loose);
            var text = new Mock<ITextMessage>(MockBehavior.Loose);
            session.Setup(s => s.GetQueue("q-high-unprocessed")).Returns(queue.Object);
            session.Setup(s => s.CreateProducer(queue.Object)).Returns(producer.Object);
            session.Setup(s => s.CreateTextMessage("p")).Returns(text.Object);

            Assert.That(await provider.SendUnprocessedMessageAsync("p"), Is.True);

            var browser = new Mock<IQueueBrowser>(MockBehavior.Loose);
            browser.Setup(b => b.GetEnumerator()).Returns(new ArrayList().GetEnumerator());
            session.Setup(s => s.CreateBrowser(queue.Object)).Returns(browser.Object);
            Assert.That(await provider.GetApproximateUnprocessedQueueLength(), Is.EqualTo(0));
        }

        [Test]
        public async Task ProbeDepth_OnBrowseFailure_ReturnsZero()
        {
            session.Setup(s => s.GetQueue("q-high")).Throws(new InvalidOperationException("no browse"));
            Assert.That(await provider.GetApproximateQueueLength(true), Is.EqualTo(0));
        }

        [Test]
        public async Task ReceiveUnprocessed_AndDelete()
        {
            var consumer = new Mock<IMessageConsumer>(MockBehavior.Strict);
            var msg = new Mock<ITextMessage>(MockBehavior.Loose);
            msg.SetupGet(m => m.Text).Returns("poison");
            var props = new Mock<IPrimitiveMap>(MockBehavior.Loose);
            props.Setup(p => p.GetInt("JMSXDeliveryCount")).Throws(new InvalidOperationException());
            msg.SetupGet(m => m.Properties).Returns(props.Object);
            msg.Setup(m => m.Acknowledge());

            session.Setup(s => s.GetQueue("q-high-unprocessed")).Returns(queue.Object);
            session.Setup(s => s.CreateConsumer(queue.Object)).Returns(consumer.Object);
            consumer.SetupSequence(c => c.Receive(It.IsAny<TimeSpan>()))
                .Returns(msg.Object)
                .Returns((IMessage)null);
            consumer.Setup(c => c.Dispose());

            Assert.That(provider.PeekUnprocessedMessages(2).Count, Is.EqualTo(1));
            Assert.That(provider.GetReceiveCount(msg.Object), Is.EqualTo(1));
            await provider.DeleteUnprocessedMessageAsync(msg.Object);
            msg.Verify(m => m.Acknowledge(), Times.Once);
        }

        [Test]
        public void GetReceiveCount_NonText_ReturnsOne()
        {
            Assert.That(provider.GetReceiveCount("x"), Is.EqualTo(1));
        }

        [Test]
        public void GetMessageBody_Fallback()
        {
            Assert.That(provider.GetMessageBody(3), Is.EqualTo("3"));
        }

        [Test]
        public async Task ProbeDepth_CapsAt1000()
        {
            var browser = new Mock<IQueueBrowser>(MockBehavior.Strict);
            var list = new ArrayList();
            for (int i = 0; i < 1005; i++) list.Add(i);
            session.Setup(s => s.GetQueue("q-high")).Returns(queue.Object);
            session.Setup(s => s.CreateBrowser(queue.Object)).Returns(browser.Object);
            browser.Setup(b => b.GetEnumerator()).Returns(list.GetEnumerator());
            browser.Setup(b => b.Dispose());
            Assert.That(await provider.GetApproximateQueueLength(true), Is.EqualTo(1001));
        }

    }
}
