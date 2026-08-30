# Queue Facade

Unified .NET queue facade (`Beztek.Facade.Queue`) over Azure Queue Storage, AWS SQS, and in-process LocalMemory.

Source: https://github.com/Beztek-Software-Solutions/queue-facade

## Projects

| Project | Description |
|---------|-------------|
| [`QueueFacade/`](QueueFacade/) | Library package `Beztek.Facade.Queue` (see [QueueFacade/README.md](QueueFacade/README.md) for full API and provider guidance) |
| [`QueueFacade.Tests/`](QueueFacade.Tests/) | NUnit unit tests |

## Quick start

```bash
dotnet restore queue-facade.sln
dotnet build queue-facade.sln
dotnet test QueueFacade.Tests/Beztek.Facade.Queue.Tests.csproj
```

With coverage (Coverlet; target ≥ 85% line coverage):

```bash
dotnet test QueueFacade.Tests/Beztek.Facade.Queue.Tests.csproj \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=./coverage/ \
  /p:Include='[Beztek.Facade.Queue]*' \
  /p:Threshold=85 \
  /p:ThresholdType=line
```

## NuGet

```bash
dotnet add package Beztek.Facade.Queue
```

See [QueueFacade/README.md](QueueFacade/README.md) for initialization samples, multi-tenant `{partition}` templates, poison-queue behavior, and message processing contracts.

## Providers

| Provider | Configuration type | Status |
|----------|-------------------|--------|
| LocalMemory | `LocalMemoryQueueProviderConfig` | Implemented (tests / single instance) |
| Azure Queue Storage | `AzureQueueProviderConfig` | Implemented |
| AWS SQS (standard queues) | `SqsQueueProviderConfig` | Implemented |
