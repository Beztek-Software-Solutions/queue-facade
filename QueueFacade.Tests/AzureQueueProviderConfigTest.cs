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
            Assert.AreEqual(QueueProviderType.AzureStorage, config.QueueProviderType);
            Assert.AreEqual("name", config.Name);
            Assert.AreEqual("endpoint", config.Endpoint);
            Assert.AreEqual("high-priority-queue", config.HighPriorityQueue);
            Assert.AreEqual("low-prority-queue", config.LowPriorityQueue);
        }

        [Test]
        public void ConstructorTest_NoLowPriorityQueue()
        {
            AzureQueueProviderConfig config = new AzureQueueProviderConfig("name", "endpoint", "high-priority-queue");
            Assert.AreEqual(QueueProviderType.AzureStorage, config.QueueProviderType);
            Assert.AreEqual("name", config.Name);
            Assert.AreEqual("endpoint", config.Endpoint);
            Assert.AreEqual("high-priority-queue", config.HighPriorityQueue);
            Assert.IsNull(config.LowPriorityQueue);
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
