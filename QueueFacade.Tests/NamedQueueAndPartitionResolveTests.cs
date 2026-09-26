// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using Beztek.Facade.Queue.Providers;
    using NUnit.Framework;

    [TestFixture]
    public class NamedQueueAndPartitionResolveTests
    {
        [Test]
        public void TryGetPrimaryQueueNames_NamedVsLocalMemory()
        {
            var named = new RedisQueueProviderConfig("n", "localhost:6379", "high-{partition}", "low-{partition}");
            Assert.That(QueueClientFactory.TryGetPrimaryQueueNames(named, out string high, out string low, out string poison), Is.True);
            Assert.That(high, Is.EqualTo("high-{partition}"));
            Assert.That(low, Is.EqualTo("low-{partition}"));
            Assert.That(poison, Does.Contain("unprocessed"));

            var local = new LocalMemoryQueueProviderConfig("local");
            Assert.That(QueueClientFactory.TryGetPrimaryQueueNames(local, out _, out _, out _), Is.False);
        }

        [Test]
        public void TryGetHighPriorityQueue_UsesNamedInterface()
        {
            var cfg = new BeanstalkdProviderConfig("n", "localhost", 11300, "tube-a");
            Assert.That(PartitionedQueueClient.TryGetHighPriorityQueue(cfg, out string high), Is.True);
            Assert.That(high, Is.EqualTo("tube-a"));
            Assert.That(PartitionedQueueClient.TryGetHighPriorityQueue(new LocalMemoryQueueProviderConfig("l"), out _), Is.False);
        }

        [TestCaseSource(nameof(PartitionTemplates))]
        public void ResolveConfig_SubstitutesPartition(IQueueProviderConfig template, Type expectedType)
        {
            IQueueProviderConfig resolved = PartitionedQueueClient.ResolveConfig(template, "acme-co");
            Assert.That(resolved, Is.InstanceOf(expectedType));
            Assert.That(resolved.Name, Does.Contain(":"));
            if (expectedType != typeof(LocalMemoryQueueProviderConfig))
            {
                Assert.That(PartitionedQueueClient.TryGetHighPriorityQueue(resolved, out string high), Is.True);
                Assert.That(high, Does.Not.Contain("{partition}"));
            }
        }

        private static System.Collections.IEnumerable PartitionTemplates()
        {
            yield return new TestCaseData(
                new AzureServiceBusProviderConfig("asb", "Endpoint=sb://x", "q-{partition}", administrationConnectionString: "Endpoint=sb://x:5300"),
                typeof(AzureServiceBusProviderConfig)).SetName("AzureServiceBus");
            yield return new TestCaseData(
                new RabbitMqProviderConfig("rb", "amqp://guest:guest@localhost:5672/", "q-{partition}"),
                typeof(RabbitMqProviderConfig)).SetName("RabbitMq");
            yield return new TestCaseData(
                new GooglePubSubProviderConfig("ps", "proj", "q-{partition}", emulatorHost: "localhost:8085"),
                typeof(GooglePubSubProviderConfig)).SetName("GooglePubSub");
            yield return new TestCaseData(
                new RedisQueueProviderConfig("rd", "localhost:6379", "q-{partition}"),
                typeof(RedisQueueProviderConfig)).SetName("Redis");
            yield return new TestCaseData(
                new ValkeyQueueProviderConfig("vk", "localhost:6379", "q-{partition}"),
                typeof(ValkeyQueueProviderConfig)).SetName("Valkey");
            yield return new TestCaseData(
                new DragonflyQueueProviderConfig("df", "localhost:6379", "q-{partition}"),
                typeof(DragonflyQueueProviderConfig)).SetName("Dragonfly");
            yield return new TestCaseData(
                new ActiveMqProviderConfig("amq", "tcp://localhost:61616", "q-{partition}"),
                typeof(ActiveMqProviderConfig)).SetName("ActiveMq");
            yield return new TestCaseData(
                new BeanstalkdProviderConfig("bs", "localhost", 11300, "q-{partition}"),
                typeof(BeanstalkdProviderConfig)).SetName("Beanstalkd");
            yield return new TestCaseData(
                new LocalMemoryQueueProviderConfig("lm"),
                typeof(LocalMemoryQueueProviderConfig)).SetName("LocalMemory");
        }
    }
}
