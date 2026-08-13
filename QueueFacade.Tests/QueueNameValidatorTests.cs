// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Globalization;
    using NUnit.Framework;
    using Queue;

    /// <summary>
    /// Portable queue name rules (Azure ∩ SQS common denominator).
    /// </summary>
    [TestFixture]
    public class QueueNameValidatorTests
    {
        [Test]
        public void Given_Short_QueueName_Throws_ValidationException()
        {
            Assert.Throws<ArgumentException>(() => QueueNameValidator.ValidateQueueName("t"));
        }

        [Test]
        public void Given_Long_QueueName_Throws_ValidationException()
        {
            Assert.Throws<ArgumentException>(() =>
                QueueNameValidator.ValidateQueueName("sdkhfkjsdhfkshdfkshdfksdasjdlkasjdlkasjdlasdasdasdasjdlasdhfksdhfksdhfs"));
        }

        [Test]
        public void Given_Whitespace_QueueName_Throws_ValidationException()
        {
            Assert.Throws<ArgumentException>(() => QueueNameValidator.ValidateQueueName(" "));
        }

        [Test]
        public void Given_InvalidCharacters_QueueName_Throws_ValidationException()
        {
            Assert.Throws<ArgumentException>(() => QueueNameValidator.ValidateQueueName("@#(_@$"));

            Assert.Throws<ArgumentException>(() =>
                QueueNameValidator.ValidateQueueName("test name"));

            Assert.Throws<ArgumentException>(() =>
                QueueNameValidator.ValidateQueueName("test/name"));

            Assert.Throws<ArgumentException>(() =>
                QueueNameValidator.ValidateQueueName("test_name"));

            Assert.Throws<ArgumentException>(() =>
                QueueNameValidator.ValidateQueueName("CAPITAL-NOT-ALLOWED"));
        }

        [Test]
        public void Given_Reserved_QueueName_Throws_ValidationException()
        {
            string reservedQueueName = "test";

            string message = Assert.Throws<ArgumentException>(() => QueueNameValidator.ValidateQueueName(reservedQueueName)).Message;

            Assert.That(message, Is.EqualTo(string.Format(CultureInfo.InvariantCulture, Constants.InvalidResourceReservedName, reservedQueueName)));
        }

        [Test]
        public void Given_Valid_QueueName_Throws_No_Exception()
        {
            QueueNameValidator.ValidateQueueName("testname");
            QueueNameValidator.ValidateQueueName("test-valid-queue");
            QueueNameValidator.ValidateQueueName("test-queue-1");
            QueueNameValidator.ValidateQueueName("997bafac-245a-4217-85ec-fc8cc93ecbbc");
        }
    }
}
