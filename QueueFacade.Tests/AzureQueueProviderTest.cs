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
            Assert.AreEqual(32, queueProvider.MaxMessageCountPerPoll);
        }

        [Test]
        public void MaxMessageSizeTest()
        {
            Assert.AreEqual(maxMessageSize, queueProvider.MaxMessageSize);
        }

        [Test]
        public void HasLowPriorityQueueTest()
        {
            Assert.IsFalse(queueProvider.HasLowPriorityQueue);
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
            Assert.AreEqual(1, result.Count);
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
    }
}
