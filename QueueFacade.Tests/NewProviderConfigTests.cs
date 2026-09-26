// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class NewProviderConfigTests
    {
        [Test]
        public void AzureServiceBusConfig_RequiresConnectionAndQueue()
        {
            Assert.Throws<ArgumentException>(() => new AzureServiceBusProviderConfig("", "cs", "queue-a"));
            Assert.Throws<ArgumentException>(() => new AzureServiceBusProviderConfig("n", "", "queue-a"));
            Assert.Throws<ArgumentException>(() => new AzureServiceBusProviderConfig("n", "cs", ""));
            var ok = new AzureServiceBusProviderConfig("n", "Endpoint=sb://x", "queue-a");
            Assert.That(ok.UnprocessedQueue, Is.EqualTo("queue-a-unprocessed"));
            Assert.That(ok.QueueProviderType, Is.EqualTo(QueueProviderType.AzureServiceBus));
            Assert.That(ok.AdministrationConnectionString, Is.Null);

            var withAdmin = new AzureServiceBusProviderConfig(
                "n",
                "Endpoint=sb://x:5672",
                "queue-a",
                administrationConnectionString: "Endpoint=sb://x:5300;UseDevelopmentEmulator=true");
            Assert.That(withAdmin.AdministrationConnectionString, Does.Contain(":5300"));
        }

        [Test]
        public void RabbitMqConfig_RequiresAmqpUri()
        {
            Assert.Throws<ArgumentException>(() => new RabbitMqProviderConfig("n", "", "queue-a"));
            var ok = new RabbitMqProviderConfig("n", "amqp://guest:guest@localhost:5672/", "queue-a");
            Assert.That(ok.QueueProviderType, Is.EqualTo(QueueProviderType.RabbitMq));
        }

        [Test]
        public void GooglePubSubConfig_RequiresProject()
        {
            Assert.Throws<ArgumentException>(() => new GooglePubSubProviderConfig("n", "", "queue-a"));
            var ok = new GooglePubSubProviderConfig("n", "proj", "queue-a", emulatorHost: "localhost:8085");
            Assert.That(ok.EmulatorHost, Is.EqualTo("localhost:8085"));
        }

        [Test]
        public void RedisConfig_RequiresConfiguration()
        {
            Assert.Throws<ArgumentException>(() => new RedisQueueProviderConfig("n", "", "queue-a"));
            var ok = new RedisQueueProviderConfig("n", "localhost:6379", "queue-a");
            Assert.That(ok.QueueProviderType, Is.EqualTo(QueueProviderType.Redis));
        }

        [Test]
        public void ValkeyAndDragonflyConfigs_ReuseRedisProtocol()
        {
            var valkey = new ValkeyQueueProviderConfig("n", "localhost:6379", "queue-a");
            Assert.That(valkey.QueueProviderType, Is.EqualTo(QueueProviderType.Valkey));
            Assert.That(valkey, Is.InstanceOf<RedisQueueProviderConfig>());

            var dragonfly = new DragonflyQueueProviderConfig("n", "localhost:6379", "queue-a");
            Assert.That(dragonfly.QueueProviderType, Is.EqualTo(QueueProviderType.Dragonfly));
            Assert.That(dragonfly, Is.InstanceOf<RedisQueueProviderConfig>());
        }

        [Test]
        public void ActiveMqConfig_RequiresBrokerUri()
        {
            Assert.Throws<ArgumentException>(() => new ActiveMqProviderConfig("n", "", "queue-a"));
            var ok = new ActiveMqProviderConfig("n", "tcp://localhost:61616", "queue-a");
            Assert.That(ok.QueueProviderType, Is.EqualTo(QueueProviderType.ActiveMq));
        }

        [Test]
        public void BeanstalkdConfig_RequiresHostAndPort()
        {
            Assert.Throws<ArgumentException>(() => new BeanstalkdProviderConfig("n", "", 11300, "queue-a"));
            Assert.Throws<ArgumentException>(() => new BeanstalkdProviderConfig("n", "localhost", 0, "queue-a"));
            var ok = new BeanstalkdProviderConfig("n", "localhost", 11300, "queue-a");
            Assert.That(ok.QueueProviderType, Is.EqualTo(QueueProviderType.Beanstalkd));
        }

        [Test]
        public void PoisonMustDifferFromHigh()
        {
            Assert.Throws<ArgumentException>(() =>
                new RedisQueueProviderConfig("n", "localhost:6379", "queue-a", unprocessedQueue: "queue-a"));
        }
    }
}
