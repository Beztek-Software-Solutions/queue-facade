// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    public static class MessageUtils
    {
        public static int GetMessageSize<T>(T messageObj, string activityId = null)
        {
            Message message = new Message(messageObj, activityId);

            string messageStr = message.ToString();
            int messageLength = messageStr.Length;

            return messageLength;
        }
    }
}
