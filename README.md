# Queue Facade

Unified .NET queue facade (`Beztek.Facade.Queue`) over Azure Queue Storage, AWS SQS, Azure Service Bus, RabbitMQ, Google Pub/Sub, Redis, ActiveMQ, Beanstalkd, and in-process LocalMemory.

Source: https://github.com/Beztek-Software-Solutions/queue-facade

## Projects

| Project | Description |
|---------|-------------|
| [`QueueFacade/`](QueueFacade/) | Library package `Beztek.Facade.Queue` (see [QueueFacade/README.md](QueueFacade/README.md) for full API and provider guidance) |
| [`QueueFacade.Tests/`](QueueFacade.Tests/) | NUnit unit tests + optional Testcontainers live suite (`QUEUEFACADE_LIVE_PROVIDERS`) |

## Quick start

```bash
make test
make test-unit
make test-live   # requires QUEUEFACADE_LIVE_PROVIDERS
make coverage
make coverage-html
dotnet restore queue-facade.sln
dotnet build queue-facade.sln
dotnet test QueueFacade.Tests/Beztek.Facade.Queue.Tests.csproj
```

`make test`, `make test-live`, and `make coverage` serialize on `flock --close .dotnet-build.lock` (same pattern as cache-facade) so parallel make targets do not race shared `bin/` outputs. `--close` drops the lock FD after acquire so MSBuild node-reuse workers cannot inherit and hold the lock. Do **not** wrap `make …` in another outer `flock` on the same file — that deadlocks.

With coverage (Coverlet; target ≥ 85% line coverage):

```bash
make coverage-html
```

### Live container tests

Optional Testcontainers suite under `QueueFacade.Tests/Live/`. Unset env → not discovered (default CI stays container-free).

Uses the **Docker Engine API**. Prefer **Podman** (rootless): the suite auto-detects `$XDG_RUNTIME_DIR/podman/podman.sock`.

```bash
# One provider
QUEUEFACADE_LIVE_PROVIDERS=redis make test-live

# Subset
QUEUEFACADE_LIVE_PROVIDERS=rabbitmq,redis,beanstalkd make test-live

# Every provider (all backends)
QUEUEFACADE_LIVE_PROVIDERS=all make test-live
```

Aliases: `localmemory`/`local`, `azure`/`azurite`, `sqs`/`aws`/`localstack`, `servicebus`/`asb`, `rabbitmq`/`rabbit`, `pubsub`/`gcp`/`google`, `redis`, `valkey`, `dragonfly`/`df`, `activemq`/`amq`, `beanstalkd`/`beanstalk`, `all`.

Live cases cover enqueue → dequeue, approximate depth, **poison on false / max-attempts**, and **requeue → process** (isolated queues per test on a shared backend).

| Live provider | Container |
|---------------|-----------|
| LocalMemory | in-process |
| Azure Queue Storage | Azurite |
| AWS SQS | LocalStack |
| Azure Service Bus | Testcontainers.ServiceBus (emulator + MSSQL) |
| RabbitMQ | rabbitmq:3.13-alpine |
| Google Pub/Sub | messagebird/gcloud-pubsub-emulator |
| Redis | redis:7-alpine |
| Valkey | valkey/valkey:8.0-alpine |
| Dragonfly | docker.dragonflydb.io/dragonflydb/dragonfly |
| ActiveMQ | apache/activemq-classic |
| Beanstalkd | schickling/beanstalkd |

## NuGet

```bash
dotnet add package Beztek.Facade.Queue
```

See [QueueFacade/README.md](QueueFacade/README.md) for purpose / competing consumers, initialization samples, **provider limitations** (peek, depth, Redis/ElastiCache), multi-tenant `{partition}` templates, and poison-queue contracts.

Provider **config** types are in `Beztek.Facade.Queue` (top-level), consistent with Cache and Storage.

## Providers

| Provider | Configuration type (namespace `Beztek.Facade.Queue`) | Status |
|----------|-------------------|--------|
| LocalMemory | `LocalMemoryQueueProviderConfig` | Implemented (tests / single instance) |
| Azure Queue Storage | `AzureQueueProviderConfig` | Implemented |
| AWS SQS (standard queues) | `SqsQueueProviderConfig` | Implemented |
| Azure Service Bus | `AzureServiceBusProviderConfig` | Implemented |
| RabbitMQ | `RabbitMqProviderConfig` | Implemented |
| Google Pub/Sub | `GooglePubSubProviderConfig` | Implemented |
| Redis | `RedisQueueProviderConfig` | Implemented |
| Valkey | `ValkeyQueueProviderConfig` | Implemented (Redis protocol) |
| Dragonfly | `DragonflyQueueProviderConfig` | Implemented (Redis protocol) |
| ActiveMQ | `ActiveMqProviderConfig` | Implemented |
| Beanstalkd | `BeanstalkdProviderConfig` | Implemented |
