// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Class to enable enqueueing and dequeing messages
    /// </summary>
    public class Message
    {
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
        /// Message.
        /// </summary>
        public object RawMessage { get; set; }

        /// <summary>
        /// Activity ID for distributed tracing.
        /// </summary>
        public string ActivityId { get; set; }

        /// <summary>
        /// Gets Message object of type <see cref="T"/>
        /// </summary>
        /// <typeparam name="T">Type of Message expected.</typeparam>
        /// <returns>Message of type <see cref="T"/></returns>
        public T GetMessageObject<T>() => JsonSerializer.Deserialize<T>(Convert.ToString(this.RawMessage));

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
