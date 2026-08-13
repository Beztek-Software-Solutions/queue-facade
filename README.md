# Queue Facade Library

This library is intended for inter-service communication by use of Queues.

# Overview

It is intended to be cloud portable and take advantage of the native managed services in each cloud, such as Azure Queue Storage and AWS Simple Queue Service.

It is a reusable and configurable queue library that can ensure that just one consumer among multiple competing consumers processes each message.
This library should be available for any micro-service for this use case.

## Providers

| `QueueProviderType` | Config class | Backend |
|---------------------|--------------|---------|
| `LocalMemory` | `LocalMemoryQueueProviderConfig` | In-process (tests / single instance) |
| `AzureStorage` | `AzureQueueProviderConfig` | Azure Queue Storage |
| `AwsSqs` | `SqsQueueProviderConfig` | AWS SQS (standard queues) |

Each provider supports a high-priority queue, an optional low-priority queue, and a **per-client poison queue** defaulting to `{highPriorityQueue}-unprocessed` (override with `unprocessedQueue`). That keeps multiple apps in one cloud account from sharing one global poison queue.

### Multi-tenant partitions (`{partition}`)

Embed `{partition}` in queue name templates (usually a **customer id**). Use `GetPartitionedQueueClient` — do not call `GetQueueClient` with unresolved templates.

```csharp
var template = new SqsQueueProviderConfig(
    name: "booth-commands",
    region: "us-east-1",
    highPriorityQueue: "al-booth-cmd-{partition}");
// poison defaults to: al-booth-cmd-{partition}-unprocessed

IPartitionedQueueClient partitioned = QueueClientFactory.GetPartitionedQueueClient(template);
IQueueClient forChurch = partitioned.ForPartition(customerId);
await forChurch.Enqueue(payload, useHighPriorityQueue: true);
```

Each partition gets its own queues (and poison queue). Partition keys are always lowercased and must use portable naming (no underscores). Watch cloud **queue-count limits** — prefer create-on-first-use (`CreateIfNotExists`).

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

- Queue names: **portable** rules via `QueueNameValidator` (same as Azure) — see below.
- Max receive batch: 10. Max message body: 256 KiB.
- Credentials: default chain (env / profile / IAM role), or pass explicit keys on the config.

## Portable queue naming (all providers)

Names must work on **both** Azure Queue Storage and AWS SQS (`QueueNameValidator`):

   - 3–63 characters
   - Lowercase letters, digits, and hyphens only (no underscores, no uppercase)
   - Must start and end alphanumeric; no consecutive hyphens
   - Reserved name `test` is rejected
   - FIFO (`.fifo`) is not supported

Partition keys (`{partition}`) follow the same character rules and are always lowercased.
## Steps to use Queue Facade

1. Find Azure storage connection string and queue names, or AWS region + queue names, or create a new queue by providing a new queue name
2. Implement callback interface IMessageProcessor such as class ProcessMessage
3. Use QueueClientFactory to create Queue client by passing connection string and at lease one queue name
       client=QueueClientFactory.GeteQueueClient(…)
4. Use client.Enqueue(…) to send generic message to queue, or client.EnqueueBatchedMessages(...) to send list of messages in batch mode.
       Example1:  bool result = await client.Enqueue<string>(stringMessage, true, activityId);
       Example2:  IList<bool> results = await client.Enqueue<string>(stringList, true, activityId);
       Example3:  List<string> unsentMessages = await client.EnqueueBatchedMessages<string>(stringList, true, activityId);

       Notice that example3 batch input stringList in chunks, each chunk includes a sub-list of input.
       This not only result in less messages in queue than example2, but also allow consumer(etc. event scheduler) to handle batched messages more efficiently.

5. Create an instance of ProcessMessage, ProcessMessage callback = new ProcessMessage()
6. Use client.DequeueAndProcess(… callback) to retrieve messages from queue
7. The callback instance should have the messages

### Critical Details

1. Application handling messages implements `IMessageProcessor`, if MessageProcessor implementation throws an exception which is Not `System.ApplicationException` then the Queue will again make the message visible to the message processor until message processor succeeds.
2. If MessageProcessor needs to handle some application exceptions such as ValidationException, it should catch and throw `System.ApplicationException` in order to avoid getting same message again and again.
3. In the MessageProcessor, we need to use the UnwrappedMessage class and not string, it will throw an exception.
