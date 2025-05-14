// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.IO;
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
            Assert.That(1, Is.EqualTo(messageProcessor.GetProcessCount()));
            Assert.That(0, Is.EqualTo(messageProcessor.GetProcessListCount()));
        }

        [Test]
        public void ProcessListTest_HappyPath()
        {
            TestMessageProcessor messageProcessor = new TestMessageProcessor();
            List<Message> messageList = new List<Message>();
            messageList.Add(new Message("Test message1", "abc"));
            messageList.Add(new Message("Test message2", "abc"));
            List<bool> results = messageProcessor.Process(messageList).Result;
            Assert.That(2, Is.EqualTo(messageProcessor.GetProcessCount()));
            Assert.That(1,Is.EqualTo(messageProcessor.GetProcessListCount()));
            Assert.That(true, Is.EqualTo(results[0]));
            Assert.That(true, Is.EqualTo(results[1]));
        }

        [Test]
        public void ProcessTest_Exception()
        {
            TestMessageProcessor messageProcessor = new TestMessageProcessor(true);
            Message message = new Message("Test message", "abc");
            Assert.Throws<IOException>(() => _ = messageProcessor.Process(message).Result);
        }

        [Test]
        public void ProcessListTest_Errors()
        {
            TestMessageProcessor messageProcessor = new TestMessageProcessor(true);
            List<Message> messageList = new List<Message>();
            messageList.Add(new Message("Test message1", "abc"));
            messageList.Add(new Message("Test message2", "abc"));
            List<bool> results = messageProcessor.Process(messageList).Result;
            Assert.That(false, Is.EqualTo(results[0]));
            Assert.That(false, Is.EqualTo(results[1]));
        }
    }
}
