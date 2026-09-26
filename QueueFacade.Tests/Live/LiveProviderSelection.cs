// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Selects which providers run live tests via <c>QUEUEFACADE_LIVE_PROVIDERS</c>.
    /// Unset / empty → no live fixtures (unit CI stays container-free).
    /// </summary>
    public static class LiveProviderSelection
    {
        public const string EnvVar = "QUEUEFACADE_LIVE_PROVIDERS";

        private static readonly QueueProviderType[] AllProviders =
        {
            QueueProviderType.LocalMemory,
            QueueProviderType.AzureStorage,
            QueueProviderType.AwsSqs,
            QueueProviderType.AzureServiceBus,
            QueueProviderType.RabbitMq,
            QueueProviderType.GooglePubSub,
            QueueProviderType.Redis,
            QueueProviderType.Valkey,
            QueueProviderType.Dragonfly,
            QueueProviderType.ActiveMq,
            QueueProviderType.Beanstalkd,
        };

        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar));

        public static IReadOnlyList<QueueProviderType> Resolve()
        {
            string raw = Environment.GetEnvironmentVariable(EnvVar);
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<QueueProviderType>();

            var selected = new List<QueueProviderType>();
            foreach (string token in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = token.Trim().ToLowerInvariant();
                if (t is "all" or "*")
                    return AllProviders.ToList();

                if (TryParse(t, out QueueProviderType provider) && !selected.Contains(provider))
                    selected.Add(provider);
                else
                    throw new ArgumentException(
                        $"Unknown provider '{token}' in {EnvVar}. " +
                        "Use: all | localmemory | azure | sqs | servicebus | rabbitmq | pubsub | redis | valkey | dragonfly | activemq | beanstalkd");
            }

            return selected;
        }

        public static bool TryParse(string token, out QueueProviderType provider)
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "localmemory":
                case "local":
                case "memory":
                    provider = QueueProviderType.LocalMemory;
                    return true;
                case "azure":
                case "azurite":
                case "azurestorage":
                    provider = QueueProviderType.AzureStorage;
                    return true;
                case "sqs":
                case "aws":
                case "awssqs":
                case "localstack":
                    provider = QueueProviderType.AwsSqs;
                    return true;
                case "servicebus":
                case "asb":
                case "azureservicebus":
                    provider = QueueProviderType.AzureServiceBus;
                    return true;
                case "rabbitmq":
                case "rabbit":
                case "amqp":
                    provider = QueueProviderType.RabbitMq;
                    return true;
                case "pubsub":
                case "gcp":
                case "google":
                case "googlepubsub":
                    provider = QueueProviderType.GooglePubSub;
                    return true;
                case "redis":
                    provider = QueueProviderType.Redis;
                    return true;
                case "valkey":
                    provider = QueueProviderType.Valkey;
                    return true;
                case "dragonfly":
                case "df":
                    provider = QueueProviderType.Dragonfly;
                    return true;
                case "activemq":
                case "amq":
                case "amazonmq":
                    provider = QueueProviderType.ActiveMq;
                    return true;
                case "beanstalkd":
                case "beanstalk":
                    provider = QueueProviderType.Beanstalkd;
                    return true;
                default:
                    provider = default;
                    return false;
            }
        }
    }
}
