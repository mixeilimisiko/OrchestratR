# OrchestratR – Saga Orchestrator for .NET

## Installation

To install **OrchestratR**, add the **OrchestratR NuGet package** to your project (available on NuGet as `OrchestratR`). For example, using the .NET CLI:

```bash
dotnet add package OrchestratR
```

**OrchestratR** targets **.NET 8** and depends on several libraries (minimum versions shown):
- **Entity Framework Core (EF Core)** 8.0.15 (including the `Microsoft.EntityFrameworkCore`, `...Relational`, and `...SqlServer` packages)
- **Microsoft.Extensions.Hosting.Abstractions** 8.0.1
- **Polly** 8.5.2

These will be installed automatically via NuGet.

### EF Core Migrations
**OrchestratR** includes built-in EF Core migrations for its Saga store. The migrations are embedded under `OrchestratR.Persistence/Migrations` and will run automatically on startup by default. If you prefer to manage the database schema yourself, you can disable automatic migration application by calling the `SkipMigrationApplication()` option when configuring **OrchestratR** (see Configuration & Options below).

## Quick Start

Let's walk through a minimal "Hello World" saga to demonstrate **OrchestratR's** usage. We'll define a simple saga context and two saga steps, register the saga with **OrchestratR's** DI extensions, and then start and resume the saga. This example is based on the **DemoAPI** included in the repository.

### 1. Define a Saga Context

Create a class derived from `SagaContext` to hold any state for the saga. For a simple example:

```csharp
public class HelloSagaContext : OrchestratR.Core.SagaContext 
{ 
    public bool Step1Done { get; set; }
    public bool Step2Done { get; set; }
}
```

The context inherits from `SagaContext` (an abstract marker class) and can include properties to share data between steps.

### 2. Implement Saga Steps

Define classes implementing `ISagaStep<TContext>` for each step in the workflow. Each step must implement an `ExecuteAsync` method (and optionally a `CompensateAsync` for rollback). The `ExecuteAsync` should return a `SagaStepStatus` indicating whether to continue to the next step or pause the saga.

```csharp
public class Step1 : ISagaStep<HelloSagaContext>
{
    public async Task<SagaStepStatus> ExecuteAsync(HelloSagaContext ctx, CancellationToken cancel = default)
    {
        Console.WriteLine("Hello from Step1");
        ctx.Step1Done = true;
        // Indicate that the saga should wait (pause) after this step
        return SagaStepStatus.Wait;
    }

    public Task CompensateAsync(HelloSagaContext ctx, CancellationToken cancel = default)
    {
        // No compensation needed in this simple example
        return Task.CompletedTask;
    }
}

public class Step2 : ISagaStep<HelloSagaContext>
{
    public async Task<SagaStepStatus> ExecuteAsync(HelloSagaContext ctx, CancellationToken cancel = default)
    {
        Console.WriteLine("World from Step2");
        ctx.Step2Done = true;
        return SagaStepStatus.Continue; // Continue (saga will complete if this is last step)
    }

    public Task CompensateAsync(HelloSagaContext ctx, CancellationToken cancel = default)
    {
        return Task.CompletedTask;
    }
}
```

In this example, **Step1** prints a message and returns `SagaStepStatus.Wait` to simulate waiting for an external event before proceeding. **Step2** prints another message and returns `Continue`. (In a real saga, a `Wait` status could indicate waiting for a callback or a time delay.)

### 3. Register the Saga

In your application startup (e.g. `Program.cs` using the generic host or in `ConfigureServices`), use **OrchestratR's** extension methods to register the saga and its infrastructure:

```csharp
// In Program.cs or Startup.cs:
services.AddSaga<HelloSagaContext>(saga => saga
    .WithStep<Step1>()       // Register Step1 in the saga
    .WithStep<Step2>()       // Register Step2 in the saga
    .WithRecovery());        // Enable automated recovery (background service)

services.AddSagaInfrastructure(options => options
    .UseInMemory()           // Use the in-memory saga store (for demo or testing)
    .SkipMigrationApplication());
```

Here we call `AddSaga<TContext>()` to define a saga of type `HelloSagaContext`. We fluently add two steps to the saga via `WithStep<Step1>()` and `WithStep<Step2>()`. The `WithRecovery()` call enables the saga recovery background service. Finally, `AddSagaInfrastructure` configures the persistence: in this case we choose an in-memory store (no database needed) and skip applying EF Core migrations (not applicable for in-memory). If using a database, you would call `UseEfCore(...)` to specify the DB provider and connection.

### 4. Starting the Saga

Resolve an `ISagaOrchestrator<HelloSagaContext>` (automatically registered in the DI container) and start a saga instance:

```csharp
var orchestrator = services.GetRequiredService<ISagaOrchestrator<HelloSagaContext>>();
var context = new HelloSagaContext();
Guid sagaId = await orchestrator.StartAsync(context);
Console.WriteLine($"Saga started with ID {sagaId}");
```

Calling `StartAsync` will execute the saga's steps in order until a step returns `Wait` or the saga completes. In our example, **Step1** will run and then pause the saga (since it returned `Wait`). The returned `Guid` is the Saga's unique ID, which we'll use to resume it.

### 5. Resuming the Saga

After the condition that **Step1** was waiting for is met (in this trivial case, immediately or via some external trigger), resume the saga:

```csharp
// ...some time later or in response to an event:
await orchestrator.ResumeAsync(sagaId, ctx => {
    // Optionally update context before resuming
    ctx.Step1Done = true;
});
```

The `ResumeAsync` method can take the saga's ID and a lambda to update the context before resuming. **OrchestratR** will load the persisted context (or use the in-memory one), apply any updates you specify, and then continue with the next pending step (here, **Step2**). In our example, **Step2** will run and print its message, then the saga will complete.

That's it – you've executed a simple two-step saga with a pause in the middle. In a more realistic scenario, the `Wait` might correspond to waiting for an external system (e.g. payment confirmation) or a time-based event, and the resume call would be triggered by that external event. The **DemoAPI** in the repository contains an **OrderSaga** example demonstrating a saga that reserves inventory, waits for payment, then ships an order upon resume.

## Core Concepts

### SagaContext
A saga's context is the data that flows through the saga, analogous to a transaction's state. In **OrchestratR**, you define a custom context class (inheriting `OrchestratR.Core.SagaContext`) to include any fields needed for your workflow (order ID, flags indicating step results, etc.). The context is serialized and persisted between steps (if using EF Core persistence) or held in memory (for in-memory mode).

### ISagaStep<TContext>
A saga is composed of steps, each implementing `ISagaStep<TContext>` for the saga's context type. A saga step encapsulates a single unit of work. The `ExecuteAsync(TContext, CancellationToken)` method is called by the orchestrator to perform the step's action. It returns a `SagaStepStatus` which signals the orchestrator to `Continue` to the next step or `Wait` (pause) the saga.

If a step throws an exception (not handled by a retry policy), the compensation may be triggered. Each step can also implement `CompensateAsync(TContext, ...)` to undo its work if a later step fails and the saga needs to roll back. (**OrchestratR** will call compensation methods in reverse order for any executed steps if the saga is aborted.)

### SagaBuilder<TContext>
This is a fluent builder used to configure the saga's steps and behaviors during service registration. When you call `AddSaga<TContext>(services, saga => { ... })`, **OrchestratR** creates a `SagaBuilder<TContext>` that you use to add steps (via `WithStep<TStep>` calls) and optional settings like `WithRecovery()` or custom step policies (retry/timeouts).

The builder collects the saga's definition (list of steps, etc.) and then finalizes the registration. Internally, building the saga will register the necessary orchestrator and step services with the DI container, including an `SagaOrchestrator<TContext>` which implements `ISagaOrchestrator<TContext>`.

### SagaOrchestrator<TContext>
This is the core runtime component that orchestrates saga execution. The orchestrator is registered as a scoped service via the builder and is responsible for tracking saga progress, invoking step `ExecuteAsync` methods, and handling persistence.

You normally interact with it through the `ISagaOrchestrator<TContext>` interface – e.g. calling `StartAsync()` to begin a saga, or `ResumeAsync()` to continue a paused saga. The orchestrator uses the `SagaStepStatus` returned by steps to decide whether to proceed or halt.

If a saga completes all steps successfully, the orchestrator will mark it as **Completed**. If a step fails (throws an exception) and a retry policy doesn't handle it, the orchestrator will mark the saga as **Failed** and attempt compensation for any executed steps.

### EF Core Persistence vs In-Memory Store
**OrchestratR** supports durable persistence of saga state using EF Core (with a SQL Server example) out of the box. When using EF Core, saga contexts and metadata are stored in a database table via the `SagaDbContext` in `OrchestratR.Persistence`, and the included migrations set up this schema.

Alternatively, for simple scenarios or testing, you can use the in-memory store which keeps saga state in memory (not durable across application restarts). You choose the implementation by calling either `UseEfCore(...)` or `UseInMemory()` in the `AddSagaInfrastructure` configuration.

Under the hood, these options register the appropriate `ISagaStore` implementation: `EfCoreSagaStore` (with `SagaDbContext` and migrations) or `InMemorySagaStore`. The `ISagaStore` interface abstracts operations to save, update, and retrieve saga state.

### Observability (OpenTelemetry Tracing)
**OrchestratR** is instrumented for tracing using .NET's `System.Diagnostics` and **OpenTelemetry**. It defines an `ActivitySource` named **"OrchestratR"** to produce trace events for saga execution. Each saga run and step execution can be emitted as activities/spans.

The code integrates with **OpenTelemetry** via a telemetry abstraction `ISagaTelemetry` and `SagaDiagnostics`. By default, **OrchestratR** provides a `SagaTelemetry` implementation that uses the **"OrchestratR"** `ActivitySource` to record events like saga started, saga completed/failed, steps started/completed, etc.

You can attach **OpenTelemetry** listeners to this `ActivitySource` to collect distributed tracing information for your sagas. This means if your application is configured for **OpenTelemetry**, **OrchestratR** sagas will automatically participate in traces, making it easier to observe saga flows across services.

## Configuration & Options

**OrchestratR** provides various options to configure saga execution behavior, retry policies, and infrastructure:

### Retry and Timeout Policies (Polly Integration)
You can configure automatic retries and timeouts for each saga step using **Polly** policies. **OrchestratR's** `SagaBuilder` offers a fluent API to set these on a per-step basis.

When adding a step via `WithStep<TStep>(stepConfig)`, you receive a `StepBuilder<TContext,TStep>` that has methods like `.WithRetry(int maxRetries)` and `.WithTimeout(TimeSpan timeout)` to specify **Polly** retry count and timeout for that step.

```csharp
services.AddSaga<MySagaContext>(saga => saga
    .WithStep<FirstStep>(step => step.WithRetry(3))   // 3 retries on FirstStep
    .WithStep<SecondStep>(step => step.WithTimeout(TimeSpan.FromSeconds(10)))
    .WithRecovery());
```

Internally, **OrchestratR** will wrap the step's execution in a **Polly** policy that applies these settings. For example, calling `.WithRetry(3).WithTimeout(TimeSpan.FromSeconds(30))` on a step will create a combined **Polly** policy that retries the step up to 3 times on exception and applies a 30-second timeout per attempt.

### WithRecovery (Saga Recovery Service)
Enabling recovery will start a background service that periodically checks for sagas that were left in a waiting state and resumes them if conditions are met. In **OrchestratR**, you enable this by calling `WithRecovery()` on the `SagaBuilder` when registering the saga.

During saga registration finalization, if recovery is enabled, **OrchestratR** registers a hosted service called `SagaRecoveryService` (which implements `IHostedService`). The `SagaRecoveryService` runs in the background (as a singleton) and will scan the saga store for sagas that are in a **InProgress** (this is the case when application shuts down during forward execution) , **Compensating** or possibly **Failed** status and attempt to resume them.

For example, if your application crashes while a saga was mid-flight, on restart the recovery service can pick up any unfinished sagas and resume processing (this ensures no saga is left hanging indefinitely).

### SagaInfrastructureOptions (EF Core, In-Memory, Migrations)
The `AddSagaInfrastructure(Action<SagaInfrastructureOptions> configure)` extension is used to configure how and where saga state is stored. The `SagaInfrastructureOptions` class provides a fluent API:

#### UseEfCore(...)
Call `UseEfCore(...)` to use Entity Framework Core for persistence. You pass in an action to configure the `DbContextOptionsBuilder` (e.g., specify your database provider and connection string). This will mark EF Core usage as enabled and store the provided options for later.

Under the hood, when you call `AddSagaInfrastructure`, if EF Core is enabled, **OrchestratR** will register the `SagaDbContext` in the DI container (using `AddDbContextPool` for efficiency) and register `EfCoreSagaStore` as the `ISagaStore` implementation.

#### UseInMemory()
Call `UseInMemory()` to use the in-memory saga store. This sets a flag indicating the in-memory store should be used, and `AddSagaInfrastructure` will register a singleton `InMemorySagaStore` as `ISagaStore`.

#### SkipMigrationApplication()
If using EF Core, by default **OrchestratR** will automatically apply any pending migrations on startup via a hosted service (`SagaMigrationService`). To disable this (for example, if you plan to apply migrations manually or you are using your own migration strategy), call `SkipMigrationApplication()`.

This sets an internal flag `SkipMigrations = true` which prevents the migration hosted service from being added. You would typically use this in production if your deployment process runs EF Core migrations separately, or if you want to control migration timing.

### Telemetry Configuration (ISagaTelemetry)
**OrchestratR's** default telemetry will emit tracing events as described in Core Concepts. However, you can customize or disable telemetry by providing your own implementation of `ISagaTelemetry`.

The `ISagaTelemetry` interface (defined in `OrchestratR.Tracing`) includes methods that the orchestrator calls at key points (saga started, step started/completed, saga completed/failed, etc.). By default, `SagaTelemetry` (the built-in implementation) uses an `ActivitySource` for **OpenTelemetry**.

If you prefer to use a different monitoring approach (or none at all), you can register a replacement `ISagaTelemetry` in the DI container. Because **OrchestratR** resolves telemetry via DI, your custom telemetry will be used instead of the default if provided.

## API Reference

Below is a summary of key APIs and usage patterns in **OrchestratR**:

### AddSaga<TContext>(Action<SagaBuilder<TContext>> configure)
Extension method to register a saga of type `TContext` with the DI container. Call this in `IServiceCollection` registration. The configure callback receives a `SagaBuilder<TContext>` to define the saga's steps and behaviors.

For each saga step, call `WithStep<TStep>(stepOptions)`. You can chain multiple `WithStep` calls to add steps in order:

```csharp
services.AddSaga<MySagaContext>(saga => saga
    .WithStep<FirstStep>(step => step.WithRetry(3))   // 3 retries on FirstStep
    .WithStep<SecondStep>(step => step.WithTimeout(TimeSpan.FromSeconds(10)))
    .WithRecovery());
```

Each `WithStep<TStep>` can take an optional lambda to configure that step's retry/timeout as shown. If no lambda is provided, the step is added with default behavior (no retries, no timeout).

### WithRecovery()
`SagaBuilder` method to enable saga recovery (background resume). This method does not take parameters. It simply flags that the `SagaRecoveryService` should be registered.

### AddSagaInfrastructure(Action<SagaInfrastructureOptions> configure)
Extension method to configure saga persistence and run final initialization. You can configure one of the following:

- **Use EF Core with a specific provider**: `options.UseEfCore(dbOptions => { ... })` – inside, call something like `dbOptions.UseSqlServer("MyConnectionString", x => x.MigrationsAssembly(SagaInfrastructureOptions.GetMigrationsAssembly()))` to point to your database.
- **Use In-Memory store**: `options.UseInMemory()` – no configuration needed, this simply keeps saga state in memory.
- **Skip migrations**: `options.SkipMigrationApplication()` – if using EF, prevents **OrchestratR** from auto-applying migrations at startup.

### StartAsync(TContext context, CancellationToken = default)
Method on `ISagaOrchestrator<TContext>` to start a new saga instance. You pass a new instance of your saga context (populated with initial data). This returns a `Guid` which is the Saga's unique ID.

The orchestrator will immediately execute the saga's first step and continue sequentially until a step returns `Wait` or the saga finishes. If a step returns `Wait`, the saga's state is saved (persisted) and `StartAsync` returns control to you (with the saga ID). The saga is now in a **Waiting** state.

### ResumeAsync(Guid sagaId, Action<TContext>? contextUpdater = null, CancellationToken = default)
Method on `ISagaOrchestrator<TContext>` to resume a paused saga. You call this using the saga ID obtained from `StartAsync`. Optionally, you can provide a lambda `contextUpdater` to modify the saga's context just before resuming.

This is useful to inject results from external events – for example, marking a payment as completed, or adding data received from another service:

```csharp
await orchestrator.ResumeAsync(sagaId, ctx => ctx.PaymentProcessed = true);
```

Internally, `ResumeAsync` will retrieve the saga's current state (context and step position) from the `ISagaStore`. If a context update lambda is provided, it applies those changes to the context. Then the orchestrator continues executing the next pending step in the saga.

## Usage Patterns

**OrchestratR** is flexible and can be used for various saga patterns:

### HTTP Request Saga
You can coordinate multiple HTTP service calls as steps in a saga. For instance, **Step1** calls Service A, **Step2** calls Service B. If **Step2** fails, **Step1's** `CompensateAsync` could undo the call to Service A. You'd likely use `SagaStepStatus.Continue` for each step (unless you need an async response). The saga ensures each call either all succeed or compensations run.

### Long-Running Transaction (with external events)
If one step involves waiting for an external event (user input, third-party callback, time delay), have that step return `SagaStepStatus.Wait`. The saga will pause. When the event occurs, call `ResumeAsync` to continue. This allows the saga to span hours or days, yet still maintain state in between.

### Message Broker Workflow
You can integrate **OrchestratR** with messaging systems by starting a saga when a message is received, and pausing for other messages. For example, a saga could publish a command to a queue in **Step1** and then wait; another service processes it and, upon completion, you resume the saga (perhaps from a different process) to do **Step2**.

### Compensation Transactions
Design your `CompensateAsync` methods to undo side effects. **OrchestratR** will call them if a saga step throws an exception that isn't handled by a retry. For example, if **Step3** fails irrecoverably, the orchestrator can call **Step2.CompensateAsync**, then **Step1.CompensateAsync** to roll back what those steps did (this is manual logic you provide – e.g., cancel an order, refund a payment, restock inventory).

### OpenTelemetry Tracing
With tracing enabled, you can track each saga and step in systems like **Jaeger**, **Zipkin**, **Dynatrace**, or **Application Insights**. The trace will show spans for "Saga Start (ID=...)", "Step1 Execute", etc., with timing and status. This is extremely helpful for debugging complex orchestrations in production.

## Advanced Topics

### Custom SagaStore Implementations
**OrchestratR's** design allows swapping out the persistence layer by implementing `OrchestratR.Core.ISagaStore`. If you prefer to use a storage mechanism other than the built-in SQL (EF Core) or in-memory store – for example, a **MongoDB** or **Redis**-based store – you can write your own `ISagaStore` with methods to insert, update, find, and list saga entities.

To use it, register your `ISagaStore` in the DI container after calling `AddSagaInfrastructure`, so that it overrides the default registration. This extensibility means **OrchestratR** isn't tied to a specific database. This process should be standartized an simplified in library's future versions.

### Partial Updates vs Whole Context
Currently, **OrchestratR** updates the entire saga context as a single object in the store. This simplifies consistency – the context is serialized (to JSON for EF Core) and saved atomically. However, it means that if you have a very large context but only one field changed, the whole context still gets written. This is not the issue with ef-core based saga store, since **Entity Framework** has enough built-in optimizations not to perform whole updates when only partial one is needed, but when implementing custom saga store, this should be taken into consideration.

In practice, this is rarely an issue for typical saga contexts (which are usually not huge), but it's something to be aware of. 

### Cooperative Cancellation vs Business Exceptions
**OrchestratR** distinguishes between a saga being cancelled and a saga encountering an error. All `ExecuteAsync` and `CompensateAsync` methods accept a `CancellationToken`. If a token is signaled (e.g., app shutdown or an explicit cancellation), the saga will attempt to stop gracefully. It will stop execution but keep saga in InProgress state, so that it can be resumed.

On the other hand, if your step code throws an exception (not related to cancellation), that is considered a business or system exception – **OrchestratR** will catch it, log it via telemetry, and treat the saga as **Failed**, triggering compensations.


### Custom Telemetry (ISagaTelemetry)
The default telemetry covers distributed tracing, but you may want to integrate sagas with other monitoring tools. By implementing `ISagaTelemetry`, you gain fine control over what happens when sagas and steps start/stop.

For example, you could emit custom logs or metrics: increment a **Prometheus** counter for "saga_failures_total" in `OnSagaCompleted` if Status==Failed, or send an email alert when a particular saga fails.

To do this, implement the interface's methods (they receive the saga context, saga ID, step info, and result/status) and register your implementation in the DI container.

## Testing

### Testing Sagas End-to-End
For a full integration test of a saga, you can simulate a saga run using the orchestrator. For example, set up a `ServiceProvider` with **OrchestratR** configured (in-memory), then:

1. Resolve `ISagaOrchestrator<YourContext>` and call `StartAsync` with a test context.
2. If a saga is supposed to pause, verify that `StartAsync` returned a saga ID and did not complete all steps.
3. Simulate whatever external action is needed (perhaps by directly setting some context state in the store or via the new resume lambda).
4. Call `ResumeAsync(sagaId, ...)` and then verify the saga completes.

You can query the `ISagaStore` (also from DI) to get the `SagaEntity` and verify its Status is **Completed** and that all expected flags in the context are true, etc.

## Examples & Samples

For a concrete example, refer to the **DemoAPI** project in the **OrchestratR** repository (in the test branch). The **DemoAPI** contains an **OrderSaga** implementation with multiple steps:

- **ReserveInventoryStep** – reserves product inventory for an order.
- **ProcessPaymentStep** – (optionally) process payment for the order.
- **ShipOrderStep** – ships the order.

The saga is configured to wait for payment confirmation between steps. The **OrderController** in **DemoAPI** shows how an HTTP endpoint triggers `StartSaga` (which runs ReserveInventory then pauses), and another endpoint triggers Continue (which resumes the saga after marking payment as done).

This is a great blueprint for orchestrating a complex business transaction across multiple services with the saga pattern. If you have the repository, check the `DemoAPI/OrderSaga` folder for the context and step implementations, and `DemoAPI/Program.cs` (or `Startup.cs`) for how the saga is wired up.

The concepts from the demo can be applied to many scenarios: booking travel (reserve flight, reserve hotel, etc., with compensation if one fails), multi-step onboarding processes, etc.

---

> **Note**: As **OrchestratR** is in active development, the API may evolve. The above documentation is based on the mid-2025 state of the systen and covers recently added features like telemetry and resume lambdas. Be sure to consult the repository's README or releases for any updates.
