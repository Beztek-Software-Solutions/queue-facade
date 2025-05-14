// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NUnit.Framework;
    using Queue.Providers;

    [TestFixture]
    public class TestLocalMemoryQueueProvider
    {
        private readonly ILogger logger = new ServiceCollection()
                .AddLogging((loggingBuilder) => loggingBuilder
                    .SetMinimumLevel(LogLevel.Warning)
                    .AddConsole()
                    ).BuildServiceProvider().GetService<ILoggerFactory>().CreateLogger<TestLocalMemoryQueueProvider>();

        public TestLocalMemoryQueueProvider()
        {
            string isLocal = Environment.GetEnvironmentVariable("IsLocal", EnvironmentVariableTarget.User);
        }

        [Test]
        public async Task DeleteMessageAsyncTest()
        {
            LocalMemoryQueueProvider queueProvider = new LocalMemoryQueueProvider(logger, false);
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                _ = queueProvider.SendMessageAsync("test message 1", isHighPriorityQueue).Result;
                _ = queueProvider.SendMessageAsync("test message 2", isHighPriorityQueue).Result;
                IList<object> messages = queueProvider.GetMessages(10, isHighPriorityQueue);
                Assert.That(2, Is.EqualTo(messages.Count));
                Assert.That(0, Is.EqualTo(queueProvider.GetNumMessages(isHighPriorityQueue)));
                Assert.That(2, Is.EqualTo(queueProvider.GetNumProcessingMessages(isHighPriorityQueue)));
                await queueProvider.DeleteMessageAsync("test message 1", isHighPriorityQueue).ConfigureAwait(false);
                Assert.That(1, Is.EqualTo(queueProvider.GetNumProcessingMessages(isHighPriorityQueue)));
            }
        }

        [Test]
        public async Task GetMesageBodyTest()
        {
            LocalMemoryQueueProvider queueProvider = new LocalMemoryQueueProvider(logger, false);
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                await queueProvider.SendMessageAsync("test message 1", isHighPriorityQueue).ConfigureAwait(false);
                IList<object> messages = queueProvider.GetMessages(10, isHighPriorityQueue);
                Assert.That(1, Is.EqualTo(messages.Count));
                Assert.That("test message 1", Is.EqualTo(queueProvider.GetMessageBody(messages[0])));
            }
        }

        [Test]
        public async Task GetUnprocessedMessageTest()
        {
            LocalMemoryQueueProvider queueProvider = new LocalMemoryQueueProvider(logger, false);
            await queueProvider.SendUnprocessedMessageAsync("test message 1").ConfigureAwait(false);
            Assert.That(1, Is.EqualTo(await queueProvider.GetNumUnprocessedMessages().ConfigureAwait(false)));
        }

        [Test]
        public async Task GetUnhideMessageTest()
        {
            // Create with unhide daemon
            LocalMemoryQueueProvider queueProvider = new LocalMemoryQueueProvider(logger, true);
            queueProvider.VisibilityTimeoutMilliseconds = 10;
            queueProvider.UhhideCheckPeriodMilliseconds = 10;

            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                await queueProvider.SendMessageAsync("test message 1", isHighPriorityQueue).ConfigureAwait(false);
                IList<object> messages = queueProvider.GetMessages(10, isHighPriorityQueue);
                Assert.That(1, Is.EqualTo(messages.Count));
                Assert.That(0, Is.EqualTo(queueProvider.GetMessages(10, isHighPriorityQueue).Count));

                // Message should reappaer after the unhide period
                await Task.Delay(21).ConfigureAwait(false);
                Assert.That(1, Is.EqualTo(queueProvider.GetMessages(10, isHighPriorityQueue).Count));
            }
        }
    }
}