// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;
    using Queue;
    using Queue.Providers;

    [TestFixture]
    public class QueueClientFactoryTest
    {
        private readonly ILogger logger = new LoggerFactory().CreateLogger<QueueClientFactoryTest>();

        [Test]
        public void AzureProvider_HappyPath()
        {
            AzureQueueProviderConfig config = new AzureQueueProviderConfig("azure", "endpoint", "high-priority-queue", "low-prority-queue");
            config.AzureStorageClientCreator = new TestAzureStorageClientCreator(new Mock<Azure.Storage.Queues.QueueClient>().Object);
            IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
            Assert.That(client, Is.Not.Null);
        }

        [Test]
        public void LocalMemoryProvider_HappyPath()
        {
            LocalMemoryQueueProviderConfig config = new LocalMemoryQueueProviderConfig("memory");
            IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
            Assert.That(client, Is.Not.Null);
        }

        [Test]
        public void SqsProvider_HappyPath()
        {
            var mockSqs = new Mock<Amazon.SQS.IAmazonSQS>();
            mockSqs
                .Setup(m => m.CreateQueueAsync(It.IsAny<Amazon.SQS.Model.CreateQueueRequest>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new Amazon.SQS.Model.CreateQueueResponse
                {
                    QueueUrl = "https://sqs.us-east-1.amazonaws.com/123/high-priority-queue",
                });
            var config = new SqsQueueProviderConfig("sqs", "us-east-1", "high-priority-queue", "low-priority-queue");
            config.SqsClientCreator = new TestSqsClientCreator(mockSqs.Object);
            IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
            Assert.That(client, Is.Not.Null);
            Assert.That(client.GetName(), Is.EqualTo("sqs"));
        }

        [Test]
        public void UnkonwnProviderTest()
        {
            IQueueProviderConfig config = new TestUnsupportedQueueProviderConfig("other");
            Assert.Throws<NotSupportedException>(() => QueueClientFactory.GetQueueClient(config, logger));
        }
    }
}
