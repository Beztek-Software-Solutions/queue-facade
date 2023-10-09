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
            Assert.IsNotNull(client);
        }

        [Test]
        public void LocalMemoryProvider_HappyPath()
        {
            LocalMemoryQueueProviderConfig config = new LocalMemoryQueueProviderConfig("memory");
            IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
            Assert.IsNotNull(client);
        }

        [Test]
        public void UnkonwnProviderTest()
        {
            IQueueProviderConfig config = new TestUnsupportedQueueProviderConfig("other");
            Assert.Throws<NotSupportedException>(() => QueueClientFactory.GetQueueClient(config, logger));
        }
    }
}
