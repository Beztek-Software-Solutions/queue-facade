// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using NUnit.Framework;
    using Queue;

    [TestFixture]
    public class TestLocalMemoryQueueProviderConfig
    {
        [Test]
        public void ConstructorTest_HappyPath()
        {
            LocalMemoryQueueProviderConfig config = new LocalMemoryQueueProviderConfig("name");
            Assert.That(QueueProviderType.LocalMemory, Is.EqualTo(config.QueueProviderType));
            Assert.That("name", Is.EqualTo(config.Name));
        }

        [Test]
        public void ConstructorTest_NoName()
        {
            Assert.Throws<ArgumentException>(() => new LocalMemoryQueueProviderConfig(""));
        }
    }
}
