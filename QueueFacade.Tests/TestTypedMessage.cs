// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;

    [Serializable]
    public class TestTypedMessage
    {
        public TestTypedMessage()
        { }

        public TestTypedMessage(string id, string value, DateTime createdDate, DateTime updatedDate, string etag)
        {
            this.Id = id;
            this.Value = value;
            this.CreatedDate = createdDate;
            this.UpdatedDate = updatedDate;
            this.Etag = etag;
        }

        public string Id { get; set; }

        public string Value { get; set; }

        public string Etag { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime UpdatedDate { get; set; }

        public override bool Equals(object obj)
        {
            TestTypedMessage other = obj as TestTypedMessage;
            if (other != null)
            {
                return string.Equals(this.Id, other.Id, StringComparison.Ordinal)
                    && string.Equals(this.Value.ToString(), other.Value.ToString(), StringComparison.Ordinal)
                    && object.Equals(this.CreatedDate, other.CreatedDate)
                    && object.Equals(this.UpdatedDate, other.UpdatedDate)
                    && string.Equals(this.Etag, other.Etag, StringComparison.Ordinal);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }
    }
}
