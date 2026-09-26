// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.IO;
    using Beztek.Facade.Queue.Providers;
    using NUnit.Framework;

    [TestFixture]
    public class BeanstalkdProtocolTests
    {
        [Test]
        public void ExpectOk_AcceptsUsingAndDeleted()
        {
            Assert.DoesNotThrow(() => BeanstalkdProtocol.ExpectOk("USING default"));
            Assert.DoesNotThrow(() => BeanstalkdProtocol.ExpectOk("DELETED"));
        }

        [Test]
        public void ExpectOk_RejectsUnexpected()
        {
            Assert.Throws<IOException>(() => BeanstalkdProtocol.ExpectOk("NOT_FOUND"));
            Assert.Throws<IOException>(() => BeanstalkdProtocol.ExpectOk(null));
        }

        [Test]
        public void ExpectWatchingReleasedBuried_Validate()
        {
            Assert.DoesNotThrow(() => BeanstalkdProtocol.ExpectWatching("WATCHING 1"));
            Assert.Throws<IOException>(() => BeanstalkdProtocol.ExpectWatching("BAD"));
            Assert.DoesNotThrow(() => BeanstalkdProtocol.ExpectReleased("RELEASED"));
            Assert.Throws<IOException>(() => BeanstalkdProtocol.ExpectReleased("BAD"));
            Assert.DoesNotThrow(() => BeanstalkdProtocol.ExpectBuried("BURIED"));
            Assert.Throws<IOException>(() => BeanstalkdProtocol.ExpectBuried("BAD"));
        }

        [Test]
        public void TryParseJobStatus_SoftMissAndSuccess()
        {
            Assert.That(BeanstalkdProtocol.TryParseJobStatus("TIMED_OUT", "RESERVED ", out _, out _), Is.False);
            Assert.That(BeanstalkdProtocol.TryParseJobStatus("DEADLINE_SOON", "RESERVED ", out _, out _), Is.False);
            Assert.That(BeanstalkdProtocol.TryParseJobStatus("NOT_FOUND", "FOUND ", out _, out _), Is.False);

            Assert.That(BeanstalkdProtocol.TryParseJobStatus("RESERVED 42 5", "RESERVED ", out ulong id, out int bytes), Is.True);
            Assert.That(id, Is.EqualTo(42UL));
            Assert.That(bytes, Is.EqualTo(5));

            Assert.Throws<IOException>(() => BeanstalkdProtocol.TryParseJobStatus("ERROR", "RESERVED ", out _, out _));
            Assert.Throws<IOException>(() => BeanstalkdProtocol.TryParseJobStatus("RESERVED x y", "RESERVED ", out _, out _));
            Assert.Throws<IOException>(() => BeanstalkdProtocol.TryParseJobStatus(null, "RESERVED ", out _, out _));
        }

        [Test]
        public void TryParseOkByteCount_AndYamlField()
        {
            Assert.That(BeanstalkdProtocol.TryParseOkByteCount("OK 12", out int n), Is.True);
            Assert.That(n, Is.EqualTo(12));
            Assert.That(BeanstalkdProtocol.TryParseOkByteCount("NOT_FOUND", out _), Is.False);
            Assert.Throws<IOException>(() => BeanstalkdProtocol.TryParseOkByteCount("OK xyz", out _));

            string yaml = "---\ncurrent-jobs-ready: 3\nreserves: 7\n";
            Assert.That(BeanstalkdProtocol.ParseYamlIntField(yaml, "current-jobs-ready", 0), Is.EqualTo(3));
            Assert.That(BeanstalkdProtocol.ParseYamlIntField(yaml, "reserves", 1), Is.EqualTo(7));
            Assert.That(BeanstalkdProtocol.ParseYamlIntField(null, "reserves", 1), Is.EqualTo(1));
            Assert.That(BeanstalkdProtocol.ParseYamlIntField("nope", "reserves", 9), Is.EqualTo(9));
        }

        [Test]
        public void ReadExactChars_ReadsPayloadAndTrailingLine()
        {
            using var reader = new StringReader("hello\r\n");
            Assert.That(BeanstalkdProtocol.ReadExactChars(reader, 5), Is.EqualTo("hello"));
        }

        [Test]
        public void ReadExactChars_Truncated_Throws()
        {
            using var reader = new StringReader("hi");
            Assert.Throws<IOException>(() => BeanstalkdProtocol.ReadExactChars(reader, 5));
        }
    }
}
