// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue
{
    /// <summary>
    /// Queue Facade Constants
    /// </summary>
    internal static class Constants
    {
        public const string InvalidResourceName = "Invalid queue name. {0}";
        public const string InvalidResourceNameLength = "Invalid queue name length. The queue name must be between {0} and {1} characters long.";
        public const string InvalidResourceReservedName = "Invalid {0} name. This {0} name is reserved.";
        public const string ResourceNameEmpty = "Invalid queue name. The queue name may not be null, empty, or whitespace only.";
        public const string UnprocessedMessageQueue = "unprocessedmessagequeue";
    }
}
