// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Globalization;
    using NUnit.Framework;
    using Queue;
    using Queue.Providers;

    /// <summary>
    /// Test cases for Queue Name Validations.
    /// </summary>
    [TestFixture]
    public class QueueNameValidatorTests
    {
        [Test]
        public void Given_Short_QueueName_Throws_ValidationException()
        {
            Assert.Throws<ArgumentException>(() => AzureQueueNameValidator.ValidateQueueName("t"));
        }

        [Test]
        public void Given_Long_QueueName_Throws_ValidationException()
        {
            Assert.Throws<ArgumentException>(() =>
                AzureQueueNameValidator.ValidateQueueName("sdkhfkjsdhfkshdfkshdfksdasjdlkasjdlkasjdlasdasdasdasjdlasdhfksdhfksdhfs"));
        }

        [Test]
        public void Given_Whitespace_QueueName_Throws_ValidationException()
        {
            Assert.Throws<ArgumentException>(() => AzureQueueNameValidator.ValidateQueueName(" "));
        }

        [Test]
        public void Given_InvalidCharacters_QueueName_Throws_ValidationException()
        {
            Assert.Throws<ArgumentException>(() => AzureQueueNameValidator.ValidateQueueName("@#(_@$"));

            Assert.Throws<ArgumentException>(() =>
                AzureQueueNameValidator.ValidateQueueName("test name"));

            Assert.Throws<ArgumentException>(() =>
                AzureQueueNameValidator.ValidateQueueName("test/name"));

            Assert.Throws<ArgumentException>(() =>
                AzureQueueNameValidator.ValidateQueueName("test_name"));

            Assert.Throws<ArgumentException>(() =>
                AzureQueueNameValidator.ValidateQueueName("CAPITAL-NOT-ALLOWED"));

        }

        [Test]
        public void Given_Reserved_QueueName_Throws_ValidationException()
        {
            string reservedQueueName = "test";

            string message = Assert.Throws<ArgumentException>(() => AzureQueueNameValidator.ValidateQueueName(reservedQueueName)).Message;

            Assert.That(message, Is.EqualTo(string.Format(CultureInfo.InvariantCulture, Constants.InvalidResourceReservedName, reservedQueueName)));
        }

        [Test]
        public void Given_Valid_QueueName_Throws_No_Exception()
        {
            AzureQueueNameValidator.ValidateQueueName("testname");
            AzureQueueNameValidator.ValidateQueueName("test-valid-queue");
            AzureQueueNameValidator.ValidateQueueName("test-queue-1");
            AzureQueueNameValidator.ValidateQueueName("997bafac-245a-4217-85ec-fc8cc93ecbbc");
        }
    }
}