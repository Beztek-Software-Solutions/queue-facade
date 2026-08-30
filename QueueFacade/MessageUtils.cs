// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    /// <summary>Helpers for estimating serialized queue message size.</summary>
    public static class MessageUtils
    {
        /// <summary>
        /// Returns the serialized length of a <see cref="Message"/> wrapping <paramref name="messageObj"/>.
        /// </summary>
        /// <typeparam name="T">Payload type.</typeparam>
        /// <param name="messageObj">Message body.</param>
        /// <param name="activityId">Optional activity id for distributed tracing.</param>
        public static int GetMessageSize<T>(T messageObj, string activityId = null)
        {
            Message message = new Message(messageObj, activityId);

            string messageStr = message.ToString();
            int messageLength = messageStr.Length;

            return messageLength;
        }
    }
}
