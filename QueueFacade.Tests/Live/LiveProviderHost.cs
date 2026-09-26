// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests.Live
{
    using System;
    using System.Threading.Tasks;
    using DotNet.Testcontainers.Builders;
    using DotNet.Testcontainers.Containers;
    using DotNet.Testcontainers.Networks;
    using Testcontainers.ServiceBus;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Starts (or skips) a throwaway queue backend for live tests.
    /// Use <see cref="CreateIsolatedClient"/> so each test gets its own queues on the shared backend.
    /// </summary>
    public sealed class LiveProviderHost : IAsyncDisposable
    {
        private const string AzuriteAccountKey =
            "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

        private readonly IContainer _container;
        private readonly IContainer _sidecar;
        private readonly INetwork _network;
        private readonly string _azureEndpoint;
        private readonly string _sqsServiceUrl;
        private readonly string _serviceBusConnection;
        private readonly string _serviceBusAdminConnection;
        private readonly string _amqpUri;
        private readonly string _pubsubEmulatorHost;
        private readonly string _redisConfig;
        private readonly string _activeMqBrokerUri;
        private readonly string _beanstalkHost;
        private readonly int _beanstalkPort;

        private LiveProviderHost(
            QueueProviderType providerType,
            IQueueClient client,
            IContainer container,
            IContainer sidecar = null,
            INetwork network = null,
            string azureEndpoint = null,
            string sqsServiceUrl = null,
            string serviceBusConnection = null,
            string serviceBusAdminConnection = null,
            string amqpUri = null,
            string pubsubEmulatorHost = null,
            string redisConfig = null,
            string activeMqBrokerUri = null,
            string beanstalkHost = null,
            int beanstalkPort = 0)
        {
            ProviderType = providerType;
            Client = client;
            _container = container;
            _sidecar = sidecar;
            _network = network;
            _azureEndpoint = azureEndpoint;
            _sqsServiceUrl = sqsServiceUrl;
            _serviceBusConnection = serviceBusConnection;
            _serviceBusAdminConnection = serviceBusAdminConnection;
            _amqpUri = amqpUri;
            _pubsubEmulatorHost = pubsubEmulatorHost;
            _redisConfig = redisConfig;
            _activeMqBrokerUri = activeMqBrokerUri;
            _beanstalkHost = beanstalkHost;
            _beanstalkPort = beanstalkPort;
        }

        public QueueProviderType ProviderType { get; }

        public IQueueClient Client { get; }

        public IQueueClient CreateIsolatedClient()
        {
            return ProviderType switch
            {
                QueueProviderType.LocalMemory => CreateLocalMemoryClient(),
                QueueProviderType.AzureStorage => CreateAzureStorageClient(_azureEndpoint),
                QueueProviderType.AwsSqs => CreateSqsClient(_sqsServiceUrl),
                QueueProviderType.AzureServiceBus => CreateServiceBusClient(_serviceBusConnection, _serviceBusAdminConnection),
                QueueProviderType.RabbitMq => CreateRabbitClient(_amqpUri),
                QueueProviderType.GooglePubSub => CreatePubSubClient(_pubsubEmulatorHost),
                QueueProviderType.Redis => CreateRedisClient(_redisConfig),
                QueueProviderType.Valkey => CreateValkeyClient(_redisConfig),
                QueueProviderType.Dragonfly => CreateDragonflyClient(_redisConfig),
                QueueProviderType.ActiveMq => CreateActiveMqClient(_activeMqBrokerUri),
                QueueProviderType.Beanstalkd => CreateBeanstalkClient(_beanstalkHost, _beanstalkPort),
                _ => throw new ArgumentOutOfRangeException(nameof(ProviderType), ProviderType, "Unsupported provider"),
            };
        }

        public static async Task<LiveProviderHost> StartAsync(QueueProviderType providerType)
        {
            if (providerType != QueueProviderType.LocalMemory)
                LiveContainerRuntime.EnsureConfigured();

            try
            {
                return providerType switch
                {
                    QueueProviderType.LocalMemory => StartLocalMemory(),
                    QueueProviderType.AzureStorage => await StartAzuriteAsync().ConfigureAwait(false),
                    QueueProviderType.AwsSqs => await StartLocalStackAsync().ConfigureAwait(false),
                    QueueProviderType.AzureServiceBus => await StartServiceBusEmulatorAsync().ConfigureAwait(false),
                    QueueProviderType.RabbitMq => await StartRabbitMqAsync().ConfigureAwait(false),
                    QueueProviderType.GooglePubSub => await StartPubSubEmulatorAsync().ConfigureAwait(false),
                    QueueProviderType.Redis => await StartRedisAsync().ConfigureAwait(false),
                    QueueProviderType.Valkey => await StartValkeyAsync().ConfigureAwait(false),
                    QueueProviderType.Dragonfly => await StartDragonflyAsync().ConfigureAwait(false),
                    QueueProviderType.ActiveMq => await StartActiveMqAsync().ConfigureAwait(false),
                    QueueProviderType.Beanstalkd => await StartBeanstalkdAsync().ConfigureAwait(false),
                    _ => throw new ArgumentOutOfRangeException(nameof(providerType), providerType, "Unsupported provider"),
                };
            }
            catch (Exception ex) when (IsDockerUnavailable(ex))
            {
                throw new InvalidOperationException(
                    $"Cannot start {providerType}: no container engine API available. " +
                    "Install Podman (preferred) or Docker, ensure the engine is running " +
                    $"(Podman socket typically at $XDG_RUNTIME_DIR/podman/podman.sock), then re-run with {LiveProviderSelection.EnvVar} set.",
                    ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_container != null)
                await _container.DisposeAsync().ConfigureAwait(false);
            if (_sidecar != null)
                await _sidecar.DisposeAsync().ConfigureAwait(false);
            if (_network != null)
                await _network.DisposeAsync().ConfigureAwait(false);
        }

        private static LiveProviderHost StartLocalMemory()
        {
            IQueueClient client = CreateLocalMemoryClient();
            return new LiveProviderHost(QueueProviderType.LocalMemory, client, container: null);
        }

        private static async Task<LiveProviderHost> StartAzuriteAsync()
        {
            IContainer container = new ContainerBuilder("mcr.microsoft.com/azure-storage/azurite:3.33.0")
                .WithCommand("azurite-queue", "--queueHost", "0.0.0.0", "--location", "/data", "--skipApiVersionCheck")
                .WithPortBinding(10001, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(10001))
                .Build();

            await container.StartAsync().ConfigureAwait(false);
            int port = container.GetMappedPublicPort(10001);
            string endpoint =
                $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey={AzuriteAccountKey};" +
                $"QueueEndpoint=http://{container.Hostname}:{port}/devstoreaccount1;";

            IQueueClient client = CreateAzureStorageClient(endpoint);
            return new LiveProviderHost(QueueProviderType.AzureStorage, client, container, azureEndpoint: endpoint);
        }

        private static async Task<LiveProviderHost> StartLocalStackAsync()
        {
            IContainer container = new ContainerBuilder("localstack/localstack:4.3")
                .WithEnvironment("SERVICES", "sqs")
                .WithPortBinding(4566, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r =>
                    r.ForPort(4566).ForPath("/_localstack/health")))
                .Build();

            await container.StartAsync().ConfigureAwait(false);
            string serviceUrl = $"http://{container.Hostname}:{container.GetMappedPublicPort(4566)}";
            IQueueClient client = CreateSqsClient(serviceUrl);
            return new LiveProviderHost(QueueProviderType.AwsSqs, client, container, sqsServiceUrl: serviceUrl);
        }

        private static async Task<LiveProviderHost> StartServiceBusEmulatorAsync()
        {
            // Official Testcontainers module: MSSQL sidecar + emulator, AMQP :5672, admin HTTP :5300.
            ServiceBusContainer emulator = new ServiceBusBuilder(
                    "mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.1")
                .WithAcceptLicenseAgreement(true)
                .Build();

            await emulator.StartAsync().ConfigureAwait(false);

            string connection = emulator.GetConnectionString();
            string adminConnection = emulator.GetHttpConnectionString();
            IQueueClient client = CreateServiceBusClient(connection, adminConnection);
            return new LiveProviderHost(
                QueueProviderType.AzureServiceBus,
                client,
                emulator,
                serviceBusConnection: connection,
                serviceBusAdminConnection: adminConnection);
        }

        private static async Task<LiveProviderHost> StartRabbitMqAsync()
        {
            IContainer container = new ContainerBuilder("rabbitmq:3.13-alpine")
                .WithPortBinding(5672, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string amqp = $"amqp://guest:guest@{container.Hostname}:{container.GetMappedPublicPort(5672)}/";
            IQueueClient client = CreateRabbitClient(amqp);
            return new LiveProviderHost(QueueProviderType.RabbitMq, client, container, amqpUri: amqp);
        }

        private static async Task<LiveProviderHost> StartPubSubEmulatorAsync()
        {
            IContainer container = new ContainerBuilder("messagebird/gcloud-pubsub-emulator:latest")
                .WithPortBinding(8681, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8681))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string host = $"{container.Hostname}:{container.GetMappedPublicPort(8681)}";
            IQueueClient client = CreatePubSubClient(host);
            return new LiveProviderHost(QueueProviderType.GooglePubSub, client, container, pubsubEmulatorHost: host);
        }

        private static async Task<LiveProviderHost> StartRedisAsync()
        {
            IContainer container = new ContainerBuilder("redis:7-alpine")
                .WithPortBinding(6379, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string config = $"{container.Hostname}:{container.GetMappedPublicPort(6379)}";
            IQueueClient client = CreateRedisClient(config);
            return new LiveProviderHost(QueueProviderType.Redis, client, container, redisConfig: config);
        }

        private static async Task<LiveProviderHost> StartValkeyAsync()
        {
            IContainer container = new ContainerBuilder("valkey/valkey:8.0-alpine")
                .WithPortBinding(6379, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string config = $"{container.Hostname}:{container.GetMappedPublicPort(6379)}";
            IQueueClient client = CreateValkeyClient(config);
            return new LiveProviderHost(QueueProviderType.Valkey, client, container, redisConfig: config);
        }

        private static async Task<LiveProviderHost> StartDragonflyAsync()
        {
            IContainer container = new ContainerBuilder("docker.dragonflydb.io/dragonflydb/dragonfly:v1.25.1")
                .WithPortBinding(6379, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string config = $"{container.Hostname}:{container.GetMappedPublicPort(6379)}";
            IQueueClient client = CreateDragonflyClient(config);
            return new LiveProviderHost(QueueProviderType.Dragonfly, client, container, redisConfig: config);
        }

        private static async Task<LiveProviderHost> StartActiveMqAsync()
        {
            IContainer container = new ContainerBuilder("apache/activemq-classic:6.1.4")
                .WithPortBinding(61616, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(61616))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string broker = $"tcp://{container.Hostname}:{container.GetMappedPublicPort(61616)}";
            IQueueClient client = CreateActiveMqClient(broker);
            return new LiveProviderHost(QueueProviderType.ActiveMq, client, container, activeMqBrokerUri: broker);
        }

        private static async Task<LiveProviderHost> StartBeanstalkdAsync()
        {
            IContainer container = new ContainerBuilder("schickling/beanstalkd")
                .WithPortBinding(11300, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(11300))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string host = container.Hostname;
            int port = container.GetMappedPublicPort(11300);
            IQueueClient client = CreateBeanstalkClient(host, port);
            return new LiveProviderHost(
                QueueProviderType.Beanstalkd,
                client,
                container,
                beanstalkHost: host,
                beanstalkPort: port);
        }

        private static IQueueClient CreateLocalMemoryClient()
        {
            var config = new LocalMemoryQueueProviderConfig(UniqueName("local"), visibilityTimeoutMilliseconds: 30_000);
            return QueueClientFactory.GetQueueClient(config);
        }

        private static IQueueClient CreateAzureStorageClient(string endpoint)
        {
            var config = new AzureQueueProviderConfig(
                name: UniqueName("azure"),
                endpoint: endpoint,
                highPriorityQueue: UniqueQueueName("az"),
                visibilityTimeoutMilliseconds: 30_000);
            return QueueClientFactory.GetQueueClient(config, NullLogger());
        }

        private static IQueueClient CreateSqsClient(string serviceUrl)
        {
            var config = new SqsQueueProviderConfig(
                name: UniqueName("sqs"),
                region: "us-east-1",
                highPriorityQueue: UniqueQueueName("sqs"),
                visibilityTimeoutMilliseconds: 30_000,
                serviceUrl: serviceUrl,
                accessKeyId: "test",
                secretAccessKey: "test");
            return QueueClientFactory.GetQueueClient(config, NullLogger());
        }

        private static IQueueClient CreateServiceBusClient(string connection, string administrationConnection = null)
        {
            var config = new AzureServiceBusProviderConfig(
                name: UniqueName("asb"),
                connectionString: connection,
                highPriorityQueue: UniqueQueueName("asb"),
                visibilityTimeoutMilliseconds: 30_000,
                administrationConnectionString: administrationConnection);
            return QueueClientFactory.GetQueueClient(config, NullLogger());
        }

        private static IQueueClient CreateRabbitClient(string amqpUri)
        {
            var config = new RabbitMqProviderConfig(
                name: UniqueName("rb"),
                amqpUri: amqpUri,
                highPriorityQueue: UniqueQueueName("rb"),
                visibilityTimeoutMilliseconds: 5_000);
            return QueueClientFactory.GetQueueClient(config, NullLogger());
        }

        private static IQueueClient CreatePubSubClient(string emulatorHost)
        {
            var config = new GooglePubSubProviderConfig(
                name: UniqueName("ps"),
                projectId: "test-project",
                highPriorityQueue: UniqueQueueName("ps"),
                visibilityTimeoutMilliseconds: 10_000,
                emulatorHost: emulatorHost);
            return QueueClientFactory.GetQueueClient(config, NullLogger());
        }

        private static IQueueClient CreateRedisClient(string redisConfig)
        {
            var config = new RedisQueueProviderConfig(
                name: UniqueName("rd"),
                configuration: redisConfig,
                highPriorityQueue: UniqueQueueName("rd"),
                visibilityTimeoutMilliseconds: 5_000);
            return QueueClientFactory.GetQueueClient(config, NullLogger());
        }

        private static IQueueClient CreateValkeyClient(string redisConfig)
        {
            var config = new ValkeyQueueProviderConfig(
                name: UniqueName("vk"),
                configuration: redisConfig,
                highPriorityQueue: UniqueQueueName("vk"),
                visibilityTimeoutMilliseconds: 5_000);
            return QueueClientFactory.GetQueueClient(config, NullLogger());
        }

        private static IQueueClient CreateDragonflyClient(string redisConfig)
        {
            var config = new DragonflyQueueProviderConfig(
                name: UniqueName("df"),
                configuration: redisConfig,
                highPriorityQueue: UniqueQueueName("df"),
                visibilityTimeoutMilliseconds: 5_000);
            return QueueClientFactory.GetQueueClient(config, NullLogger());
        }

        private static IQueueClient CreateActiveMqClient(string brokerUri)
        {
            var config = new ActiveMqProviderConfig(
                name: UniqueName("amq"),
                brokerUri: brokerUri,
                highPriorityQueue: UniqueQueueName("amq"),
                visibilityTimeoutMilliseconds: 5_000,
                userName: "admin",
                password: "admin");
            return QueueClientFactory.GetQueueClient(config, NullLogger());
        }

        private static IQueueClient CreateBeanstalkClient(string host, int port)
        {
            var config = new BeanstalkdProviderConfig(
                name: UniqueName("bs"),
                host: host,
                port: port,
                highPriorityQueue: UniqueQueueName("bs"),
                visibilityTimeoutMilliseconds: 5_000);
            return QueueClientFactory.GetQueueClient(config, NullLogger());
        }

        private static string UniqueName(string prefix)
        {
            string raw = $"{prefix}-{Guid.NewGuid():N}";
            return raw.Length <= 40 ? raw : raw.Substring(0, 40);
        }

        private static string UniqueQueueName(string prefix)
        {
            string raw = $"{prefix}-{Guid.NewGuid():N}";
            return raw.Length <= 63 ? raw : raw.Substring(0, 63);
        }

        private static ILogger NullLogger() =>
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        private static bool IsDockerUnavailable(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                string msg = e.Message ?? string.Empty;
                if (msg.Contains("Cannot connect to the Docker daemon", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("docker.sock", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("podman.sock", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
