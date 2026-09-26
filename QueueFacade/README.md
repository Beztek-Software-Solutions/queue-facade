# Queue Facade library

## Purpose

`Beztek.Facade.Queue` is a **portable competing-consumer work queue** for .NET. Application services hand work to a named queue and one or more worker processes pull and process that work. The same `IQueueClient` code runs against Azure Queue Storage, Azure Service Bus, AWS SQS, RabbitMQ, Google Pub/Sub, Redis / Valkey / Dragonfly, ActiveMQ, Beanstalkd, or in-process LocalMemory — swap the provider config, not the handlers.

It is **not** a general pub/sub bus, stream processor, or RPC framework. Use it when you need:

- Fire-and-forget or asynchronous **commands / jobs** between services
- **Horizontal scale-out** of workers (many consumers, each message handled once)
- A **poison / dead-letter** path when processing keeps failing
- Optional **multi-tenant** queue names via `{partition}` templates

## Competing consumers

Several worker instances call `DequeueAndProcess` on the **same** logical queues. The backing store hands each message to **exactly one** consumer at a time (visibility lock / ack / reserve). That is the competing-consumer (competing-queue) pattern:

```text
  Producers ──Enqueue──▶  [ high | low | poison queues ]
                                │
              ┌─────────────────┼─────────────────┐
              ▼                 ▼                 ▼
          Worker A          Worker B          Worker C
          DequeueAndProcess (same queue names, same app)
```

Semantics the facade standardizes:

| Concern | Behavior |
|---------|----------|
| Delivery | At-least-once while a message is invisible / locked; complete (ack/delete) removes it |
| Failure | Non-`ApplicationException` → retry after visibility timeout; `false` or max attempts → poison queue |
| Discard | Throw `ApplicationException` → delete without poison |
| Isolation | Poison queue defaults to `{high}-unprocessed` **per client name**, not one account-wide DLQ |

Workers must be **idempotent** where practical: after a crash, a locked message can become visible again and be delivered to another consumer.

## Core API (`IQueueClient`)

| Method | Behavior |
|--------|----------|
| `GetName` | Logical client name (factory cache key) |
| `Enqueue<T>` | Send one message or a list of messages |
| `EnqueueBatchedMessages<T>` | Pack many objects into fewer queue messages within size limits |
| `DequeueAndProcess` | Poll, rate-limit, and dispatch to `IMessageProcessor` or `IQueueProcessorHandler` |
| `StopDequeuing` | Stop an active dequeue loop |
| `GetApproximateQueueLength` | Approximate high- or low-priority queue depth |
| `GetApproximateUnprocessedQueueLength` | Approximate poison queue depth |
| `PeekUnprocessedMessagesAsync` | Inspect poison queue payloads (non-destructive on Azure) |
| `RequeueUnprocessedMessagesAsync` | Move poison messages back to the primary queue |

Obtain instances via `QueueClientFactory.GetQueueClient`. For multi-tenant templates, use `QueueClientFactory.GetPartitionedQueueClient` and `IPartitionedQueueClient.ForPartition`.

## Initializing queue clients

Provider **config** types live in the top-level `Beztek.Facade.Queue` namespace (same pattern as Cache and Storage). Provider *implementations* remain internal under `Beztek.Facade.Queue.Providers`.

Every example follows the same pattern: construct a provider config → `QueueClientFactory.GetQueueClient` → `Enqueue` / `DequeueAndProcess`. Poison queue defaults to `{highPriorityQueue}-unprocessed` unless you pass `unprocessedQueue`.

```csharp
using Beztek.Facade.Queue;
```

### LocalMemory (tests / single process)

```csharp
var config = new LocalMemoryQueueProviderConfig(
    name: "memory",
    visibilityTimeoutMilliseconds: 30_000);

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

### Azure Queue Storage

```csharp
var config = new AzureQueueProviderConfig(
    name: "booth-commands",
    endpoint: "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    highPriorityQueue: "al-booth-commands",
    lowPriorityQueue: null,              // optional
    visibilityTimeoutMilliseconds: 30_000,
    unprocessedQueue: null);             // optional; default {high}-unprocessed

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

### Azure Service Bus

```csharp
var config = new AzureServiceBusProviderConfig(
    name: "booth-commands",
    connectionString: "Endpoint=sb://....servicebus.windows.net/;SharedAccessKeyName=...;SharedAccessKey=...",
    highPriorityQueue: "al-booth-commands",
    lowPriorityQueue: null,
    visibilityTimeoutMilliseconds: 30_000,
    unprocessedQueue: null);

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

Notes:

- Uses Service Bus **queues** (not topics/subscriptions).
- For the local emulator (image 2.x+): set `UseDevelopmentEmulator=true` on the AMQP connection
  string, and pass `administrationConnectionString` from the emulator HTTP endpoint (port 5300)
  so `CreateIfNotExists` can use `ServiceBusAdministrationClient`. Emulator runtime properties
  report `ActiveMessageCount=0` (known limitation); depth APIs need a real namespace to be meaningful.

### AWS SQS

```csharp
var config = new SqsQueueProviderConfig(
    name: "booth-commands",
    region: "us-east-1",
    highPriorityQueue: "al-booth-commands",
    lowPriorityQueue: null,              // optional
    visibilityTimeoutMilliseconds: 30_000,
    serviceUrl: null,                    // or "http://localhost:4566" for LocalStack
    accessKeyId: null,                   // null = default AWS credential chain
    secretAccessKey: null,
    unprocessedQueue: null,
    sessionToken: null);                 // optional STS token (does not auto-refresh)

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

Notes:

- Queue names follow portable rules via `QueueNameValidator` (same as Azure).
- Max receive batch: 10. Max message body: 256 KiB.
- Credentials: omit `accessKeyId` / `secretAccessKey` to use the default AWS
  credential chain (env / profile / **IAM role**). Pass explicit keys only when
  needed (local tooling). Prefer the default chain for roles over static STS triples.
- FIFO (`.fifo`) queues are not supported.
- **Peek is not supported.** `PeekUnprocessedMessagesAsync` receives with a visibility timeout (same as a normal receive). Prefer `RequeueUnprocessedMessagesAsync` without peeking first.

### RabbitMQ

```csharp
var config = new RabbitMqProviderConfig(
    name: "booth-commands",
    amqpUri: "amqp://user:pass@localhost:5672/",
    highPriorityQueue: "al-booth-commands",
    lowPriorityQueue: null,
    visibilityTimeoutMilliseconds: 30_000,
    unprocessedQueue: null);

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

Notes:

- No broker-native peek; poison “peek” receives messages (manual ack path).
- Visibility timeout is emulated (ack + republish with incremented attempt).

### Google Cloud Pub/Sub

```csharp
var config = new GooglePubSubProviderConfig(
    name: "booth-commands",
    projectId: "my-gcp-project",
    highPriorityQueue: "al-booth-commands",   // topic id; subscription = "{topic}-sub"
    lowPriorityQueue: null,
    visibilityTimeoutMilliseconds: 30_000,
    unprocessedQueue: null,
    emulatorHost: null);                     // e.g. "localhost:8085" for the emulator

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

Notes:

- Pull subscriptions only. `CreateIfNotExists` creates the topic and `{topic}-sub` subscription.
- Set `emulatorHost` (or `PUBSUB_EMULATOR_HOST`) for local emulator testing.
- Approximate depth is **best-effort** (no cheap exact depth API on many deployments / emulator).

### Redis

```csharp
var config = new RedisQueueProviderConfig(
    name: "booth-commands",
    configuration: "localhost:6379",         // StackExchange.Redis config string
    highPriorityQueue: "al-booth-commands",
    lowPriorityQueue: null,
    visibilityTimeoutMilliseconds: 30_000,
    unprocessedQueue: null);

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

Notes:

- Uses Redis **lists**: ready list + processing list, atomic `RPOPLPUSH` / `LMOVE` (`ListRightPopLeftPush`) so competing consumers do not double-claim a message. A background reclaim moves expired processing entries back to ready (visibility timeout).
- Keys use a Redis Cluster hash tag (`qf:{queue}:ready` / `qf:{queue}:proc`) so both lists stay on one slot — required for **ElastiCache / Redis Cluster**.
- `configuration` is any StackExchange.Redis connection string (host, password, ssl, abortConnect, etc.).

**Is Redis efficient for this?** For modest throughput and short payloads, yes: list ops are O(1) claim and work well as a lightweight queue. It is **not** a dedicated broker: reclaim scans the processing list (cost grows with in-flight depth), there is no first-class delayed/scheduled delivery UI, observability is DIY, and memory + persistence (AOF/RDB) are your operational burden. Prefer SQS, Service Bus, or RabbitMQ when you need cloud-managed DLQ metrics, long retention, or very large fan-out. Redis/Valkey shine when you already run a cache and want a simple shared work queue without another managed service.

**ElastiCache (Redis OSS and Valkey):** Supported. AWS documents list commands including `LPUSH`, `RPOPLPUSH` / `LMOVE`, and blocking variants. Point `configuration` at the primary endpoint (TLS/auth as required). Use cluster-mode only with the hash-tagged keys above (already the library default). Serverless ElastiCache supports the same list primitives; validate size/TTL limits for your workload.

### Valkey

Valkey speaks the Redis protocol; the same list/processing-list provider is reused (including on **Amazon ElastiCache for Valkey**).

```csharp
var config = new ValkeyQueueProviderConfig(
    name: "booth-commands",
    configuration: "localhost:6379",
    highPriorityQueue: "al-booth-commands",
    lowPriorityQueue: null,
    visibilityTimeoutMilliseconds: 30_000,
    unprocessedQueue: null);

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

### Dragonfly

Dragonfly speaks the Redis protocol; the same list/processing-list provider is reused.

```csharp
var config = new DragonflyQueueProviderConfig(
    name: "booth-commands",
    configuration: "localhost:6379",
    highPriorityQueue: "al-booth-commands",
    lowPriorityQueue: null,
    visibilityTimeoutMilliseconds: 30_000,
    unprocessedQueue: null);

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

### ActiveMQ (including Amazon MQ ActiveMQ engines)

```csharp
var config = new ActiveMqProviderConfig(
    name: "booth-commands",
    brokerUri: "tcp://localhost:61616",
    highPriorityQueue: "al-booth-commands",
    lowPriorityQueue: null,
    visibilityTimeoutMilliseconds: 30_000,
    unprocessedQueue: null,
    userName: "admin",
    password: "admin");

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

### Beanstalkd

```csharp
var config = new BeanstalkdProviderConfig(
    name: "booth-commands",
    host: "localhost",
    port: 11300,
    highPriorityQueue: "al-booth-commands",   // tube name
    lowPriorityQueue: null,
    visibilityTimeoutMilliseconds: 30_000,
    unprocessedQueue: null);

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

Notes:

- Tubes map 1:1 to high / low / poison queue names.
- TTR is derived from `visibilityTimeoutMilliseconds`.

## Multi-tenant partitions (`{partition}`)

Embed `{partition}` in queue name templates (usually a **customer id**). Use `GetPartitionedQueueClient` — do not call `GetQueueClient` with unresolved templates.

```csharp
var template = new SqsQueueProviderConfig(
    name: "booth-commands",
    region: "us-east-1",
    highPriorityQueue: "al-booth-cmd-{partition}");
// poison defaults to: al-booth-cmd-{partition}-unprocessed

IPartitionedQueueClient partitioned = QueueClientFactory.GetPartitionedQueueClient(template);
IQueueClient forCustomer = partitioned.ForPartition(customerId);
await forCustomer.Enqueue(payload, useHighPriorityQueue: true);
```

Each partition gets its own queues (and poison queue). Partition keys are always lowercased and must use portable naming (no underscores). Watch cloud **queue-count limits** — prefer create-on-first-use (`CreateIfNotExists`).

## Poison queue and message processing

Each provider supports a high-priority queue, an optional low-priority queue, and a **per-client poison queue** defaulting to `{highPriorityQueue}-unprocessed` (override with `unprocessedQueue`). That keeps multiple apps in one cloud account from sharing one global poison queue.

### Processing contract

1. Application handling messages implements `IMessageProcessor`. If the processor throws an exception that is **not** `System.ApplicationException`, the message stays on the queue and becomes visible again after the visibility timeout (~30s by default). `Message.ProcessingAttempt` / the provider receive count increments on each delivery. After `QueueDequeueConfig.MaxProcessingAttempts` (default **5**), the message is moved to the poison queue (`{high}-unprocessed`) and deleted from the primary queue so other work is not blocked.
2. Returning `false` from the processor moves the message to the poison queue immediately (same as max attempts).
3. If the processor needs to discard a message without poison (e.g. validation), catch and throw `System.ApplicationException` so the message is deleted and not retried.
4. Inspect poison payloads with `PeekUnprocessedMessagesAsync`. Move them back to the primary queue with `RequeueUnprocessedMessagesAsync` (receive-count / attempts reset).
5. In the `MessageProcessor`, unwrap with `Message.GetMessageObject<T>()`, not a bare string.

### Usage steps

1. Obtain the provider connection details (endpoint / broker URI / project id / etc.) and portable queue names.
2. Implement `IMessageProcessor` (e.g. class `ProcessMessage`).
3. Construct the matching `*ProviderConfig` and call `QueueClientFactory.GetQueueClient(...)`.
4. Use `client.Enqueue(...)` to send a message, or `client.EnqueueBatchedMessages(...)` to send many objects in fewer queue messages.
5. Create a processor instance and call `client.DequeueAndProcess(..., processor, cancellationToken)`.

## Portable queue naming (all providers)

Names must work across cloud backends (`QueueNameValidator` — common denominator of Azure Queue Storage and AWS SQS):

- 3–63 characters
- Lowercase letters, digits, and hyphens only (no underscores, no uppercase)
- Must start and end alphanumeric; no consecutive hyphens
- Reserved name `test` is rejected
- FIFO (`.fifo`) is not supported

Partition keys (`{partition}`) follow the same character rules and are always lowercased.

## Providers

Config classes are in **`Beztek.Facade.Queue`** (not `.Providers`):

| `QueueProviderType` | Config class | Backend |
|---------------------|--------------|---------|
| `LocalMemory` | `LocalMemoryQueueProviderConfig` | In-process (tests / single instance) |
| `AzureStorage` | `AzureQueueProviderConfig` | Azure Queue Storage |
| `AwsSqs` | `SqsQueueProviderConfig` | AWS SQS (standard queues) |
| `AzureServiceBus` | `AzureServiceBusProviderConfig` | Azure Service Bus queues |
| `RabbitMq` | `RabbitMqProviderConfig` | RabbitMQ classic queues |
| `GooglePubSub` | `GooglePubSubProviderConfig` | Google Cloud Pub/Sub (pull) |
| `Redis` | `RedisQueueProviderConfig` | Redis lists + processing-list visibility |
| `Valkey` | `ValkeyQueueProviderConfig` | Valkey (Redis protocol; same provider) |
| `Dragonfly` | `DragonflyQueueProviderConfig` | Dragonfly (Redis protocol; same provider) |
| `ActiveMq` | `ActiveMqProviderConfig` | Apache ActiveMQ (Amazon MQ ActiveMQ engines) |
| `Beanstalkd` | `BeanstalkdProviderConfig` | Beanstalkd tubes |

## Provider semantics and limitations

The facade API is shared; **back-end fidelity is not identical**. Treat the rows marked *questionable* as “works for happy-path competing consumers, but do not rely on this edge for production ops.”

| Provider | Competing consumers | Peek poison | Approximate depth | Notes / when questionable |
|----------|---------------------|-------------|-------------------|---------------------------|
| **LocalMemory** | Process-local only | Yes | Exact | Not multi-host. |
| **Azure Queue Storage** | Yes (visibility) | Yes (true peek) | Approximate | Strong fit for this library’s model. |
| **AWS SQS** | Yes (visibility) | **No true peek** — receive + visibility hides the message | Approximate | **Do not peek-then-requeue**; live tests requeue without peeking first. No FIFO. |
| **Azure Service Bus** | Yes (peek-lock) | Yes | Approximate (cloud); **emulator reports `ActiveMessageCount=0`** | Prefer queues not topics. Emulator needs separate admin connection string (`:5300`) and `MaxDeliveryCount` ≤ 10. |
| **RabbitMQ** | Yes (manual ack) | **No** — get consumes; “peek” receives | Yes (`QueueDeclarePassive`) | Visibility is emulated by ack + republish after timeout — fine for tests, weaker than broker-native TTL/retry plugins. |
| **Google Pub/Sub** | Yes (ack deadline) | **No** — pull leases | **Weak** (sample pull / 0–1 style) | Pull subscriptions only; closer to pub/sub than a classic queue. Depth APIs are best-effort. |
| **Redis / Valkey / Dragonfly** | Yes (atomic list move) | Yes (list range on ready) | Yes (`LLEN`) | Efficient for modest loads. Visibility reclaim scans at most the oldest `MaxReclaimScan` (512) processing entries per tick — not a full broker. ElastiCache OK with hash-tagged keys. |
| **ActiveMQ** | Yes (individual ack) | Limited | Browser/probe based | Prefetch and ack modes affect competing behavior; depth probe is approximate. |
| **Beanstalkd** | Yes (reserve / TTR) | Buried peek | Stats-based | Good tube model; custom TCP text client (protocol unit-tested over an in-memory stream), not a cloud-managed service. |

**Production shortlist for “real” queues:** Azure Queue Storage, SQS, Service Bus, RabbitMQ, Beanstalkd. **Redis/Valkey:** good when co-located with cache and traffic is moderate. **Pub/Sub / ActiveMQ:** usable, but peek/depth and some failure modes are the weakest matches to the facade contract.

## Testing

Unit tests use **Moq** against injectable seams (default CI stays container-free, `Category!=Live`):

| Seam | Covers |
|------|--------|
| `SqsClientCreator` / `AzureStorageClientCreator` | SQS, Azure Queue Storage |
| `IAzureServiceBusOps` / `IPubSubOps` | Service Bus, Google Pub/Sub |
| `IChannel` / `ISession` / `IBeanstalkdClient` | RabbitMQ, ActiveMQ, Beanstalkd providers |
| `BeanstalkdClient(Stream)` | Beanstalkd text protocol (no TCP) |
| Real NonPersistent Redis / LocalMemory | Redis codec + in-process provider |

Thin live SDK adapters (`AzureServiceBusOps`, `PubSubOps`) stay `[ExcludeFromCodeCoverage]`; exercise them via `QUEUEFACADE_LIVE_PROVIDERS`. Target ≥ **85%** line coverage (`make coverage` / `make coverage-check`).

Optional live suite (`QUEUEFACADE_LIVE_PROVIDERS`): LocalMemory, Azurite, LocalStack, Service Bus emulator, RabbitMQ, Pub/Sub emulator, Redis, ActiveMQ, Beanstalkd — see the repo [README](../README.md#live-container-tests).

```bash
QUEUEFACADE_LIVE_PROVIDERS=all make test-live
```

XML documentation is included in the NuGet package (`GenerateDocumentationFile`).
