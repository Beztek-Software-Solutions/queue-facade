// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using Queue;

    [TestFixture]
    public class AbstractMessageProcessorTest
    {
        [Test]
        public void ProcessTest_HappyPath()
        {
            TestMessageProcessor messageProcessor = new TestMessageProcessor();
            Message message = new Message("Test message", "abc");
            messageProcessor.Process(message).Wait();
            Assert.AreEqual(1, messageProcessor.GetProcessCount());
            Assert.AreEqual(0, messageProcessor.GetProcessListCount());
        }

        [Test]
        public void ProcessListTest_HappyPath()
        {
            TestMessageProcessor messageProcessor = new TestMessageProcessor();
            List<Message> messageList = new List<Message>();
            messageList.Add(new Message("Test message1", "abc"));
            messageList.Add(new Message("Test message2", "abc"));
            List<bool> results = messageProcessor.Process(messageList).Result;
            Assert.AreEqual(2, messageProcessor.GetProcessCount());
            Assert.AreEqual(1, messageProcessor.GetProcessListCount());
            Assert.AreEqual(true, results[0]);
            Assert.AreEqual(true, results[1]);
        }

        [Test]
        public void ProcessTest_Exception()
        {
            TestMessageProcessor messageProcessor = new TestMessageProcessor(true);
            Message message = new Message("Test message", "abc");
            Assert.Throws<AggregateException>(() => _ = messageProcessor.Process(message).Result);
        }

        [Test]
        public void ProcessListTest_Errors()
        {
            TestMessageProcessor messageProcessor = new TestMessageProcessor(true);
            List<Message> messageList = new List<Message>();
            messageList.Add(new Message("Test message1", "abc"));
            messageList.Add(new Message("Test message2", "abc"));
            List<bool> results = messageProcessor.Process(messageList).Result;
            Assert.AreEqual(false, results[0]);
            Assert.AreEqual(false, results[1]);
        }
    }
}
