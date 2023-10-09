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
            Assert.IsNotNull(sqhandler);
        }

        [Test]
        public void AddProcessorToDefaultHandlerTest()
        {
            TestMessageProcessor callback = new TestMessageProcessor();
            IQueueProcessorHandler sqhandler = IQueueProcessorHandler.Default().AddProcessor(typeof(string), callback);
            Assert.IsNotNull(sqhandler);
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
            Assert.IsFalse(result);
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
            Assert.IsTrue(result);
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
            Assert.IsTrue(result[0]);
            Assert.AreEqual(1, callback.GetProcessListCount());
            Assert.AreEqual(2, callback.GetProcessCount());
        }
    }
}
