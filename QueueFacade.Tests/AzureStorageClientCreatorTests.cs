// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using Azure.Storage.Queues;
    using NUnit.Framework;
    using Queue.Providers;

    [TestFixture]
    public class AzureStorageClientCreatorTests
    {
        [Test]
        public void CreateQueueClient_ValidEndpoint_ReturnsClient()
        {
            var creator = new AzureStorageClientCreator();
            string endpoint =
                "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdA==;EndpointSuffix=core.windows.net";

            QueueClient client = creator.CreateQueueClient("orders", endpoint);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.Name, Is.EqualTo("orders"));
        }

        [Test]
        public void CreateQueueClient_InvalidEndpoint_ThrowsArgumentException()
        {
            var creator = new AzureStorageClientCreator();

            var ex = Assert.Throws<ArgumentException>(() =>
                creator.CreateQueueClient("orders", "not-a-valid-connection-string"));

            Assert.That(ex.Message, Does.Contain("Invalid combination"));
        }
    }
}
