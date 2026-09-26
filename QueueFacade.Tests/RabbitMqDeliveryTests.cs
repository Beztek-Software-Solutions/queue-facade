// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System.Collections.Generic;
    using Beztek.Facade.Queue.Providers;
    using NUnit.Framework;

    [TestFixture]
    public class RabbitMqDeliveryTests
    {
        [Test]
        public void ParseAttempt_DefaultsAndParses()
        {
            Assert.That(RabbitMqDelivery.ParseAttempt(null), Is.EqualTo(1));
            Assert.That(RabbitMqDelivery.ParseAttempt(new Dictionary<string, object>()), Is.EqualTo(1));
            Assert.That(RabbitMqDelivery.ParseAttempt(new Dictionary<string, object> { ["x-attempt"] = null }), Is.EqualTo(1));
            Assert.That(RabbitMqDelivery.ParseAttempt(new Dictionary<string, object> { ["x-attempt"] = "nope" }), Is.EqualTo(1));
            Assert.That(RabbitMqDelivery.ParseAttempt(new Dictionary<string, object> { ["x-attempt"] = "0" }), Is.EqualTo(1));
            Assert.That(RabbitMqDelivery.ParseAttempt(new Dictionary<string, object> { ["x-attempt"] = "4" }), Is.EqualTo(4));
            Assert.That(RabbitMqDelivery.ParseAttempt(new Dictionary<string, object> { ["x-attempt"] = 7 }), Is.EqualTo(7));
        }

        [Test]
        public void ClampTake_Bounds()
        {
            Assert.That(RabbitMqDelivery.ClampTake(0, 32), Is.EqualTo(1));
            Assert.That(RabbitMqDelivery.ClampTake(100, 32), Is.EqualTo(32));
            Assert.That(RabbitMqDelivery.ClampTake(5, 32), Is.EqualTo(5));
        }
    }
}
