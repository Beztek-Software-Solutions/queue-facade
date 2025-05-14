// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using Queue;

    [TestFixture]
    public class DefaultProcessorHandlerTest
    {
        DefaultProcessorHandler handler = null;

        [SetUp]
        public void TestInitialize()
        {
            handler = new DefaultProcessorHandler();
        }

        [Test]
        public void AddProcessorTest()
        {
            TestMessageProcessor callback = new TestMessageProcessor();
            IQueueProcessorHandler sqhandler = handler.AddProcessor(typeof(string), callback);
            Assert.That(sqhandler, Is.Not.Null);
        }

        [Test]
        public void AddProcessorToDefaultHandlerTest()
        {
            TestMessageProcessor callback = new TestMessageProcessor();
            IQueueProcessorHandler sqhandler = IQueueProcessorHandler.Default().AddProcessor(typeof(string), callback);
            Assert.That(sqhandler, Is.Not.Null);
        }


        [Test]
        public void ProcessTest()
        {
            Message message = new Message {
                ActivityId = "id",
                RawMessage = "message",
                MessageType = "type"
            };
            bool result = handler.Process(message).Result;
            Assert.That(result, Is.False);
        }

        [Test]
        public void ProcessSingleMessageTest()
        {
            TestMessageProcessor callback = new TestMessageProcessor();
            _ = handler.AddProcessor(typeof(string), callback);

            Message message = new Message {
                ActivityId = "id",
                RawMessage = "message",
                MessageType = "System.String"
            };
            bool result = handler.Process(message).Result;
            Assert.That(result, Is.True);
        }

        [Test]
        public void ProcessListTest()
        {
            TestMessageProcessor callback = new TestMessageProcessor();
            _ = handler.AddProcessor(typeof(string), callback);

            List<Message> messageList = new List<Message>();
            foreach (string id in new string[] { "id1", "id2" })
            {
                Message message = new Message {
                    ActivityId = id,
                    RawMessage = "message",
                    MessageType = "System.String"
                };
                messageList.Add(message);
            }

            List<bool> result = handler.Process(messageList).Result;
            Assert.That(result[0], Is.True);
            Assert.That(1, Is.EqualTo(callback.GetProcessListCount()));
            Assert.That(2, Is.EqualTo(callback.GetProcessCount()));
        }
    }
}
