// Copyright (c) Beztek Software Solutions. All rights reserved.


namespace Beztek.Facade.Queue
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Base implementation of IMessageProcessor
    /// </summary>
    public abstract class AbstractMessageProcessor : IMessageProcessor
    {
        public abstract Task<bool> Process(Message message);

        public virtual async Task<List<bool>> Process(List<Message> messageList)
        {
            List<bool> results = new List<bool>();
            foreach (Message message in messageList)
            {
                try
                {
                    results.Add(await Process(message).ConfigureAwait(false));
                }
                catch (Exception)
                {
                    results.Add(false);
                }
            }

            return results;
        }
    }
}
