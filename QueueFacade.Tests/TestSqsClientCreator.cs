// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using Amazon.SQS;
    using Queue.Providers;

    internal class TestSqsClientCreator : SqsClientCreator
    {
        private readonly IAmazonSQS client;

        internal TestSqsClientCreator(IAmazonSQS client)
        {
            this.client = client;
        }

        internal override IAmazonSQS CreateClient(SqsQueueProviderConfig config)
        {
            return client;
        }
    }
}
