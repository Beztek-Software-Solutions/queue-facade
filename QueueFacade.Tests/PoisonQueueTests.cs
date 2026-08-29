// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NUnit.Framework;
    using Queue;
    using Queue.Providers;

    [TestFixture]
    public class PoisonQueueTests
    {
        private readonly ILogger logger = new LoggerFactory().CreateLogger<PoisonQueueTests>();

        [Test]
        public async Task FalseResult_MovesToPoisonImmediately()
        {
            LocalMemoryQueueProvider provider = new LocalMemoryQueueProvider(this.logger, implementUnhide: false, visibilityTimeoutMilliseconds: 50);
            QueueClient client = new QueueClient("poison-false", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor(returnFalseInsteadOfThrow: true);
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("payload", true, "act-1"), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);
            Assert.That(hooks.Count, Is.EqualTo(1));

            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, 1, 100, maxProcessingAttempts: 5);
            await client.ProcessMessage(hooks[0], true);

            Assert.That(await provider.GetNumUnprocessedMessages(), Is.EqualTo(1));
            Assert.That(provider.GetNumMessages(true), Is.EqualTo(0));
            Assert.That(provider.GetNumProcessingMessages(true), Is.EqualTo(0));

            IReadOnlyList<Message> peeked = await client.PeekUnprocessedMessagesAsync();
            Assert.That(peeked.Count, Is.EqualTo(1));
            Assert.That(ReadStringPayload(peeked[0]), Is.EqualTo("payload"));
            Assert.That(peeked[0].ActivityId, Is.EqualTo("act-1"));
        }

        [Test]
        public async Task Exception_BelowMaxAttempts_LeavesForVisibility()
        {
            LocalMemoryQueueProvider provider = new LocalMemoryQueueProvider(this.logger, implementUnhide: false, visibilityTimeoutMilliseconds: 50);
            QueueClient client = new QueueClient("poison-retry", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor();
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("payload", true), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);

            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, 1, 100, maxProcessingAttempts: 3);
            await client.ProcessMessage(hooks[0], true);

            Assert.That(await provider.GetNumUnprocessedMessages(), Is.EqualTo(0));
            Assert.That(provider.GetNumProcessingMessages(true), Is.EqualTo(1));
            Assert.That(processor.ProcessCount, Is.EqualTo(1));
            Assert.That(provider.GetReceiveCount(hooks[0]), Is.EqualTo(1));
        }

        [Test]
        public async Task Exception_AtMaxAttempts_MovesToPoison()
        {
            LocalMemoryQueueProvider provider = new LocalMemoryQueueProvider(this.logger, implementUnhide: false, visibilityTimeoutMilliseconds: 50);
            QueueClient client = new QueueClient("poison-max", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor();
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("payload", true, "act-max"), Is.True);

            // Simulate third delivery (max attempts = 3)
            IList<object> hooks = provider.GetMessages(1, true);
            string body = provider.GetMessageBody(hooks[0]);
            provider.receiveCounts[body] = 3;

            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, 1, 100, maxProcessingAttempts: 3);
            await client.ProcessMessage(hooks[0], true);

            Assert.That(await provider.GetNumUnprocessedMessages(), Is.EqualTo(1));
            Assert.That(provider.GetNumProcessingMessages(true), Is.EqualTo(0));

            IReadOnlyList<Message> peeked = await client.PeekUnprocessedMessagesAsync();
            Assert.That(peeked.Count, Is.EqualTo(1));
            Assert.That(peeked[0].ProcessingAttempt, Is.EqualTo(3));
            Assert.That(ReadStringPayload(peeked[0]), Is.EqualTo("payload"));
        }

        [Test]
        public async Task RequeueUnprocessed_MovesBackToPrimary()
        {
            LocalMemoryQueueProvider provider = new LocalMemoryQueueProvider(this.logger, implementUnhide: false, visibilityTimeoutMilliseconds: 50);
            QueueClient client = new QueueClient("poison-requeue", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor(returnFalseInsteadOfThrow: true);
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("requeue-me", true, "act-rq"), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, 1, 100, maxProcessingAttempts: 2);
            await client.ProcessMessage(hooks[0], true);

            Assert.That(await client.GetApproximateUnprocessedQueueLength(), Is.EqualTo(1));

            int requeued = await client.RequeueUnprocessedMessagesAsync(maxMessages: 10, useHighPriorityQueue: true);
            Assert.That(requeued, Is.EqualTo(1));
            Assert.That(await client.GetApproximateUnprocessedQueueLength(), Is.EqualTo(0));
            Assert.That(provider.GetNumMessages(true), Is.EqualTo(1));

            IList<object> primary = provider.GetMessages(1, true);
            Message restored = JsonSerializer.Deserialize<Message>(provider.GetMessageBody(primary[0]));
            Assert.That(ReadStringPayload(restored), Is.EqualTo("requeue-me"));
            Assert.That(restored.ActivityId, Is.EqualTo("act-rq"));
            Assert.That(provider.GetReceiveCount(primary[0]), Is.EqualTo(1));
        }

        private static string ReadStringPayload(Message message)
        {
            if (message.RawMessage is JsonElement element)
            {
                return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
            }

            return Convert.ToString(message.RawMessage);
        }
    }
}
