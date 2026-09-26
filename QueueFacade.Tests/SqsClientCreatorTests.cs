// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using Amazon.SQS;
    using NUnit.Framework;
    using Queue.Providers;

    [TestFixture]
    public class SqsClientCreatorTests
    {
        [Test]
        public void CreateClient_WithRegion_UsesDefaultCredentialChain()
        {
            var creator = new SqsClientCreator();
            var config = new SqsQueueProviderConfig("name", "us-east-1", "high-priority-queue");

            using IAmazonSQS client = creator.CreateClient(config);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.Config.RegionEndpoint.SystemName, Is.EqualTo("us-east-1"));
        }

        [Test]
        public void CreateClient_WithServiceUrlAndRegion_ConfiguresCustomEndpoint()
        {
            var creator = new SqsClientCreator();
            var config = new SqsQueueProviderConfig(
                "name",
                region: "us-east-1",
                highPriorityQueue: "high-priority-queue",
                serviceUrl: "http://localhost:4566");

            using IAmazonSQS client = creator.CreateClient(config);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.Config.ServiceURL, Is.EqualTo("http://localhost:4566/"));
            Assert.That(client.Config.AuthenticationRegion, Is.EqualTo("us-east-1"));
        }

        [Test]
        public void CreateClient_WithExplicitCredentials_UsesBasicCredentials()
        {
            var creator = new SqsClientCreator();
            var config = new SqsQueueProviderConfig(
                "name",
                "us-east-1",
                "high-priority-queue",
                accessKeyId: "test-key",
                secretAccessKey: "test-secret");

            using IAmazonSQS client = creator.CreateClient(config);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.Config.RegionEndpoint.SystemName, Is.EqualTo("us-east-1"));
        }

        [Test]
        public void CreateClient_WithSessionToken_CreatesClient()
        {
            var creator = new SqsClientCreator();
            var config = new SqsQueueProviderConfig(
                "name",
                "us-east-1",
                "high-priority-queue",
                accessKeyId: "test-key",
                secretAccessKey: "test-secret",
                sessionToken: "test-session-token");

            using IAmazonSQS client = creator.CreateClient(config);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.Config.RegionEndpoint.SystemName, Is.EqualTo("us-east-1"));
        }
    }
}
