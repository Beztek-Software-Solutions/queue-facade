// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using NUnit.Framework;
    using Queue;
    using Queue.Providers;

    [TestFixture]
    public class AzureQueueProviderConfigTest
    {
        [Test]
        public void ConstructorTest_HappyPath()
        {
            AzureQueueProviderConfig config = new AzureQueueProviderConfig("name", "endpoint", "high-priority-queue", "low-prority-queue");
            Assert.That(QueueProviderType.AzureStorage, Is.EqualTo(config.QueueProviderType));
            Assert.That("name", Is.EqualTo(config.Name));
            Assert.That("endpoint", Is.EqualTo(config.Endpoint));
            Assert.That("high-priority-queue", Is.EqualTo(config.HighPriorityQueue));
            Assert.That("low-prority-queue", Is.EqualTo(config.LowPriorityQueue));
        }

        [Test]
        public void ConstructorTest_NoLowPriorityQueue()
        {
            AzureQueueProviderConfig config = new AzureQueueProviderConfig("name", "endpoint", "high-priority-queue");
            Assert.That(QueueProviderType.AzureStorage, Is.EqualTo(config.QueueProviderType));
            Assert.That("name", Is.EqualTo(config.Name));
            Assert.That("endpoint", Is.EqualTo(config.Endpoint));
            Assert.That("high-priority-queue", Is.EqualTo(config.HighPriorityQueue));
            Assert.That(config.LowPriorityQueue, Is.Null);
        }

        [Test]
        public void ConstructorTest_NoName()
        {
            Assert.Throws<ArgumentException>(() =>
                new AzureQueueProviderConfig("", "endpoint", "high-priority-queue", "low-prority-queue"));
        }

        [Test]
        public void ConstructorTest_NoEndpoint()
        {
            Assert.Throws<ArgumentException>(() =>
                new AzureQueueProviderConfig("name", "", "high-priority-queue", "low-prority-queue"));
        }

        [Test]
        public void ConstructorTest_NoHighPriorityQueue()
        {
            Assert.Throws<ArgumentException>(() =>
                new AzureQueueProviderConfig("name", "endpoint", "", "low-prority-queue"));
        }

        [Test]
        public void ConstructorTest_BadHighPriorityQueueName()
        {
            Assert.Throws<ArgumentException>(() =>
                new AzureQueueProviderConfig("name", "endpoint", "bad name", "low-prority-queue"));
        }

        [Test]
        public void ConstructorTest_BadLowhPriorityQueueName()
        {
            Assert.Throws<ArgumentException>(() =>
                new AzureQueueProviderConfig("name", "endpoint", "high-priority-queue", "bad name"));
        }
    }
}
