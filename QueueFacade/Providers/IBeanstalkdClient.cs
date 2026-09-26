// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using System;

    /// <summary>Beanstalkd TCP client surface (mockable in unit tests).</summary>
    internal interface IBeanstalkdClient : IDisposable
    {
        void Use(string tube);

        void Watch(string tube);

        void Ignore(string tube);

        ulong Put(int priority, int delaySeconds, int ttrSeconds, string body);

        BeanstalkdJob Reserve(int timeoutSeconds);

        void Delete(ulong id);

        void Release(ulong id, int priority, int delaySeconds);

        void Bury(ulong id, int priority);

        bool KickJob(ulong id);

        BeanstalkdJob PeekBuried();

        int StatsTubeCurrentJobsReady(string tube);

        int StatsJobReserves(ulong id);
    }
}
