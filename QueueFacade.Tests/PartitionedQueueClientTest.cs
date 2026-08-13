// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Moq;
    using NUnit.Framework;
    using Queue;
    using Queue.Providers;

    [TestFixture]
    public class PartitionedQueueClientTest
    {
        [Test]
        public void NormalizePartitionKey_RejectsEmpty()
        {
            Assert.Throws<ArgumentException>(() => QueuePartition.NormalizePartitionKey(""));
        }

        [Test]
        public void NormalizePartitionKey_AlwaysLowercases()
        {
            Assert.That(
                QueuePartition.NormalizePartitionKey("ABC-def"),
                Is.EqualTo("abc-def"));
        }

        [Test]
        public void NormalizePartitionKey_RejectsUnderscore()
        {
            Assert.Throws<ArgumentException>(() => QueuePartition.NormalizePartitionKey("abc_def"));
        }

        [Test]
        public void Sqs_ForPartition_ResolvesCustomerQueues()
        {
            var mockSqs = new Mock<IAmazonSQS>();
            mockSqs
                .Setup(m => m.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), default))
                .ReturnsAsync((CreateQueueRequest req, System.Threading.CancellationToken _) =>
                    new CreateQueueResponse
                    {
                        QueueUrl = $"https://sqs.us-east-1.amazonaws.com/123/{req.QueueName}",
                    });

            var template = new SqsQueueProviderConfig(
                name: "booth-cmd",
                region: "us-east-1",
                highPriorityQueue: "al-booth-cmd-{partition}",
                lowPriorityQueue: "al-booth-cmd-{partition}-low");
            template.SqsClientCreator = new TestSqsClientCreator(mockSqs.Object);

            IPartitionedQueueClient partitioned = QueueClientFactory.GetPartitionedQueueClient(template);
            string customerId = "8f3c2a1b-7d6e-4f9a-b0c1-2d3e4f5a6b7c";
            IQueueClient a = partitioned.ForPartition(customerId);
            IQueueClient b = partitioned.ForPartition(customerId);

            Assert.That(a, Is.SameAs(b));
            Assert.That(a.GetName(), Is.EqualTo($"booth-cmd:{customerId}"));
            Assert.That(template.UnprocessedQueue, Is.EqualTo("al-booth-cmd-{partition}-unprocessed"));
        }

        [Test]
        public void Sqs_GetQueueClient_RejectsUnresolvedTemplate()
        {
            var template = new SqsQueueProviderConfig(
                "booth-cmd",
                "us-east-1",
                "al-booth-cmd-{partition}");
            Assert.Throws<ArgumentException>(() => QueueClientFactory.GetQueueClient(template));
        }

        [Test]
        public void Sqs_Partitioned_RequiresToken()
        {
            var config = new SqsQueueProviderConfig("booth-cmd-no-token", "us-east-1", "al-booth-cmd");
            Assert.Throws<ArgumentException>(() =>
                QueueClientFactory.GetPartitionedQueueClient(config));
        }

        [Test]
        public void LocalMemory_ForPartition_IsolatesClients()
        {
            var template = new LocalMemoryQueueProviderConfig("mem-booth");
            IPartitionedQueueClient partitioned = QueueClientFactory.GetPartitionedQueueClient(template);
            IQueueClient a = partitioned.ForPartition("customer-a");
            IQueueClient b = partitioned.ForPartition("customer-b");
            Assert.That(a.GetName(), Is.EqualTo("mem-booth:customer-a"));
            Assert.That(b.GetName(), Is.EqualTo("mem-booth:customer-b"));
            Assert.That(a, Is.Not.SameAs(b));
        }
    }
}
