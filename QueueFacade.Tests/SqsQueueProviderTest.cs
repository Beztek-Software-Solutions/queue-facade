// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Moq;
    using NUnit.Framework;
    using Queue.Providers;

    [TestFixture]
    public class SqsQueueProviderTest
    {
        private Mock<IAmazonSQS> mockSqs;
        private SqsQueueProvider queueProvider;

        [SetUp]
        public void TestInitialize()
        {
            mockSqs = new Mock<IAmazonSQS>(MockBehavior.Strict);
            var config = new SqsQueueProviderConfig("test-name", "us-east-1", "test-high-priority");
            config.SqsClientCreator = new TestSqsClientCreator(mockSqs.Object);
            queueProvider = new SqsQueueProvider(config);

            mockSqs
                .Setup(m => m.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()))
                .Returns<CreateQueueRequest, CancellationToken>((req, _) =>
                    Task.FromResult(new CreateQueueResponse
                    {
                        QueueUrl = $"https://sqs.us-east-1.amazonaws.com/123/{req.QueueName}",
                        HttpStatusCode = HttpStatusCode.OK,
                    }));
        }

        [Test]
        public void CreateIfNotExistsTest()
        {
            queueProvider.CreateIfNotExists();
            mockSqs.Verify(
                m => m.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == "test-high-priority"), It.IsAny<CancellationToken>()),
                Times.Once);
            mockSqs.Verify(
                m => m.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == "test-high-priority-unprocessed"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public void MaxMessageCountPerPollTest()
        {
            Assert.That(queueProvider.MaxMessageCountPerPoll, Is.EqualTo(10));
        }

        [Test]
        public void MaxMessageSizeTest()
        {
            Assert.That(queueProvider.MaxMessageSize, Is.EqualTo(262_144));
        }

        [Test]
        public void HasLowPriorityQueueTest()
        {
            Assert.That(queueProvider.HasLowPriorityQueue, Is.False);
        }

        [Test]
        public void GetMessageBodyTest()
        {
            var message = new Message { Body = "payload" };
            Assert.That(queueProvider.GetMessageBody(message), Is.EqualTo("payload"));
        }

        [Test]
        public async Task SendMessageAsyncTest()
        {
            mockSqs
                .Setup(m => m.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SendMessageResponse { MessageId = "mid-1", HttpStatusCode = HttpStatusCode.OK });

            bool ok = await queueProvider.SendMessageAsync("test", true);
            Assert.That(ok, Is.True);
            mockSqs.Verify(
                m => m.SendMessageAsync(
                    It.Is<SendMessageRequest>(r =>
                        r.MessageBody == "test"
                        && r.QueueUrl.EndsWith("test-high-priority", StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task SendUnprocessedMessageAsyncTest()
        {
            mockSqs
                .Setup(m => m.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SendMessageResponse { MessageId = "mid-2", HttpStatusCode = HttpStatusCode.OK });

            bool ok = await queueProvider.SendUnprocessedMessageAsync("poison");
            Assert.That(ok, Is.True);
        }

        [Test]
        public void GetMessagesTest_FirstDeliveryOnly()
        {
            mockSqs
                .Setup(m => m.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReceiveMessageResponse
                {
                    Messages = new List<Message>
                    {
                        new Message
                        {
                            MessageId = "1",
                            Body = "first",
                            ReceiptHandle = "rh1",
                            Attributes = new Dictionary<string, string> { ["ApproximateReceiveCount"] = "1" },
                        },
                        new Message
                        {
                            MessageId = "2",
                            Body = "retry",
                            ReceiptHandle = "rh2",
                            Attributes = new Dictionary<string, string> { ["ApproximateReceiveCount"] = "3" },
                        },
                    },
                });

            IList<object> result = queueProvider.GetMessages(10, true);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(queueProvider.GetMessageBody(result[0]), Is.EqualTo("first"));
        }

        [Test]
        public async Task DeleteMessageAsyncTest()
        {
            mockSqs
                .Setup(m => m.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK });

            queueProvider.CreateIfNotExists();
            await queueProvider.DeleteMessageAsync(
                new Message { ReceiptHandle = "rh-delete", Body = "x" },
                true);

            mockSqs.Verify(
                m => m.DeleteMessageAsync(
                    It.Is<DeleteMessageRequest>(r => r.ReceiptHandle == "rh-delete"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task GetApproximateQueueLengthTest()
        {
            mockSqs
                .Setup(m => m.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetQueueAttributesResponse
                {
                    Attributes = new Dictionary<string, string>
                    {
                        ["ApproximateNumberOfMessages"] = "7",
                    },
                });

            long length = await queueProvider.GetApproximateQueueLength(true);
            Assert.That(length, Is.EqualTo(7));
        }

        [Test]
        public void CreateIfNotExists_WithLowPriority()
        {
            var config = new SqsQueueProviderConfig(
                "test-name-lp", "us-east-1", "hi-q", "lo-q");
            config.SqsClientCreator = new TestSqsClientCreator(mockSqs.Object);
            var provider = new SqsQueueProvider(config);
            Assert.That(provider.HasLowPriorityQueue, Is.True);
            provider.CreateIfNotExists();
            mockSqs.Verify(
                m => m.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == "lo-q"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public void CreateIfNotExists_ExceptionResetsFlag()
        {
            mockSqs
                .Setup(m => m.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new AmazonSQSException("simulated"));

            Assert.Throws<AmazonSQSException>(() => queueProvider.CreateIfNotExists());

            // Reset mock so retry can succeed for high + unprocessed
            mockSqs.Reset();
            mockSqs
                .Setup(m => m.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()))
                .Returns<CreateQueueRequest, CancellationToken>((req, _) =>
                    Task.FromResult(new CreateQueueResponse
                    {
                        QueueUrl = $"https://sqs.us-east-1.amazonaws.com/123/{req.QueueName}",
                    }));

            Assert.DoesNotThrow(() => queueProvider.CreateIfNotExists());
        }
    }
}
