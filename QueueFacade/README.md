# Queue Facade library

## Introduction

`Beztek.Facade.Queue` is a cloud-portable queue facade for .NET inter-service messaging. Services enqueue and dequeue through a single `IQueueClient` API; the library routes operations to Azure Queue Storage, AWS SQS, or an in-process LocalMemory provider based on configuration.

The library ensures that exactly one consumer among competing consumers processes each message, with per-client poison queues, portable queue naming, and optional multi-tenant `{partition}` templates.

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

```csharp
using Beztek.Facade.Queue;
```

### AWS SQS

```csharp
var config = new SqsQueueProviderConfig(
    name: "booth-commands",
    region: "us-east-1",
    highPriorityQueue: "al-booth-commands",
    lowPriorityQueue: null,           // optional
    visibilityTimeoutMilliseconds: 30_000,
    serviceUrl: null,                 // or "http://localhost:4566" for LocalStack
    accessKeyId: null,                // null = default AWS credential chain
    secretAccessKey: null);

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

Notes:

- Queue names follow portable rules via `QueueNameValidator` (same as Azure).
- Max receive batch: 10. Max message body: 256 KiB.
- Credentials: default chain (env / profile / IAM role), or pass explicit keys on the config.
- FIFO (`.fifo`) queues are not supported.

### Azure Queue Storage

```csharp
var config = new AzureQueueProviderConfig(
    name: "booth-commands",
    endpoint: "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    highPriorityQueue: "al-booth-commands",
    lowPriorityQueue: null,
    visibilityTimeoutMilliseconds: 30_000);

IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
await client.Enqueue(payload, useHighPriorityQueue: true);
```

### LocalMemory (tests / single process)

```csharp
var config = new LocalMemoryQueueProviderConfig("memory", visibilityTimeoutMilliseconds: 30_000);
IQueueClient client = QueueClientFactory.GetQueueClient(config, logger);
```

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

1. Obtain Azure storage connection string and queue names, or AWS region + queue names, or create a new queue by providing a new queue name.
2. Implement `IMessageProcessor` (e.g. class `ProcessMessage`).
3. Use `QueueClientFactory.GetQueueClient(...)` to create a queue client.
4. Use `client.Enqueue(...)` to send a message, or `client.EnqueueBatchedMessages(...)` to send many objects in fewer queue messages.
5. Create a processor instance and call `client.DequeueAndProcess(..., processor, cancellationToken)`.

## Portable queue naming (all providers)

Names must work on **both** Azure Queue Storage and AWS SQS (`QueueNameValidator`):

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

## Testing

Unit tests use **Moq** for SQS and Azure SDK clients (`IAmazonSQS`, `QueueClient`) via injectable `SqsClientCreator` and `AzureStorageClientCreator` test doubles. LocalMemory tests use the real in-process provider. No cloud credentials, LocalStack, or Azurite are required in CI.

XML documentation is included in the NuGet package (`GenerateDocumentationFile`).
