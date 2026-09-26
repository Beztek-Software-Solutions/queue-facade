// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;
    using System.Globalization;
    using System.IO;

    /// <summary>Pure Beanstalkd text-protocol helpers (unit-tested without a TCP socket).</summary>
    internal static class BeanstalkdProtocol
    {
        internal static void ExpectOk(string line)
        {
            if (line != null
                && (line.StartsWith("USING ", StringComparison.Ordinal) || line == "DELETED"))
            {
                return;
            }

            throw new IOException("Beanstalkd command failed: " + line);
        }

        internal static void ExpectWatching(string line)
        {
            if (line == null || !line.StartsWith("WATCHING ", StringComparison.Ordinal))
            {
                throw new IOException("Beanstalkd watch/ignore failed: " + line);
            }
        }

        internal static void ExpectReleased(string line)
        {
            if (line != "RELEASED")
            {
                throw new IOException("Beanstalkd release failed: " + line);
            }
        }

        internal static void ExpectBuried(string line)
        {
            if (line != "BURIED")
            {
                throw new IOException("Beanstalkd bury failed: " + line);
            }
        }

        /// <summary>
        /// Parses <c>RESERVED id bytes</c> / <c>FOUND id bytes</c>.
        /// Returns false for TIMED_OUT / DEADLINE_SOON / NOT_FOUND.
        /// </summary>
        internal static bool TryParseJobStatus(string line, string successPrefix, out ulong id, out int byteCount)
        {
            id = 0;
            byteCount = 0;
            if (string.IsNullOrEmpty(line))
            {
                throw new IOException("Beanstalkd closed connection");
            }

            if (IsSoftMiss(line))
            {
                return false;
            }

            if (!line.StartsWith(successPrefix, StringComparison.Ordinal))
            {
                throw new IOException("Beanstalkd job status failed: " + line);
            }

            string[] parts = line.Split(' ');
            if (parts.Length < 3
                || !ulong.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byteCount)
                || byteCount < 0)
            {
                throw new IOException("Beanstalkd job status malformed: " + line);
            }

            return true;
        }

        internal static bool TryParseOkByteCount(string line, out int byteCount)
        {
            byteCount = 0;
            if (string.IsNullOrEmpty(line) || !line.StartsWith("OK ", StringComparison.Ordinal))
            {
                return false;
            }

            if (!int.TryParse(line.AsSpan(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out byteCount)
                || byteCount < 0)
            {
                throw new IOException("Beanstalkd OK payload size malformed: " + line);
            }

            return true;
        }

        internal static int ParseYamlIntField(string yaml, string fieldName, int defaultValue)
        {
            if (string.IsNullOrEmpty(yaml))
            {
                return defaultValue;
            }

            string prefix = fieldName + ":";
            foreach (string raw in yaml.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (int.TryParse(line.AsSpan(prefix.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        internal static string ReadExactChars(TextReader reader, int bytes)
        {
            char[] buf = new char[bytes];
            int read = 0;
            while (read < bytes)
            {
                int n = reader.Read(buf, read, bytes - read);
                if (n <= 0)
                {
                    throw new IOException("Beanstalkd truncated payload");
                }

                read += n;
            }

            reader.ReadLine(); // trailing CRLF after payload
            return new string(buf);
        }

        private static bool IsSoftMiss(string line) =>
            line.StartsWith("TIMED_OUT", StringComparison.Ordinal)
            || line.StartsWith("DEADLINE_SOON", StringComparison.Ordinal)
            || line.StartsWith("NOT_FOUND", StringComparison.Ordinal);
    }
}
