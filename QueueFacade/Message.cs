// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Envelope for enqueueing and dequeuing typed messages.
    /// </summary>
    public class Message
    {
        /// <summary>Creates an empty message (used for deserialization).</summary>
        public Message()
        { }

        internal Message(object message, string activityId = null)
        {
            this.MessageType = message.GetType().ToString();
            this.RawMessage = message;
            this.ActivityId = activityId;
        }

        /// <summary>
        /// Serialized type information of <see cref="RawMessage"/>.
        /// </summary>
        public string MessageType { get; set; }

        /// <summary>
        /// Message body (serialized payload).
        /// </summary>
        public object RawMessage { get; set; }

        /// <summary>
        /// Delivery attempt count for this message (1 on first receive). Providers expose native
        /// dequeue/receive counts; LocalMemory tracks attempts across visibility timeouts.
        /// </summary>
        public int ProcessingAttempt { get; set; } = 1;

        /// <summary>
        /// Activity ID for distributed tracing.
        /// </summary>
        public string ActivityId { get; set; }

        /// <summary>
        /// Gets the message object of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type of message expected.</typeparam>
        /// <returns>Message of type <typeparamref name="T"/>.</returns>
        public T GetMessageObject<T>() => JsonSerializer.Deserialize<T>(Convert.ToString(this.RawMessage));

        /// <inheritdoc />
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
