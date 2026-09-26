// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Queues;
    using Azure.Storage.Queues.Models;
    using Moq;
    using NUnit.Framework;
    using Queue.Providers;

    [TestFixture]
    public class AzureQueueProviderTest
    {
        private Mock<QueueClient> mockQueueClient;
        private AzureQueueProvider queueProvider;
        private int maxMessageSize = 1000;

        [SetUp]
        public void TestInitialize()
        {
            AzureQueueProviderConfig config = new AzureQueueProviderConfig("test-name", "test-endpoint", "test-high-priority");
            this.mockQueueClient = new Mock<QueueClient>();
            this.mockQueueClient.Setup(m => m.MessageMaxBytes).Returns(maxMessageSize);
            var queueProperties = QueuesModelFactory.QueueProperties(null, 11L);
            this.mockQueueClient
                .Setup(m => m.GetProperties(It.IsAny<CancellationToken>()))
                .Returns(new ResponseTest<QueueProperties>(queueProperties));
            config.AzureStorageClientCreator = new TestAzureStorageClientCreator(this.mockQueueClient.Object);
            this.queueProvider = new AzureQueueProvider(config);
        }

        [Test]
        public void CreateIfNotExistsTest()
        {
            queueProvider.CreateIfNotExists();
        }

        [Test]
        public void MaxMessageCountPerPollTest()
        {
            Assert.That(32, Is.EqualTo(queueProvider.MaxMessageCountPerPoll));
        }

        [Test]
        public void MaxMessageSizeTest()
        {
            Assert.That(maxMessageSize, Is.EqualTo(queueProvider.MaxMessageSize));
        }

        [Test]
        public void HasLowPriorityQueueTest()
        {
            Assert.That(queueProvider.HasLowPriorityQueue, Is.False);
        }

        [Test]
        public void GetMessageBodyTest()
        {
            try
            {
                queueProvider.GetMessageBody(new Mock<QueueMessage>().Object);
            }
            catch (NullReferenceException)
            {
                // We expect this exception, because we cannot mock QueueMessage.Body
            }
        }

        [Test]
        public void DeleteMessagesAsyncTest()
        {
            queueProvider.DeleteMessageAsync(new Mock<QueueMessage>().Object, true).Wait();
        }

        [Test]
        public void SendMessagesAsyncTest()
        {
            Mock<Azure.Response<SendReceipt>> mockResponse = new Mock<Azure.Response<SendReceipt>>();
            Mock<SendReceipt> mockSendReceipt = new Mock<SendReceipt>();
            mockQueueClient.Setup(m => m.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult(mockResponse.Object));
            mockResponse.Setup(m => m.Value).Returns(mockSendReceipt.Object);

            queueProvider.SendMessageAsync("test", true).Wait();
        }

        [Test]
        public void GetMessagesTest()
        {
            Mock<Azure.Response<QueueMessage[]>> mockResponse = new Mock<Azure.Response<QueueMessage[]>>();
            mockQueueClient.Setup(m => m.ReceiveMessages(It.IsAny<int>(), It.IsAny<TimeSpan>(), default(CancellationToken))).Returns(mockResponse.Object);
            mockResponse.Setup(m => m.Value).Returns(new QueueMessage[] { default(QueueMessage) });

            IList<object> result = queueProvider.GetMessages(10, true);
            Assert.That(1, Is.EqualTo(result.Count));
        }

        [Test]
        public void SendUnprocessedMessageAsyncTest()
        {
            Mock<Azure.Response<SendReceipt>> mockResponse = new Mock<Azure.Response<SendReceipt>>();
            Mock<SendReceipt> mockSendReceipt = new Mock<SendReceipt>();
            mockQueueClient.Setup(m => m.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult(mockResponse.Object));
            mockResponse.Setup(m => m.Value).Returns(mockSendReceipt.Object);

            queueProvider.SendUnprocessedMessageAsync("test").Wait();
        }

        [Test]
        public void CreateIfNotExistsTest_Exception()
        {
            mockQueueClient.Setup(m => m.CreateIfNotExists(null, default(CancellationToken))).Throws(new ArgumentException("simulated exception"));

            Assert.Throws<ArgumentException>(() => queueProvider.CreateIfNotExists());
        }

        [Test]
        public async Task GetApproximateQueueLengthTest()
        {
            long length = await queueProvider.GetApproximateQueueLength(true);
            Assert.That(length, Is.EqualTo(11));
        }

        [Test]
        public async Task GetApproximateUnprocessedQueueLengthTest()
        {
            long length = await queueProvider.GetApproximateUnprocessedQueueLength();
            Assert.That(length, Is.EqualTo(11));
        }

        [Test]
        public async Task DeleteUnprocessedMessageAsyncTest()
        {
            mockQueueClient
                .Setup(m => m.DeleteMessageAsync("mid-1", "pop-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<Azure.Response>());

            await queueProvider.DeleteUnprocessedMessageAsync(
                QueuesModelFactory.QueueMessage("mid-1", "pop-1", "body", 0, null, null, null));
        }

        [Test]
        public async Task DeleteUnprocessedMessageAsync_NonQueueMessage_NoOp()
        {
            await queueProvider.DeleteUnprocessedMessageAsync("not-a-queue-message");
            mockQueueClient.Verify(
                m => m.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public void GetMessages_NullArray_ReturnsEmpty()
        {
            Mock<Azure.Response<QueueMessage[]>> mockResponse = new Mock<Azure.Response<QueueMessage[]>>();
            mockQueueClient
                .Setup(m => m.ReceiveMessages(It.IsAny<int>(), It.IsAny<TimeSpan>(), default(CancellationToken)))
                .Returns(mockResponse.Object);
            mockResponse.Setup(m => m.Value).Returns((QueueMessage[])null);

            IList<object> result = queueProvider.GetMessages(10, true);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetReceiveCount_NullHook_DefaultsToOne()
        {
            Assert.That(queueProvider.GetReceiveCount(null), Is.EqualTo(1));
            Assert.That(queueProvider.GetReceiveCount("x"), Is.EqualTo(1));
        }

        [Test]
        public void GetReceiveCount_FromQueueMessage()
        {
            QueueMessage msg = QueuesModelFactory.QueueMessage("id", "pop", "body", dequeueCount: 4, null, null, null);
            Assert.That(queueProvider.GetReceiveCount(msg), Is.EqualTo(4));
        }

        [Test]
        public void PeekUnprocessedMessages_ReturnsPeeked()
        {
            PeekedMessage peeked = QueuesModelFactory.PeekedMessage("id", "poison-body", 1, null, null);
            mockQueueClient
                .Setup(m => m.PeekMessages(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(new ResponseTest<PeekedMessage[]>(new[] { peeked }));

            IList<object> result = queueProvider.PeekUnprocessedMessages(5);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(queueProvider.GetMessageBody(result[0]), Is.EqualTo("poison-body"));
        }

        [Test]
        public void PeekUnprocessedMessages_Null_ReturnsEmpty()
        {
            mockQueueClient
                .Setup(m => m.PeekMessages(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(new ResponseTest<PeekedMessage[]>(null));

            Assert.That(queueProvider.PeekUnprocessedMessages(5).Count, Is.EqualTo(0));
        }

        [Test]
        public void ReceiveUnprocessedMessages_ReturnsMessages()
        {
            QueueMessage msg = QueuesModelFactory.QueueMessage("id", "pop", "body", 1, null, null, null);
            mockQueueClient
                .Setup(m => m.ReceiveMessages(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(new ResponseTest<QueueMessage[]>(new[] { msg }));

            IList<object> result = queueProvider.ReceiveUnprocessedMessages(5);
            Assert.That(result.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReceiveUnprocessedMessages_Null_ReturnsEmpty()
        {
            mockQueueClient
                .Setup(m => m.ReceiveMessages(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(new ResponseTest<QueueMessage[]>(null));

            Assert.That(queueProvider.ReceiveUnprocessedMessages(5).Count, Is.EqualTo(0));
        }

        [Test]
        public void GetMessageBody_PeekedMessage_AndFallback()
        {
            PeekedMessage peeked = QueuesModelFactory.PeekedMessage("id", "peeked-text", 1, null, null);
            Assert.That(queueProvider.GetMessageBody(peeked), Is.EqualTo("peeked-text"));
            Assert.That(queueProvider.GetMessageBody("plain"), Is.EqualTo("plain"));
            Assert.That(queueProvider.GetMessageBody(null), Is.Null);
        }

        [Test]
        public async Task DeleteMessageAsync_NullMessage_NoOp()
        {
            await queueProvider.DeleteMessageAsync(null, true);
            mockQueueClient.Verify(
                m => m.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task SendMessageAsync_EmptyMessageId_ReturnsFalse()
        {
            Mock<Azure.Response<SendReceipt>> mockResponse = new Mock<Azure.Response<SendReceipt>>();
            mockQueueClient.Setup(m => m.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(mockResponse.Object);
            mockResponse.Setup(m => m.Value).Returns(QueuesModelFactory.SendReceipt(string.Empty, default, default, "pop", default));

            Assert.That(await queueProvider.SendMessageAsync("test", true), Is.False);
        }

        [Test]
        public void CreateIfNotExists_WithLowPriority()
        {
            AzureQueueProviderConfig config = new AzureQueueProviderConfig(
                "test-name-lp", "test-endpoint", "test-high-priority", "test-low-priority");
            config.AzureStorageClientCreator = new TestAzureStorageClientCreator(this.mockQueueClient.Object);
            AzureQueueProvider provider = new AzureQueueProvider(config);
            Assert.That(provider.HasLowPriorityQueue, Is.True);
            provider.CreateIfNotExists();
        }

        [Test]
        public async Task SendMessageAsync_LowPriority_UsesLastClient()
        {
            AzureQueueProviderConfig config = new AzureQueueProviderConfig(
                "test-name-lp2", "test-endpoint", "test-high-priority", "test-low-priority");
            config.AzureStorageClientCreator = new TestAzureStorageClientCreator(this.mockQueueClient.Object);
            AzureQueueProvider provider = new AzureQueueProvider(config);

            Mock<Azure.Response<SendReceipt>> mockResponse = new Mock<Azure.Response<SendReceipt>>();
            mockQueueClient.Setup(m => m.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(mockResponse.Object);
            mockResponse.Setup(m => m.Value).Returns(
                QueuesModelFactory.SendReceipt("mid-lo", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "pop", DateTimeOffset.UtcNow));

            Assert.That(await provider.SendMessageAsync("x", useHighPriorityQueue: false), Is.True);
        }
    }
}
