// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests.Live
{
    using System.Collections;
    using NUnit.Framework;

    /// <summary>
    /// Feeds <see cref="LiveProviderTests"/> one fixture per selected provider.
    /// When <c>QUEUEFACADE_LIVE_PROVIDERS</c> is unset, yields nothing.
    /// </summary>
    public static class LiveProviderFixtureSource
    {
        public static IEnumerable Providers()
        {
            foreach (QueueProviderType provider in LiveProviderSelection.Resolve())
            {
                yield return new TestFixtureData(provider)
                    .SetArgDisplayNames(provider.ToString());
            }
        }
    }
}
