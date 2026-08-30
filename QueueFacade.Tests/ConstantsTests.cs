// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using NUnit.Framework;
    using Queue;

    [TestFixture]
    public class ConstantsTests
    {
        [Test]
        public void DefaultUnprocessedQueueName_AppendsSuffix()
        {
            string result = Constants.DefaultUnprocessedQueueName("orders", QueueNameValidator.MaxLength);
            Assert.That(result, Is.EqualTo("orders-unprocessed"));
        }

        [Test]
        public void DefaultUnprocessedQueueName_TruncatesWhenTooLong()
        {
            string high = new string('a', 55);
            string result = Constants.DefaultUnprocessedQueueName(high, QueueNameValidator.MaxLength);

            Assert.That(result.Length, Is.EqualTo(QueueNameValidator.MaxLength));
            Assert.That(result, Does.EndWith(Constants.UnprocessedQueueSuffix));
        }

        [Test]
        public void DefaultUnprocessedQueueName_RejectsEmptyHighPriorityQueue()
        {
            Assert.Throws<ArgumentException>(() =>
                Constants.DefaultUnprocessedQueueName("", QueueNameValidator.MaxLength));
        }

        [Test]
        public void DefaultUnprocessedQueueName_RejectsMaxLengthTooSmallForSuffix()
        {
            Assert.Throws<ArgumentException>(() =>
                Constants.DefaultUnprocessedQueueName("orders", maxLength: 5));
        }
    }
}
