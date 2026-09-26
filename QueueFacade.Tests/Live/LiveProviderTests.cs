// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;

    /// <summary>
    /// Cross-provider live suite. Discovered only when <c>QUEUEFACADE_LIVE_PROVIDERS</c> is set.
    /// </summary>
    [TestFixtureSource(typeof(LiveProviderFixtureSource), nameof(LiveProviderFixtureSource.Providers))]
    [Category("Live")]
    public class LiveProviderTests
    {
        private readonly QueueProviderType _providerType;
        private LiveProviderHost _host;

        public LiveProviderTests(QueueProviderType providerType)
        {
            _providerType = providerType;
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            try
            {
                _host = await LiveProviderHost.StartAsync(_providerType).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Inconclusive(ex.Message);
            }
            catch (Exception ex) when (IsImageOrStartupFailure(ex))
            {
                Assert.Inconclusive($"Skipping {_providerType}: container image/startup failed — {ex.Message}");
            }
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            if (_host != null)
                await _host.DisposeAsync().ConfigureAwait(false);
        }

        [SetUp]
        public void SetUp()
        {
            Assume.That(_host, Is.Not.Null);
        }

        [Test]
        public async Task Enqueue_ThenDequeueAndProcess_ConsumesMessage()
        {
            IQueueClient client = _host.CreateIsolatedClient();
            var processor = new TestMessageProcessor();
            string payload = "live-" + Guid.NewGuid().ToString("N");

            Assert.That(await client.Enqueue(payload, useHighPriorityQueue: true).ConfigureAwait(false), Is.True);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _ = client.DequeueAndProcess(
                maxMessageRate: 10,
                maxAsynchronousProcesses: 2,
                processor: processor,
                cancellationToken: cts.Token,
                batchSize: 1,
                pollIntervalInMilliseconds: 100,
                maxProcessingAttempts: 3);

            DateTime deadline = DateTime.UtcNow.AddSeconds(20);
            while (processor.GetProcessCount() < 1 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }

            await client.StopDequeuing().ConfigureAwait(false);
            Assert.That(processor.GetProcessCount(), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task GetApproximateQueueLength_AfterEnqueue_IsPositive()
        {
            // Emulator admin runtime properties always report ActiveMessageCount=0.
            if (_providerType == QueueProviderType.AzureServiceBus)
                Assert.Ignore("Service Bus emulator does not expose ActiveMessageCount.");

            IQueueClient client = _host.CreateIsolatedClient();
            Assert.That(await client.Enqueue("len-" + Guid.NewGuid().ToString("N"), true).ConfigureAwait(false), Is.True);

            // Allow create/send to settle on remote backends.
            long depth = 0;
            for (int i = 0; i < 20; i++)
            {
                depth = await client.GetApproximateQueueLength(isHighPriorityQueue: true).ConfigureAwait(false);
                if (depth > 0)
                    break;
                await Task.Delay(100).ConfigureAwait(false);
            }

            Assert.That(depth, Is.GreaterThan(0));
        }

        [Test]
        public async Task FalseResult_MovesToPoison_ThenRequeue_AndProcess()
        {
            IQueueClient client = _host.CreateIsolatedClient();
            string payload = "poison-" + Guid.NewGuid().ToString("N");

            Assert.That(await client.Enqueue(payload, useHighPriorityQueue: true).ConfigureAwait(false), Is.True);

            var failing = new ThrowingMessageProcessor(returnFalseInsteadOfThrow: true);
            using (var ctsFail = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                _ = client.DequeueAndProcess(
                    maxMessageRate: 10,
                    maxAsynchronousProcesses: 2,
                    processor: failing,
                    cancellationToken: ctsFail.Token,
                    batchSize: 1,
                    pollIntervalInMilliseconds: 100,
                    maxProcessingAttempts: 3);

                DateTime poisonDeadline = DateTime.UtcNow.AddSeconds(25);
                bool poisonVisible = await WaitUntilUnprocessedVisibleAsync(client, poisonDeadline)
                    .ConfigureAwait(false);

                await client.StopDequeuing().ConfigureAwait(false);
                Assert.That(poisonVisible, Is.True, "expected message on poison queue after false result");
            }

            // Do not Peek before requeue: SQS has no true peek (receive + visibility would hide the message).
            int requeued = await client.RequeueUnprocessedMessagesAsync(maxMessages: 10, useHighPriorityQueue: true)
                .ConfigureAwait(false);
            Assert.That(requeued, Is.GreaterThan(0));

            var ok = new TestMessageProcessor();
            using var ctsOk = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _ = client.DequeueAndProcess(
                maxMessageRate: 10,
                maxAsynchronousProcesses: 2,
                processor: ok,
                cancellationToken: ctsOk.Token,
                batchSize: 1,
                pollIntervalInMilliseconds: 100,
                maxProcessingAttempts: 3);

            DateTime okDeadline = DateTime.UtcNow.AddSeconds(25);
            while (ok.GetProcessCount() < 1 && DateTime.UtcNow < okDeadline)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }

            await client.StopDequeuing().ConfigureAwait(false);
            Assert.That(ok.GetProcessCount(), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task PeekUnprocessed_AfterPoison_ReturnsPayload()
        {
            IQueueClient client = _host.CreateIsolatedClient();
            string payload = "peek-" + Guid.NewGuid().ToString("N");
            Assert.That(await client.Enqueue(payload, true).ConfigureAwait(false), Is.True);

            var failing = new ThrowingMessageProcessor(returnFalseInsteadOfThrow: true);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _ = client.DequeueAndProcess(
                maxMessageRate: 10,
                maxAsynchronousProcesses: 2,
                processor: failing,
                cancellationToken: cts.Token,
                batchSize: 1,
                pollIntervalInMilliseconds: 100,
                maxProcessingAttempts: 3);

            DateTime deadline = DateTime.UtcNow.AddSeconds(25);
            bool poisonVisible = await WaitUntilUnprocessedVisibleAsync(client, deadline).ConfigureAwait(false);
            Assume.That(poisonVisible, Is.True, "poison message never became visible");

            await client.StopDequeuing().ConfigureAwait(false);

            IReadOnlyList<Message> peeked = await client.PeekUnprocessedMessagesAsync(10).ConfigureAwait(false);
            Assert.That(peeked.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task Exception_AtMaxAttemptsOne_MovesToPoison()
        {
            IQueueClient client = _host.CreateIsolatedClient();
            string payload = "maxattempt-" + Guid.NewGuid().ToString("N");
            Assert.That(await client.Enqueue(payload, true).ConfigureAwait(false), Is.True);

            var throwing = new ThrowingMessageProcessor();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _ = client.DequeueAndProcess(
                maxMessageRate: 10,
                maxAsynchronousProcesses: 2,
                processor: throwing,
                cancellationToken: cts.Token,
                batchSize: 1,
                pollIntervalInMilliseconds: 100,
                maxProcessingAttempts: 1);

            DateTime deadline = DateTime.UtcNow.AddSeconds(25);
            bool poisonVisible = await WaitUntilUnprocessedVisibleAsync(client, deadline).ConfigureAwait(false);

            await client.StopDequeuing().ConfigureAwait(false);
            Assert.That(poisonVisible, Is.True);
            Assert.That(throwing.ProcessCount, Is.GreaterThanOrEqualTo(1));
        }

        /// <summary>
        /// Waits until poison messages are visible. Service Bus emulator reports
        /// <c>ActiveMessageCount=0</c>, so fall back to peek when depth stays zero.
        /// </summary>
        private static async Task<bool> WaitUntilUnprocessedVisibleAsync(
            IQueueClient client, DateTime deadline)
        {
            while (DateTime.UtcNow < deadline)
            {
                if (await client.GetApproximateUnprocessedQueueLength().ConfigureAwait(false) > 0)
                    return true;

                IReadOnlyList<Message> peeked =
                    await client.PeekUnprocessedMessagesAsync(1).ConfigureAwait(false);
                if (peeked.Count > 0)
                    return true;

                await Task.Delay(150).ConfigureAwait(false);
            }

            return false;
        }

        private static bool IsImageOrStartupFailure(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                string msg = e.Message ?? string.Empty;
                if (msg.Contains("manifest unknown", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("pull access denied", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("No such image", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Unable to find image", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("HTTP request to", StringComparison.OrdinalIgnoreCase)
                    || e is TimeoutException)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
