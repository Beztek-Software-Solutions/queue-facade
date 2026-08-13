// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using NUnit.Framework;
    using Queue;
    using Queue.Providers;

    [TestFixture]
    public class SqsQueueProviderConfigTest
    {
        [Test]
        public void ConstructorTest_HappyPath()
        {
            var config = new SqsQueueProviderConfig("name", "us-east-1", "high-priority-queue", "low-priority-queue");
            Assert.That(config.QueueProviderType, Is.EqualTo(QueueProviderType.AwsSqs));
            Assert.That(config.Name, Is.EqualTo("name"));
            Assert.That(config.Region, Is.EqualTo("us-east-1"));
            Assert.That(config.HighPriorityQueue, Is.EqualTo("high-priority-queue"));
            Assert.That(config.LowPriorityQueue, Is.EqualTo("low-priority-queue"));
            Assert.That(config.UnprocessedQueue, Is.EqualTo("high-priority-queue-unprocessed"));
        }

        [Test]
        public void ConstructorTest_NoLowPriorityQueue()
        {
            var config = new SqsQueueProviderConfig("name", "us-east-1", "high-priority-queue");
            Assert.That(config.LowPriorityQueue, Is.Null);
            Assert.That(config.UnprocessedQueue, Is.EqualTo("high-priority-queue-unprocessed"));
        }

        [Test]
        public void ConstructorTest_ExplicitUnprocessedQueue()
        {
            var config = new SqsQueueProviderConfig(
                "name",
                "us-east-1",
                "high-priority-queue",
                unprocessedQueue: "app-a-poison");
            Assert.That(config.UnprocessedQueue, Is.EqualTo("app-a-poison"));
        }

        [Test]
        public void ConstructorTest_UnprocessedSameAsHighRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new SqsQueueProviderConfig(
                    "name",
                    "us-east-1",
                    "high-priority-queue",
                    unprocessedQueue: "high-priority-queue"));
        }

        [Test]
        public void ConstructorTest_ServiceUrlWithoutRegion()
        {
            var config = new SqsQueueProviderConfig(
                "name",
                region: null,
                highPriorityQueue: "high-priority-queue",
                serviceUrl: "http://localhost:4566");
            Assert.That(config.ServiceUrl, Is.EqualTo("http://localhost:4566"));
        }

        [Test]
        public void ConstructorTest_NoName()
        {
            Assert.Throws<ArgumentException>(() =>
                new SqsQueueProviderConfig("", "us-east-1", "high-priority-queue"));
        }

        [Test]
        public void ConstructorTest_NoRegionOrServiceUrl()
        {
            Assert.Throws<ArgumentException>(() =>
                new SqsQueueProviderConfig("name", "", "high-priority-queue"));
        }

        [Test]
        public void ConstructorTest_NoHighPriorityQueue()
        {
            Assert.Throws<ArgumentException>(() =>
                new SqsQueueProviderConfig("name", "us-east-1", ""));
        }

        [Test]
        public void ConstructorTest_BadHighPriorityQueueName()
        {
            Assert.Throws<ArgumentException>(() =>
                new SqsQueueProviderConfig("name", "us-east-1", "bad name"));
        }

        [Test]
        public void ConstructorTest_FifoRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new SqsQueueProviderConfig("name", "us-east-1", "my-queue.fifo"));
        }

        [Test]
        public void ConstructorTest_UnderscoreRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new SqsQueueProviderConfig("name", "us-east-1", "booth_commands"));
        }
    }
}
