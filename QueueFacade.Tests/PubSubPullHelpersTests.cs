// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System.Collections.Generic;
    using Beztek.Facade.Queue.Providers;
    using Google.Cloud.PubSub.V1;
    using Google.Protobuf;
    using NUnit.Framework;

    [TestFixture]
    public class PubSubPullHelpersTests
    {
        [Test]
        public void ClampAndAckDeadline()
        {
            Assert.That(PubSubPullHelpers.ClampTake(0, 100), Is.EqualTo(1));
            Assert.That(PubSubPullHelpers.ClampTake(200, 100), Is.EqualTo(100));
            Assert.That(PubSubPullHelpers.AckDeadlineSeconds(500), Is.EqualTo(10));
            Assert.That(PubSubPullHelpers.AckDeadlineSeconds(30_000), Is.EqualTo(30));
        }

        [Test]
        public void ToHookList_AndCollectAckIds()
        {
            Assert.That(PubSubPullHelpers.ToHookList(null), Is.Empty);

            var msg = new ReceivedMessage
            {
                AckId = "ack-1",
                Message = new PubsubMessage { Data = ByteString.CopyFromUtf8("hi") },
            };
            List<object> hooks = PubSubPullHelpers.ToHookList(new[] { msg, null });
            Assert.That(hooks.Count, Is.EqualTo(1));

            Assert.That(PubSubPullHelpers.CollectAckIds(null), Is.Empty);
            Assert.That(PubSubPullHelpers.CollectAckIds(hooks), Is.EqualTo(new[] { "ack-1" }));
            Assert.That(PubSubPullHelpers.CollectAckIds(new object[] { "nope" }), Is.Empty);
        }

        [Test]
        public void DeadlineSecondsForHooks()
        {
            Assert.That(PubSubPullHelpers.DeadlineSecondsForHooks(null, false, 30), Is.Null);
            Assert.That(PubSubPullHelpers.DeadlineSecondsForHooks(new List<object>(), false, 30), Is.Null);
            Assert.That(PubSubPullHelpers.DeadlineSecondsForHooks(new List<object> { new object() }, true, 30), Is.EqualTo(0));
            Assert.That(PubSubPullHelpers.DeadlineSecondsForHooks(new List<object> { new object() }, false, 30), Is.EqualTo(30));
        }
    }
}
