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

        [Test]
        public async Task ApplicationException_DeletesWithoutPoison()
        {
            LocalMemoryQueueProvider provider = new LocalMemoryQueueProvider(this.logger, implementUnhide: false, visibilityTimeoutMilliseconds: 50);
            QueueClient client = new QueueClient("poison-app-ex", provider, this.logger);
            var processor = new ApplicationExceptionMessageProcessor();
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("discard", true), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, 1, 100, maxProcessingAttempts: 5);
            await client.ProcessMessage(hooks[0], true);

            Assert.That(await provider.GetNumUnprocessedMessages(), Is.EqualTo(0));
            Assert.That(provider.GetNumMessages(true), Is.EqualTo(0));
            Assert.That(provider.GetNumProcessingMessages(true), Is.EqualTo(0));
        }

        [Test]
        public async Task ProcessMessage_EmptyBody_IsNoOp()
        {
            LocalMemoryQueueProvider provider = new LocalMemoryQueueProvider(this.logger, implementUnhide: false, visibilityTimeoutMilliseconds: 50);
            QueueClient client = new QueueClient("poison-empty", provider, this.logger);
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), new TestMessageProcessor());
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, 1, 100, maxProcessingAttempts: 3);

            // LocalMemory GetMessageBody uses ToString(); empty string yields the empty-body path.
            await client.ProcessMessage(string.Empty, true);
            Assert.That(await provider.GetNumUnprocessedMessages(), Is.EqualTo(0));
        }

        [Test]
        public async Task ProcessMessageList_FalseResults_MoveAllToPoison()
        {
            LocalMemoryQueueProvider provider = new LocalMemoryQueueProvider(this.logger, implementUnhide: false, visibilityTimeoutMilliseconds: 50);
            QueueClient client = new QueueClient("poison-batch-false", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor(returnFalseInsteadOfThrow: true);
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("a", true), Is.True);
            Assert.That(await client.Enqueue("b", true), Is.True);
            IList<object> hooks = provider.GetMessages(2, true);
            Assert.That(hooks.Count, Is.EqualTo(2));

            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, batchSize: 2, pollIntervalInMilliseconds: 100, maxProcessingAttempts: 5);
            await client.ProcessMessageList(new List<object>(hooks), true);

            Assert.That(await provider.GetNumUnprocessedMessages(), Is.EqualTo(2));
            Assert.That(provider.GetNumProcessingMessages(true), Is.EqualTo(0));
        }

        [Test]
        public async Task ProcessMessageList_ExceptionAtMaxAttempts_PoisonsBatch()
        {
            LocalMemoryQueueProvider provider = new LocalMemoryQueueProvider(this.logger, implementUnhide: false, visibilityTimeoutMilliseconds: 50);
            QueueClient client = new QueueClient("poison-batch-max", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor();
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("batch-max", true), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);
            string body = provider.GetMessageBody(hooks[0]);
            provider.receiveCounts[body] = 3;

            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, batchSize: 1, pollIntervalInMilliseconds: 100, maxProcessingAttempts: 3);
            await client.ProcessMessageList(new List<object> { hooks[0] }, true);

            Assert.That(await provider.GetNumUnprocessedMessages(), Is.EqualTo(1));
        }

        [Test]
        public async Task ProcessMessageList_EmptyBody_LeavesForRetry()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("poison-batch-empty", provider, this.logger);
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), new TestMessageProcessor());
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, batchSize: 1, 100, 3);

            await client.ProcessMessageList(new List<object> { string.Empty }, true);
            Assert.That(await provider.Inner.GetNumUnprocessedMessages(), Is.EqualTo(0));
        }

        [Test]
        public async Task ProcessMessageList_InvalidJson_LeavesForRetry()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("poison-batch-badjson", provider, this.logger);
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), new TestMessageProcessor());
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, batchSize: 1, 100, 3);

            await client.ProcessMessageList(new List<object> { "not-json{" }, true);
            Assert.That(await provider.Inner.GetNumUnprocessedMessages(), Is.EqualTo(0));
        }

        [Test]
        public async Task FinalizeBatch_DeleteFailureAtMaxAttempts_MaybePoisons()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("poison-maybe", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor(returnFalseInsteadOfThrow: true);
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("maybe-poison", true), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);
            string body = provider.GetMessageBody(hooks[0]);
            provider.Inner.receiveCounts[body] = 3;

            provider.DeleteMessageException = new InvalidOperationException("delete failed");
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, batchSize: 1, 100, maxProcessingAttempts: 3);

            await client.ProcessMessageList(new List<object>(hooks), true);

            // False result already moved the payload; MaybePoison retries send then delete (also fails) — poison depth stays 1+.
            Assert.That(await provider.Inner.GetNumUnprocessedMessages(), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task FinalizeBatch_DeleteFailureBelowMax_DoesNotDoublePoison()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("poison-maybe-low", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor(returnFalseInsteadOfThrow: true);
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("low-attempt", true), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);
            // receive count remains 1

            provider.DeleteMessageException = new InvalidOperationException("delete failed");
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, batchSize: 1, 100, maxProcessingAttempts: 5);

            await client.ProcessMessageList(new List<object>(hooks), true);

            // First false result still enqueues poison once; MaybePoison returns early below max.
            Assert.That(await provider.Inner.GetNumUnprocessedMessages(), Is.EqualTo(1));
        }

        [Test]
        public async Task Exception_AtMaxAttempts_PoisonSendFailure_IsSwallowed()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("poison-send-fail", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor();
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("payload", true), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);
            string body = provider.GetMessageBody(hooks[0]);
            provider.Inner.receiveCounts[body] = 3;

            provider.SendUnprocessedException = new InvalidOperationException("poison send failed");
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, 1, 100, maxProcessingAttempts: 3);

            Assert.DoesNotThrowAsync(async () => await client.ProcessMessage(hooks[0], true));
            Assert.That(await provider.Inner.GetNumUnprocessedMessages(), Is.EqualTo(0));
        }

        [Test]
        public async Task ProcessMessageList_BatchThrow_PoisonSendFailure_IsSwallowed()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("poison-batch-send-fail", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor();
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("payload", true), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);
            string body = provider.GetMessageBody(hooks[0]);
            provider.Inner.receiveCounts[body] = 3;

            provider.SendUnprocessedException = new InvalidOperationException("poison send failed");
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, batchSize: 1, 100, maxProcessingAttempts: 3);

            Assert.DoesNotThrowAsync(async () => await client.ProcessMessageList(new List<object>(hooks), true));
            Assert.That(await provider.Inner.GetNumUnprocessedMessages(), Is.EqualTo(0));
        }

        [Test]
        public async Task ApplicationException_DeleteFailure_IsSwallowed()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("poison-app-del-fail", provider, this.logger);
            var processor = new ApplicationExceptionMessageProcessor();
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("discard", true), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);
            provider.DeleteMessageException = new InvalidOperationException("delete failed");
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, 1, 100, 5);

            Assert.DoesNotThrowAsync(async () => await client.ProcessMessage(hooks[0], true));
        }

        [Test]
        public async Task PeekUnprocessed_SkipsEmptyAndBadJson()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("poison-peek", provider, this.logger);

            Assert.That(await provider.SendUnprocessedMessageAsync("not-json{"), Is.True);
            Assert.That(await provider.SendUnprocessedMessageAsync(string.Empty), Is.True);
            Assert.That(await provider.SendUnprocessedMessageAsync(new Message("ok", "act").ToString()), Is.True);

            // Empty body via override when hook is the empty string already stored; also skip bad JSON.
            IReadOnlyList<Message> peeked = await client.PeekUnprocessedMessagesAsync(10);
            Assert.That(peeked.Count, Is.EqualTo(1));
            Assert.That(ReadStringPayload(peeked[0]), Is.EqualTo("ok"));
        }

        [Test]
        public async Task RequeueUnprocessed_SendFailure_LeavesOnPoison()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("poison-rq-fail", provider, this.logger);
            ThrowingMessageProcessor processor = new ThrowingMessageProcessor(returnFalseInsteadOfThrow: true);
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), processor);

            Assert.That(await client.Enqueue("stay", true), Is.True);
            IList<object> hooks = provider.GetMessages(1, true);
            client.QueueDequeueConfig = new QueueDequeueConfig(1000, 5, handler, CancellationToken.None, 1, 100, 2);
            await client.ProcessMessage(hooks[0], true);
            Assert.That(await client.GetApproximateUnprocessedQueueLength(), Is.EqualTo(1));

            provider.SendMessageFails = true;
            int requeued = await client.RequeueUnprocessedMessagesAsync(10, true);
            Assert.That(requeued, Is.EqualTo(0));
            Assert.That(await client.GetApproximateUnprocessedQueueLength(), Is.EqualTo(1));
        }

        [Test]
        public async Task RequeueUnprocessed_SkipsEmptyAndBadJson()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("poison-rq-skip", provider, this.logger);

            Assert.That(await provider.SendUnprocessedMessageAsync(string.Empty), Is.True);
            Assert.That(await provider.SendUnprocessedMessageAsync("not-json{"), Is.True);

            int requeued = await client.RequeueUnprocessedMessagesAsync(10, true);
            Assert.That(requeued, Is.EqualTo(0));
            // Bad JSON is received then left (send never succeeds / catch path); empty skipped.
            Assert.That(await provider.Inner.GetNumUnprocessedMessages(), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task EnqueueList_PartialFailures_StillReturnsResults()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("enqueue-list-fail", provider, this.logger);

            provider.SendMessageFails = true;
            IList<string> messages = new List<string> { "a", "b" };
            IList<bool> results = await client.Enqueue<string>(messages, true);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0], Is.False);
            Assert.That(results[1], Is.False);
        }

        [Test]
        public async Task EnqueueList_ExceptionOnSend_CountsAsFailure()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("enqueue-list-ex", provider, this.logger);

            provider.SendMessageException = new InvalidOperationException("boom");
            IList<string> messages = new List<string> { "a" };
            IList<bool> results = await client.Enqueue<string>(messages, true);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0], Is.False);
        }

        [Test]
        public async Task DequeueAndProcess_WhenAlreadyRunning_Throws()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("dequeue-twice", provider, this.logger);
            DefaultProcessorHandler handler = new DefaultProcessorHandler();
            handler.AddProcessor(typeof(string), new TestMessageProcessor());
            using var cts = new CancellationTokenSource();
            var config = new QueueDequeueConfig(1000, 2, handler, cts.Token, 1, 100, 3);

            Task loop = client.DequeueAndProcess(config);
            await Task.Delay(30);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await client.DequeueAndProcess(config));
            cts.Cancel();
            try
            {
                await client.StopDequeuing();
            }
            catch (InvalidOperationException)
            {
                // Loop may have already cleared the polling flag after cancel.
            }

            await loop;
        }

        [Test]
        public void StopDequeuing_WhenNotRunning_Throws()
        {
            ControllableQueueProvider provider = new ControllableQueueProvider(this.logger);
            QueueClient client = new QueueClient("stop-idle", provider, this.logger);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await client.StopDequeuing());
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
