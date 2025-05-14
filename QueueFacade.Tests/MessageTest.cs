// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Queue;

    [TestFixture]
    public class MessageTest
    {
        private readonly string activityId = "myActivityId";
        private readonly string mymessage = "my test message";
        private readonly string mymessageType = "my test message type";

        Message message = null;

        [SetUp]
        public void TestInitialize()
        {
            message = new Message();
        }

        [Test]
        public void ActivityIdTest()
        {
            message.ActivityId = activityId;
            string result = message.ActivityId;
            Assert.That(result, Is.EqualTo(activityId));
        }

        [Test]
        public void MessageTypeTest()
        {
            message.MessageType = mymessageType;
            string result = message.MessageType;
            Assert.That(result, Is.EqualTo(mymessageType));
        }

        [Test]
        public void Message()
        {
            message.RawMessage = mymessage;
            string result = (string)message.RawMessage;
            Assert.That(result, Is.EqualTo(mymessage));
        }

        [Test]
        public void GetMessageObjectTest()
        {
            message.ActivityId = activityId;
            message.MessageType = mymessageType;
            message.RawMessage = JsonConvert.SerializeObject(mymessage);
            string result = message.GetMessageObject<string>();
            Assert.That(result, Is.EqualTo(mymessage));
        }

        [Test]
        public void TypeTest()
        {
            TestTypedMessage testObject = new TestTypedMessage("GetAndReplaceKeyNotPresentAsyncTest-key", "getandputifabsentasync-result", GetNow(), GetNow(), Guid.NewGuid().ToString());
            message.ActivityId = activityId;
            message.MessageType = typeof(TestTypedMessage).ToString();
            message.RawMessage = JsonConvert.SerializeObject(testObject);
            TestTypedMessage result = message.GetMessageObject<TestTypedMessage>();
            Assert.That(result, Is.EqualTo(testObject));
        }

        [Test]
        public void SerializeDeserializeTest()
        {
            TestTypedMessage testObject = new TestTypedMessage("GetAndReplaceKeyNotPresentAsyncTest-key", "getandputifabsentasync-result", GetNow(), GetNow(), Guid.NewGuid().ToString());
            message = new Message(testObject, activityId);
            string stringifiedMessage = JsonConvert.SerializeObject(message);
            Message unstringifiedMessage = JsonConvert.DeserializeObject<Message>(stringifiedMessage);
            TestTypedMessage result = unstringifiedMessage.GetMessageObject<TestTypedMessage>();
            Assert.That(result, Is.EqualTo(testObject));
        }

        // Internal

        private DateTime GetNow()
        {
            DateTime dt = DateTime.Now;
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, 0);
        }
    }
}
