// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Beztek.Facade.Queue.Providers;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class BeanstalkdProviderTests
    {
        private Mock<IBeanstalkdClient> client;
        private BeanstalkdProvider provider;

        [SetUp]
        public void SetUp()
        {
            client = new Mock<IBeanstalkdClient>(MockBehavior.Strict);
            var config = new BeanstalkdProviderConfig("n", "localhost", 11300, "q-high", "q-low");
            provider = new BeanstalkdProvider(config, client.Object);
        }

        [Test]
        public async Task SendMessageAsync_PutsOnHighTube()
        {
            client.Setup(c => c.Use("q-high"));
            client.Setup(c => c.Put(1024, 0, It.IsAny<int>(), "hello")).Returns(1UL);
            Assert.That(await provider.SendMessageAsync("hello", true), Is.True);
            client.Verify(c => c.Put(1024, 0, It.IsAny<int>(), "hello"), Times.Once);
        }

        [Test]
        public async Task SendUnprocessed_PutsOnPoison()
        {
            client.Setup(c => c.Use("q-high-unprocessed"));
            client.Setup(c => c.Put(1024, 0, It.IsAny<int>(), "bad")).Returns(2UL);
            client.Setup(c => c.Use("q-high"));
            Assert.That(await provider.SendUnprocessedMessageAsync("bad"), Is.True);
        }

        [Test]
        public void GetMessages_ReservesAndReturnsHooks()
        {
            client.Setup(c => c.Watch("q-high"));
            client.Setup(c => c.Ignore("q-low"));
            client.Setup(c => c.Ignore("q-high-unprocessed"));
            client.Setup(c => c.Ignore("default"));
            client.SetupSequence(c => c.Reserve(0))
                .Returns(new BeanstalkdJob(9, "body"))
                .Returns((BeanstalkdJob)null);
            client.Setup(c => c.StatsJobReserves(9UL)).Returns(3);

            IList<object> msgs = provider.GetMessages(5, true);
            Assert.That(msgs.Count, Is.EqualTo(1));
            Assert.That(provider.GetMessageBody(msgs[0]), Is.EqualTo("body"));
            Assert.That(provider.GetReceiveCount(msgs[0]), Is.EqualTo(3));
        }

        [Test]
        public async Task DeleteMessageAsync_DeletesJob()
        {
            var hook = new BeanstalkdProvider.BeanstalkdHook(42, "x", 1);
            client.Setup(c => c.Delete(42UL));
            await provider.DeleteMessageAsync(hook, true);
            client.VerifyAll();
        }

        [Test]
        public async Task ApproximateLengths_UseStatsTube()
        {
            client.Setup(c => c.StatsTubeCurrentJobsReady("q-high")).Returns(4);
            client.Setup(c => c.StatsTubeCurrentJobsReady("q-high-unprocessed")).Returns(2);
            Assert.That(await provider.GetApproximateQueueLength(true), Is.EqualTo(4));
            Assert.That(await provider.GetApproximateUnprocessedQueueLength(), Is.EqualTo(2));
        }

        [Test]
        public void ReceiveUnprocessed_UsesPoisonTube()
        {
            var loose = new Mock<IBeanstalkdClient>(MockBehavior.Loose);
            loose.Setup(c => c.Reserve(0)).Returns((BeanstalkdJob)null);
            var config = new BeanstalkdProviderConfig("n", "localhost", 11300, "q-high", "q-low");
            var p = new BeanstalkdProvider(config, loose.Object);
            Assert.That(p.ReceiveUnprocessedMessages(1), Is.Empty);
            loose.Verify(c => c.Watch("q-high-unprocessed"), Times.AtLeastOnce);
        }

        [Test]
        public async Task DeleteUnprocessed_Deletes()
        {
            var hook = new BeanstalkdProvider.BeanstalkdHook(7, "p", 1);
            client.Setup(c => c.Delete(7UL));
            await provider.DeleteUnprocessedMessageAsync(hook);
            client.Verify(c => c.Delete(7UL), Times.Once);
        }

        [Test]
        public void GetReceiveCount_FallsBackOnStatsFailure()
        {
            var hook = new BeanstalkdProvider.BeanstalkdHook(1, "b", 5);
            client.Setup(c => c.StatsJobReserves(1UL)).Throws(new InvalidOperationException("boom"));
            Assert.That(provider.GetReceiveCount(hook), Is.EqualTo(5));
            Assert.That(provider.GetReceiveCount("nope"), Is.EqualTo(1));
        }

        [Test]
        public void PeekUnprocessed_UsesPoison()
        {
            client.Setup(c => c.Watch("q-high-unprocessed"));
            client.Setup(c => c.Watch("q-high"));
            client.Setup(c => c.Ignore(It.IsAny<string>()));
            client.Setup(c => c.Reserve(0)).Returns((BeanstalkdJob)null);
            Assert.That(provider.PeekUnprocessedMessages(2).Count, Is.EqualTo(0));
        }

        [Test]
        public void GetReceiveCount_NonHook_ReturnsOne()
        {
            Assert.That(provider.GetReceiveCount("x"), Is.EqualTo(1));
        }

        [Test]
        public void GetMessageBody_Fallback()
        {
            Assert.That(provider.GetMessageBody(5), Is.EqualTo("5"));
        }

        [Test]
        public void Reserve_StatsFailure_UsesHint()
        {
            client.Setup(c => c.Watch("q-high"));
            client.Setup(c => c.Ignore("q-low"));
            client.Setup(c => c.Ignore("q-high-unprocessed"));
            client.Setup(c => c.Ignore("default"));
            client.SetupSequence(c => c.Reserve(0))
                .Returns(new BeanstalkdJob(1, "b"))
                .Returns((BeanstalkdJob)null);
            client.Setup(c => c.StatsJobReserves(1UL)).Throws(new System.IO.IOException("stats"));
            IList<object> msgs = provider.GetMessages(2, true);
            Assert.That(msgs.Count, Is.EqualTo(1));
        }

        [Test]
        public void WatchOnly_IgnoreIOException_IsSwallowed()
        {
            client.Setup(c => c.Watch("q-high"));
            client.Setup(c => c.Ignore("q-low")).Throws(new System.IO.IOException("ignore"));
            client.Setup(c => c.Ignore("q-high-unprocessed"));
            client.Setup(c => c.Ignore("default"));
            client.Setup(c => c.Reserve(0)).Returns((BeanstalkdJob)null);
            Assert.That(provider.GetMessages(1, true).Count, Is.EqualTo(0));
        }

    }
}
