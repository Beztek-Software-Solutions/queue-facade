// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class NamedQueueConfigValidationTests
    {
        [Test]
        public void ResolvePoisonQueue_Default_WhenUnprocessedNull()
        {
            string poison = NamedQueueConfigValidation.ResolvePoisonQueue(
                "q-high",
                unprocessedQueue: null,
                validateName: _ => { });
            Assert.That(poison, Is.EqualTo("q-high-unprocessed"));
        }

        [Test]
        public void ResolvePoisonQueue_TrimsExplicit()
        {
            string poison = NamedQueueConfigValidation.ResolvePoisonQueue(
                "q-high",
                "q-low",
                "  poison-q  ",
                validateName: _ => { });
            Assert.That(poison, Is.EqualTo("poison-q"));
        }

        [Test]
        public void ResolvePoisonQueue_RejectsSameAsHigh()
        {
            Assert.Throws<ArgumentException>(() =>
                NamedQueueConfigValidation.ResolvePoisonQueue(
                    "q-high",
                    "q-high",
                    validateName: _ => { }));
        }

        [Test]
        public void RequireNonEmpty_Throws()
        {
            Assert.Throws<ArgumentException>(() => NamedQueueConfigValidation.RequireNonEmpty("", "msg"));
        }
    }
}
