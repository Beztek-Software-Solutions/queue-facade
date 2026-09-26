// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Beztek.Facade.Queue.Providers;
    using NUnit.Framework;

    /// <summary>In-memory duplex stream that serves scripted Beanstalkd responses.</summary>
    internal sealed class ScriptedDuplexStream : Stream
    {
        private readonly Queue<byte> readQueue = new Queue<byte>();
        private readonly MemoryStream written = new MemoryStream();
        private readonly object gate = new object();

        public void EnqueueServerLine(string lineWithoutCrLf)
        {
            EnqueueServerBytes(Encoding.UTF8.GetBytes(lineWithoutCrLf + "\r\n"));
        }

        public void EnqueueServerBytes(byte[] bytes)
        {
            lock (gate)
            {
                foreach (byte b in bytes)
                {
                    readQueue.Enqueue(b);
                }
            }
        }

        public string WrittenAscii
        {
            get
            {
                lock (gate)
                {
                    return Encoding.ASCII.GetString(written.ToArray());
                }
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (gate)
            {
                int n = 0;
                while (n < count && readQueue.Count > 0)
                {
                    buffer[offset + n++] = readQueue.Dequeue();
                }

                return n;
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (gate)
            {
                written.Write(buffer, offset, count);
            }
        }
    }

    [TestFixture]
    public class BeanstalkdClientTests
    {
        private ScriptedDuplexStream stream;
        private BeanstalkdClient client;

        [SetUp]
        public void SetUp()
        {
            stream = new ScriptedDuplexStream();
            client = new BeanstalkdClient(stream, ownsStream: true);
        }

        [TearDown]
        public void TearDown()
        {
            client.Dispose();
        }

        [Test]
        public void Use_Watch_Ignore_RoundTrip()
        {
            stream.EnqueueServerLine("USING tube-a");
            client.Use("tube-a");

            stream.EnqueueServerLine("WATCHING 2");
            client.Watch("tube-a");

            stream.EnqueueServerLine("WATCHING 1");
            client.Ignore("default");

            Assert.That(stream.WrittenAscii, Does.Contain("use tube-a"));
            Assert.That(stream.WrittenAscii, Does.Contain("watch tube-a"));
            Assert.That(stream.WrittenAscii, Does.Contain("ignore default"));
        }

        [Test]
        public void Put_ReturnsInsertedId()
        {
            stream.EnqueueServerLine("INSERTED 42");
            ulong id = client.Put(1024, 0, 30, "hi");
            Assert.That(id, Is.EqualTo(42UL));
            Assert.That(stream.WrittenAscii, Does.Contain("put 1024 0 30 2"));
            Assert.That(stream.WrittenAscii, Does.Contain("hi"));
        }

        [Test]
        public void Put_OnBadReply_Throws()
        {
            stream.EnqueueServerLine("OUT_OF_MEMORY");
            Assert.Throws<IOException>(() => client.Put(1, 0, 1, "x"));
        }

        [Test]
        public void Reserve_ReturnsJob_OrNullOnTimeout()
        {
            stream.EnqueueServerBytes(Encoding.UTF8.GetBytes("RESERVED 9 4\r\nbody\r\n"));
            BeanstalkdJob job = client.Reserve(0);
            Assert.That(job, Is.Not.Null);
            Assert.That(job.Id, Is.EqualTo(9UL));
            Assert.That(job.Body, Is.EqualTo("body"));

            stream.EnqueueServerLine("TIMED_OUT");
            Assert.That(client.Reserve(0), Is.Null);
        }

        [Test]
        public void Delete_Release_Bury_Kick_PeekBuried()
        {
            stream.EnqueueServerLine("DELETED");
            client.Delete(1);

            stream.EnqueueServerLine("RELEASED");
            client.Release(1, 1024, 0);

            stream.EnqueueServerLine("BURIED");
            client.Bury(1, 1024);

            stream.EnqueueServerLine("KICKED");
            Assert.That(client.KickJob(1), Is.True);

            stream.EnqueueServerLine("NOT_FOUND");
            Assert.That(client.KickJob(2), Is.False);

            stream.EnqueueServerBytes(Encoding.UTF8.GetBytes("FOUND 3 1\r\nz\r\n"));
            BeanstalkdJob buried = client.PeekBuried();
            Assert.That(buried.Id, Is.EqualTo(3UL));
            Assert.That(buried.Body, Is.EqualTo("z"));

            stream.EnqueueServerLine("NOT_FOUND");
            Assert.That(client.PeekBuried(), Is.Null);
        }

        [Test]
        public void StatsTube_AndStatsJob()
        {
            string yaml = "---\ncurrent-jobs-ready: 7\n";
            stream.EnqueueServerBytes(Encoding.UTF8.GetBytes($"OK {Encoding.UTF8.GetByteCount(yaml)}\r\n{yaml}\r\n"));
            Assert.That(client.StatsTubeCurrentJobsReady("t"), Is.EqualTo(7));

            string jobYaml = "---\nreserves: 4\n";
            stream.EnqueueServerBytes(Encoding.UTF8.GetBytes($"OK {Encoding.UTF8.GetByteCount(jobYaml)}\r\n{jobYaml}\r\n"));
            Assert.That(client.StatsJobReserves(9), Is.EqualTo(4));

            stream.EnqueueServerLine("NOT_FOUND");
            Assert.That(client.StatsJobReserves(99), Is.EqualTo(1));
        }

        [Test]
        public void Use_OnBadReply_Throws()
        {
            stream.EnqueueServerLine("BAD_FORMAT");
            Assert.Throws<IOException>(() => client.Use("x"));
        }
    }
}
