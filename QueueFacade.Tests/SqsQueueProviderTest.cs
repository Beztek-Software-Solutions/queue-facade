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
        public void GetMessagesTest_IncludesRetryDeliveries()
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
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(queueProvider.GetMessageBody(result[0]), Is.EqualTo("first"));
            Assert.That(queueProvider.GetMessageBody(result[1]), Is.EqualTo("retry"));
            Assert.That(queueProvider.GetReceiveCount(result[1]), Is.EqualTo(3));
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
        public async Task GetApproximateUnprocessedQueueLengthTest()
        {
            mockSqs
                .Setup(m => m.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetQueueAttributesResponse
                {
                    Attributes = new Dictionary<string, string>
                    {
                        ["ApproximateNumberOfMessages"] = "3",
                    },
                });

            long length = await queueProvider.GetApproximateUnprocessedQueueLength();
            Assert.That(length, Is.EqualTo(3));
        }

        [Test]
        public async Task DeleteUnprocessedMessageAsyncTest()
        {
            mockSqs
                .Setup(m => m.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK });

            queueProvider.CreateIfNotExists();
            await queueProvider.DeleteUnprocessedMessageAsync(new Message
            {
                ReceiptHandle = "rh-poison",
                Body = "x",
            });

            mockSqs.Verify(
                m => m.DeleteMessageAsync(
                    It.Is<DeleteMessageRequest>(r =>
                        r.ReceiptHandle == "rh-poison"
                        && r.QueueUrl.EndsWith("test-high-priority-unprocessed", StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task DeleteUnprocessedMessageAsync_NullHook_NoOp()
        {
            await queueProvider.DeleteUnprocessedMessageAsync(null);
            mockSqs.Verify(
                m => m.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
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

        [Test]
        public void CreateIfNotExists_SecondCall_IsNoOp()
        {
            queueProvider.CreateIfNotExists();
            queueProvider.CreateIfNotExists();
            mockSqs.Verify(
                m => m.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)); // high + unprocessed once each
        }

        [Test]
        public async Task SendMessageAsync_EmptyMessageId_ReturnsFalse()
        {
            mockSqs
                .Setup(m => m.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SendMessageResponse { MessageId = null, HttpStatusCode = HttpStatusCode.OK });

            Assert.That(await queueProvider.SendMessageAsync("test", true), Is.False);
        }

        [Test]
        public void GetMessages_NullMessages_ReturnsEmpty()
        {
            mockSqs
                .Setup(m => m.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReceiveMessageResponse { Messages = null });

            IList<object> result = queueProvider.GetMessages(10, true);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetReceiveCount_MissingOrInvalid_DefaultsToOne()
        {
            Assert.That(queueProvider.GetReceiveCount(new Message { Body = "x" }), Is.EqualTo(1));
            Assert.That(
                queueProvider.GetReceiveCount(new Message
                {
                    Body = "x",
                    Attributes = new Dictionary<string, string> { ["ApproximateReceiveCount"] = "nope" },
                }),
                Is.EqualTo(1));
            Assert.That(queueProvider.GetReceiveCount(null), Is.EqualTo(1));
            Assert.That(queueProvider.GetReceiveCount("not-a-message"), Is.EqualTo(1));
        }

        [Test]
        public void ReceiveAndPeekUnprocessed_ReturnsMessages()
        {
            mockSqs
                .Setup(m => m.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReceiveMessageResponse
                {
                    Messages = new List<Message>
                    {
                        new Message { MessageId = "p1", Body = "poison-body", ReceiptHandle = "rh-p" },
                    },
                });

            IList<object> received = queueProvider.ReceiveUnprocessedMessages(5);
            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(queueProvider.GetMessageBody(received[0]), Is.EqualTo("poison-body"));

            IList<object> peeked = queueProvider.PeekUnprocessedMessages(5);
            Assert.That(peeked.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetApproximateQueueLength_MissingAttribute_ReturnsZero()
        {
            mockSqs
                .Setup(m => m.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetQueueAttributesResponse
                {
                    Attributes = new Dictionary<string, string>(),
                });

            Assert.That(await queueProvider.GetApproximateQueueLength(true), Is.EqualTo(0));
            Assert.That(await queueProvider.GetApproximateUnprocessedQueueLength(), Is.EqualTo(0));
        }

        [Test]
        public async Task GetApproximateQueueLength_NullAttributes_ReturnsZero()
        {
            mockSqs
                .Setup(m => m.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetQueueAttributesResponse { Attributes = null });

            Assert.That(await queueProvider.GetApproximateQueueLength(true), Is.EqualTo(0));
        }

        [Test]
        public async Task DeleteMessageAsync_NullOrEmptyReceipt_NoOp()
        {
            queueProvider.CreateIfNotExists();
            await queueProvider.DeleteMessageAsync(null, true);
            await queueProvider.DeleteMessageAsync(new Message { Body = "x", ReceiptHandle = "" }, true);
            mockSqs.Verify(
                m => m.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public void EnsureQueueUrl_QueueNameExists_FallsBackToGetUrl()
        {
            mockSqs
                .Setup(m => m.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new QueueNameExistsException("exists"));
            mockSqs
                .Setup(m => m.GetQueueUrlAsync(It.IsAny<GetQueueUrlRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetQueueUrlResponse
                {
                    QueueUrl = "https://sqs.us-east-1.amazonaws.com/123/existing",
                });

            Assert.DoesNotThrow(() => queueProvider.CreateIfNotExists());
            mockSqs.Verify(
                m => m.GetQueueUrlAsync(It.IsAny<GetQueueUrlRequest>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [Test]
        public async Task SendMessageAsync_LowPriority_UsesLowQueueUrl()
        {
            var config = new SqsQueueProviderConfig("lp-send", "us-east-1", "hi-q", "lo-q");
            config.SqsClientCreator = new TestSqsClientCreator(mockSqs.Object);
            var provider = new SqsQueueProvider(config);

            mockSqs
                .Setup(m => m.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SendMessageResponse { MessageId = "mid-lo" });

            Assert.That(await provider.SendMessageAsync("x", useHighPriorityQueue: false), Is.True);
            mockSqs.Verify(
                m => m.SendMessageAsync(
                    It.Is<SendMessageRequest>(r => r.QueueUrl.EndsWith("lo-q", StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public void GetMessageBody_NullMessage_ReturnsNull()
        {
            Assert.That(queueProvider.GetMessageBody(null), Is.Null);
        }
    }
}
