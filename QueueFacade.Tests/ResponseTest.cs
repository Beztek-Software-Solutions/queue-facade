// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using Azure;

    class ResponseTest<T> : Response<T>
    {
        public override T Value { get; }

        public ResponseTest(T value)
        {
            this.Value = value;
        }

        public override Response GetRawResponse()
        {
            throw new NotImplementedException();
        }
    }
}
