// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System.Threading.Tasks;
    using Beztek.Facade.Queue.Providers;
    using Moq;
    using NUnit.Framework;
    using StackExchange.Redis;

    [TestFixture]
    public class RedisQueueCodecTests
    {
        [Test]
        public void EncodeAndParse_ReadyRoundTrip()
        {
            string encoded = RedisQueueCodec.EncodeReady("body|with|pipes", 3);
            Assert.That(RedisQueueCodec.TryParseReady(encoded, out string body, out int attempt), Is.True);
            Assert.That(attempt, Is.EqualTo(3));
            Assert.That(body, Is.EqualTo("body|with|pipes"));
        }

        [Test]
        public void TryParseReady_RejectsBadShapes()
        {
            Assert.That(RedisQueueCodec.TryParseReady(RedisValue.Null, out _, out _), Is.False);
            Assert.That(RedisQueueCodec.TryParseReady("nopipe", out _, out _), Is.False);
            Assert.That(RedisQueueCodec.TryParseReady("|body", out _, out _), Is.False);
            Assert.That(RedisQueueCodec.TryParseReady("x|body", out _, out _), Is.False);
        }

        [Test]
        public void EncodeAndParse_ProcessingRoundTrip()
        {
            string encoded = RedisQueueCodec.EncodeProcessing("payload", 2, 1_700_000_000_000);
            Assert.That(RedisQueueCodec.TryParseProcessing(encoded, out long visibleAt, out string body, out int attempt), Is.True);
            Assert.That(visibleAt, Is.EqualTo(1_700_000_000_000));
            Assert.That(attempt, Is.EqualTo(2));
            Assert.That(body, Is.EqualTo("payload"));
        }

        [Test]
        public void TryParseProcessing_RejectsBadShapes()
        {
            Assert.That(RedisQueueCodec.TryParseProcessing(RedisValue.Null, out _, out _, out _), Is.False);
            Assert.That(RedisQueueCodec.TryParseProcessing("onlyone", out _, out _, out _), Is.False);
            Assert.That(RedisQueueCodec.TryParseProcessing("a|2|body", out _, out _, out _), Is.False);
            Assert.That(RedisQueueCodec.TryParseProcessing("100|x|body", out _, out _, out _), Is.False);
            Assert.That(RedisQueueCodec.TryParseProcessing("|2|body", out _, out _, out _), Is.False);
        }

        [Test]
        public void ReadyKey_AndProcKey_ShareHashTag()
        {
            Assert.That(RedisQueueCodec.ReadyKey("booth"), Is.EqualTo("qf:{booth}:ready"));
            Assert.That(RedisQueueCodec.ProcKey("booth"), Is.EqualTo("qf:{booth}:proc"));
        }

        [Test]
        public void IsExpired_ComparesTimestamps()
        {
            Assert.That(RedisQueueCodec.IsExpired(10, 10), Is.True);
            Assert.That(RedisQueueCodec.IsExpired(10, 11), Is.True);
            Assert.That(RedisQueueCodec.IsExpired(12, 11), Is.False);
        }

        [Test]
        public async Task DeleteMessageAsync_RemovesProcessingPayload()
        {
            var db = new Mock<IDatabase>(MockBehavior.Strict);
            var config = new RedisQueueProviderConfig("n", "localhost:6379", "q-high");
            var provider = new RedisQueueProvider(config, db.Object);

            RedisValue payload = RedisQueueCodec.EncodeProcessing("m", 1, 1);
            var hook = new RedisQueueProvider.RedisHook("q-high", "m", 1, payload);
            db.Setup(d => d.ListRemoveAsync(It.Is<RedisKey>(k => k == "qf:{q-high}:proc"), payload, 1, CommandFlags.None))
                .ReturnsAsync(1);

            await provider.DeleteMessageAsync(hook, isHighPriorityQueue: true);
            db.VerifyAll();
        }

        [Test]
        public async Task DeleteMessageAsync_IgnoresNonHooks()
        {
            var db = new Mock<IDatabase>(MockBehavior.Strict);
            var config = new RedisQueueProviderConfig("n", "localhost:6379", "q-high");
            var provider = new RedisQueueProvider(config, db.Object);
            await provider.DeleteMessageAsync("not-a-hook", true);
            await provider.DeleteMessageAsync(new RedisQueueProvider.RedisHook("q-high", "m", 1, processingPayload: null), true);
        }

        [Test]
        public void ReclaimExpired_MovesDueItemsAndDropsCorrupt()
        {
            var db = new Mock<IDatabase>(MockBehavior.Strict);
            var config = new RedisQueueProviderConfig("n", "localhost:6379", "q-high", visibilityTimeoutMilliseconds: 1000);
            var provider = new RedisQueueProvider(config, db.Object);

            long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            RedisValue due = RedisQueueCodec.EncodeProcessing("a", 1, now - 5_000);
            RedisValue future = RedisQueueCodec.EncodeProcessing("b", 1, now + 60_000);
            RedisValue corrupt = (RedisValue)"bad";

            db.Setup(d => d.ListRange(It.Is<RedisKey>(k => k == "qf:{q-high}:proc"), -RedisQueueProvider.MaxReclaimScan, -1, CommandFlags.None))
                .Returns(new[] { due, future, corrupt });
            db.Setup(d => d.ListRemove(It.Is<RedisKey>(k => k == "qf:{q-high}:proc"), corrupt, 1, CommandFlags.None))
                .Returns(1);
            db.Setup(d => d.ListRemove(It.Is<RedisKey>(k => k == "qf:{q-high}:proc"), due, 1, CommandFlags.None))
                .Returns(1);
            db.Setup(d => d.ListLeftPush(
                    It.Is<RedisKey>(k => k == "qf:{q-high}:ready"),
                    It.Is<RedisValue>(v => v == RedisQueueCodec.EncodeReady("a", 2)),
                    When.Always,
                    CommandFlags.None))
                .Returns(1);

            provider.ReclaimExpired("q-high");

            db.VerifyAll();
        }

        [Test]
        public async Task SendAndPeekUnprocessed_UsesReadyList()
        {
            var db = new Mock<IDatabase>(MockBehavior.Strict);
            var config = new RedisQueueProviderConfig("n", "localhost:6379", "q-high");
            var provider = new RedisQueueProvider(config, db.Object);

            db.Setup(d => d.ListLeftPushAsync(
                    It.Is<RedisKey>(k => k == "qf:{q-high-unprocessed}:ready"),
                    It.IsAny<RedisValue>(),
                    When.Always,
                    CommandFlags.None))
                .ReturnsAsync(1);
            Assert.That(await provider.SendUnprocessedMessageAsync("poison-body"), Is.True);

            RedisValue encoded = RedisQueueCodec.EncodeReady("poison-body", 1);
            db.Setup(d => d.ListRange(It.Is<RedisKey>(k => k == "qf:{q-high-unprocessed}:ready"), 0, 0, CommandFlags.None))
                .Returns(new[] { encoded });
            var peeked = provider.PeekUnprocessedMessages(1);
            Assert.That(peeked.Count, Is.EqualTo(1));
            Assert.That(provider.GetMessageBody(peeked[0]), Is.EqualTo("poison-body"));
        }

        [Test]
        public void GetMessages_PopsReadyIntoProcessing()
        {
            var db = new Mock<IDatabase>(MockBehavior.Strict);
            var config = new RedisQueueProviderConfig("n", "localhost:6379", "q-high", visibilityTimeoutMilliseconds: 1000);
            var provider = new RedisQueueProvider(config, db.Object);

            RedisValue ready = RedisQueueCodec.EncodeReady("hello", 1);
            db.Setup(d => d.ListRange(It.Is<RedisKey>(k => k == "qf:{q-high}:proc"), -RedisQueueProvider.MaxReclaimScan, -1, CommandFlags.None))
                .Returns(System.Array.Empty<RedisValue>());
            db.SetupSequence(d => d.ListRightPopLeftPush(
                    It.Is<RedisKey>(k => k == "qf:{q-high}:ready"),
                    It.Is<RedisKey>(k => k == "qf:{q-high}:proc"),
                    CommandFlags.None))
                .Returns(ready)
                .Returns(RedisValue.Null);
            db.Setup(d => d.ListRemove(It.Is<RedisKey>(k => k == "qf:{q-high}:proc"), ready, 1, CommandFlags.None))
                .Returns(1);
            db.Setup(d => d.ListLeftPush(
                    It.Is<RedisKey>(k => k == "qf:{q-high}:proc"),
                    It.IsAny<RedisValue>(),
                    When.Always,
                    CommandFlags.None))
                .Returns(1);

            var msgs = provider.GetMessages(2, true);
            Assert.That(msgs.Count, Is.EqualTo(1));
            Assert.That(provider.GetReceiveCount(msgs[0]), Is.EqualTo(1));
            Assert.That(provider.GetMessageBody(msgs[0]), Is.EqualTo("hello"));
        }
    }
}
