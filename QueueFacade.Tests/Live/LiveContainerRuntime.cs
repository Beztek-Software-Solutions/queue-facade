// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests.Live
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Points Testcontainers at a container engine API. Prefers Podman (rootless socket)
    /// when present; otherwise leaves Docker defaults alone.
    /// </summary>
    public static class LiveContainerRuntime
    {
        private static readonly object Gate = new object();
        private static bool _configured;

        public static void EnsureConfigured()
        {
            lock (Gate)
            {
                if (_configured)
                    return;
                _configured = true;

                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED")))
                    Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");

                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
                    return;

                string socket = FindPodmanSocket();
                if (socket == null)
                    return;

                Environment.SetEnvironmentVariable("DOCKER_HOST", "unix://" + socket);
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE")))
                    Environment.SetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE", socket);
            }
        }

        public static string FindPodmanSocket()
        {
            string xdg = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                string candidate = Path.Combine(xdg, "podman", "podman.sock");
                if (File.Exists(candidate))
                    return candidate;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                uint uid = GetUnixUserId();
                string candidate = $"/run/user/{uid}/podman/podman.sock";
                if (File.Exists(candidate))
                    return candidate;

                const string systemSock = "/run/podman/podman.sock";
                if (File.Exists(systemSock))
                    return systemSock;
            }

            return null;
        }

        private static uint GetUnixUserId()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return 0;
            try
            {
                return getuid();
            }
            catch
            {
                return 0;
            }
        }

        [DllImport("libc", SetLastError = false)]
        private static extern uint getuid();
    }
}
