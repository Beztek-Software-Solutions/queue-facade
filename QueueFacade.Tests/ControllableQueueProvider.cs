// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Queue.Providers;

    /// <summary>
    /// Wraps <see cref="LocalMemoryQueueProvider"/> so unit tests can inject send/delete/body failures
    /// without mocking every <see cref="IQueueProvider"/> member.
    /// </summary>
    internal sealed class ControllableQueueProvider : IQueueProvider
    {
        private readonly LocalMemoryQueueProvider inner;

        internal ControllableQueueProvider(ILogger logger = null, bool implementUnhide = false, int visibilityTimeoutMilliseconds = 50)
        {
            this.inner = new LocalMemoryQueueProvider(logger, implementUnhide, visibilityTimeoutMilliseconds);
        }

        internal LocalMemoryQueueProvider Inner => this.inner;

        internal bool SendMessageFails { get; set; }

        internal bool SendUnprocessedFails { get; set; }

        internal Exception DeleteMessageException { get; set; }

        internal Exception SendUnprocessedException { get; set; }

        internal Exception SendMessageException { get; set; }

        internal Func<object, string> GetMessageBodyOverride { get; set; }

        public int MaxMessageCountPerPoll
        {
            get => this.inner.MaxMessageCountPerPoll;
            set => this.inner.MaxMessageCountPerPoll = value;
        }

        public int MaxMessageSize
        {
            get => this.inner.MaxMessageSize;
            set => this.inner.MaxMessageSize = value;
        }

        public bool HasLowPriorityQueue
        {
            get => this.inner.HasLowPriorityQueue;
            set => this.inner.HasLowPriorityQueue = value;
        }

        public void CreateIfNotExists() => this.inner.CreateIfNotExists();

        public string GetMessageBody(object messageHook)
        {
            if (this.GetMessageBodyOverride != null)
            {
                return this.GetMessageBodyOverride(messageHook);
            }

            return this.inner.GetMessageBody(messageHook);
        }

        public async Task DeleteMessageAsync(object messageHook, bool isHighPriorityQueue)
        {
            if (this.DeleteMessageException != null)
            {
                throw this.DeleteMessageException;
            }

            await this.inner.DeleteMessageAsync(messageHook, isHighPriorityQueue).ConfigureAwait(false);
        }

        public async Task<bool> SendMessageAsync(string message, bool useHighPriorityQueue)
        {
            if (this.SendMessageException != null)
            {
                throw this.SendMessageException;
            }

            if (this.SendMessageFails)
            {
                return false;
            }

            return await this.inner.SendMessageAsync(message, useHighPriorityQueue).ConfigureAwait(false);
        }

        public async Task<bool> SendUnprocessedMessageAsync(string message)
        {
            if (this.SendUnprocessedException != null)
            {
                throw this.SendUnprocessedException;
            }

            if (this.SendUnprocessedFails)
            {
                return false;
            }

            return await this.inner.SendUnprocessedMessageAsync(message).ConfigureAwait(false);
        }

        public IList<object> GetMessages(int maxMessagesToRetrieve, bool isHighPriorityQueue) =>
            this.inner.GetMessages(maxMessagesToRetrieve, isHighPriorityQueue);

        public int GetReceiveCount(object messageHook) => this.inner.GetReceiveCount(messageHook);

        public IList<object> PeekUnprocessedMessages(int maxMessagesToRetrieve) =>
            this.inner.PeekUnprocessedMessages(maxMessagesToRetrieve);

        public IList<object> ReceiveUnprocessedMessages(int maxMessagesToRetrieve) =>
            this.inner.ReceiveUnprocessedMessages(maxMessagesToRetrieve);

        public Task DeleteUnprocessedMessageAsync(object messageHook) =>
            this.inner.DeleteUnprocessedMessageAsync(messageHook);

        public Task<long> GetApproximateUnprocessedQueueLength() =>
            this.inner.GetApproximateUnprocessedQueueLength();

        public Task<long> GetApproximateQueueLength(bool isHighPriorityQueue) =>
            this.inner.GetApproximateQueueLength(isHighPriorityQueue);
    }
}
