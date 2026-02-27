# Task Queue Refactoring Design

## Overview
Refactor the TaskQueue system to support a normalized, registry-based task processor architecture with chain configuration and parallel execution support.

---

## Design Goals

1. **Normalized Interface** - All task processors follow the same pattern
2. **Dynamic Registration** - Tasks registered at startup, no hardcoded switches
3. **Chain Configuration** - Declarative chain definitions with parallel support
4. **Type Safety** - Strong typing for inputs, outputs, and shared data
5. **Extensibility** - Easy to add new task types without modifying core code
6. **Correlation Tracking** - CorrelationId + TaskId for progress mapping

---

## Architecture Components

### 1. Task Processor Interface (Enhanced)

```csharp
/// <summary>
/// Normalized interface for all task processors
/// </summary>
public interface ITaskProcessor<TInput, TOutput>
{
    /// <summary>
    /// Process the task asynchronously
    /// </summary>
    Task<TOutput> ProcessAsync(
        TInput input,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validate input before processing
    /// </summary>
    Task<bool> ValidateInputAsync(TInput input);

    /// <summary>
    /// Task type identifier (e.g., "mod_import")
    /// </summary>
    string TaskType { get; }

    /// <summary>
    /// Task metadata for configuration and discovery
    /// </summary>
    TaskProcessorMetadata Metadata { get; }
}
```

### 2. Task Processor Metadata

```csharp
/// <summary>
/// Metadata describing a task processor's capabilities
/// </summary>
public class TaskProcessorMetadata
{
    /// <summary>
    /// Unique task type identifier
    /// </summary>
    public required string TaskType { get; init; }

    /// <summary>
    /// Human-readable display name
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Description of what this task does
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Input model type (for deserialization)
    /// </summary>
    public required Type InputType { get; init; }

    /// <summary>
    /// Output model type (for serialization)
    /// </summary>
    public required Type OutputType { get; init; }

    /// <summary>
    /// Estimated average duration in seconds
    /// </summary>
    public int? EstimatedDurationSeconds { get; init; }

    /// <summary>
    /// Supports cancellation?
    /// </summary>
    public bool SupportsCancellation { get; init; } = true;

    /// <summary>
    /// Can be used in chains?
    /// </summary>
    public bool SupportsChaining { get; init; } = true;

    /// <summary>
    /// Tags for categorization/filtering
    /// </summary>
    public string[] Tags { get; init; } = Array.Empty<string>();
}
```

### 3. Task Registry

```csharp
/// <summary>
/// Central registry for all task processors
/// </summary>
public interface ITaskRegistry
{
    /// <summary>
    /// Register a task processor
    /// </summary>
    void Register<TInput, TOutput>(ITaskProcessor<TInput, TOutput> processor);

    /// <summary>
    /// Get processor by task type
    /// </summary>
    object? GetProcessor(string taskType);

    /// <summary>
    /// Get metadata for a task type
    /// </summary>
    TaskProcessorMetadata? GetMetadata(string taskType);

    /// <summary>
    /// Get all registered task types
    /// </summary>
    IEnumerable<string> GetRegisteredTaskTypes();

    /// <summary>
    /// Get all task metadata
    /// </summary>
    IEnumerable<TaskProcessorMetadata> GetAllMetadata();

    /// <summary>
    /// Check if task type is registered
    /// </summary>
    bool IsRegistered(string taskType);
}

public class TaskRegistry : ITaskRegistry
{
    private readonly ConcurrentDictionary<string, object> _processors = new();
    private readonly ConcurrentDictionary<string, TaskProcessorMetadata> _metadata = new();

    public void Register<TInput, TOutput>(ITaskProcessor<TInput, TOutput> processor)
    {
        var taskType = processor.TaskType;

        if (_processors.ContainsKey(taskType))
        {
            throw new InvalidOperationException($"Task type '{taskType}' is already registered");
        }

        _processors[taskType] = processor;
        _metadata[taskType] = processor.Metadata;
    }

    public object? GetProcessor(string taskType)
    {
        return _processors.TryGetValue(taskType, out var processor) ? processor : null;
    }

    public TaskProcessorMetadata? GetMetadata(string taskType)
    {
        return _metadata.TryGetValue(taskType, out var metadata) ? metadata : null;
    }

    public IEnumerable<string> GetRegisteredTaskTypes() => _processors.Keys;
    public IEnumerable<TaskProcessorMetadata> GetAllMetadata() => _metadata.Values;
    public bool IsRegistered(string taskType) => _processors.ContainsKey(taskType);
}
```

### 4. Task Processor Factory

```csharp
/// <summary>
/// Factory for creating and executing task processors
/// </summary>
public interface ITaskProcessorFactory
{
    /// <summary>
    /// Process a task by type with JSON input
    /// </summary>
    Task<object?> ProcessTaskAsync(
        string taskType,
        string inputJson,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validate task input
    /// </summary>
    Task<bool> ValidateTaskInputAsync(string taskType, string inputJson);

    /// <summary>
    /// Get processor metadata
    /// </summary>
    TaskProcessorMetadata? GetTaskMetadata(string taskType);
}

public class TaskProcessorFactory : ITaskProcessorFactory
{
    private readonly ITaskRegistry _registry;
    private readonly ILogger _logger;

    public TaskProcessorFactory(ITaskRegistry registry, ILogger logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task<object?> ProcessTaskAsync(
        string taskType,
        string inputJson,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        var processor = _registry.GetProcessor(taskType);
        if (processor == null)
        {
            throw new NotSupportedException($"Task type '{taskType}' is not registered");
        }

        var metadata = _registry.GetMetadata(taskType)!;

        // Deserialize input using the registered type
        var input = JsonSerializer.Deserialize(inputJson, metadata.InputType);
        if (input == null)
        {
            throw new InvalidOperationException($"Failed to deserialize input for task '{taskType}'");
        }

        // Use reflection to call ProcessAsync
        var method = processor.GetType().GetMethod("ProcessAsync");
        var task = (Task?)method?.Invoke(processor, new[] { input, progressReporter, cancellationToken });

        if (task == null)
        {
            throw new InvalidOperationException($"Failed to invoke ProcessAsync for task '{taskType}'");
        }

        await task.ConfigureAwait(false);

        // Get result using reflection
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task);
    }

    public async Task<bool> ValidateTaskInputAsync(string taskType, string inputJson)
    {
        var processor = _registry.GetProcessor(taskType);
        if (processor == null) return false;

        var metadata = _registry.GetMetadata(taskType)!;
        var input = JsonSerializer.Deserialize(inputJson, metadata.InputType);
        if (input == null) return false;

        var method = processor.GetType().GetMethod("ValidateInputAsync");
        var task = (Task<bool>?)method?.Invoke(processor, new[] { input });

        return task != null && await task.ConfigureAwait(false);
    }

    public TaskProcessorMetadata? GetTaskMetadata(string taskType)
    {
        return _registry.GetMetadata(taskType);
    }
}
```

### 5. Chain Configuration

```csharp
/// <summary>
/// Defines a multi-phase task chain
/// </summary>
public class ChainConfiguration
{
    /// <summary>
    /// Unique chain identifier (e.g., "folder_import_chain")
    /// </summary>
    public required string ChainId { get; init; }

    /// <summary>
    /// Human-readable name
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Description of the chain
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Ordered list of phases
    /// </summary>
    public required List<ChainPhase> Phases { get; init; }

    /// <summary>
    /// Maximum parallel chains allowed (-1 = unlimited)
    /// </summary>
    public int MaxParallelChains { get; init; } = 1;

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public string[] Tags { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Single phase in a task chain
/// </summary>
public class ChainPhase
{
    /// <summary>
    /// Phase number (1-based)
    /// </summary>
    public required int PhaseNumber { get; init; }

    /// <summary>
    /// Task type to execute
    /// </summary>
    public required string TaskType { get; init; }

    /// <summary>
    /// Pause for user input after this phase?
    /// </summary>
    public bool RequiresUserAction { get; init; } = false;

    /// <summary>
    /// Prompt message for user action
    /// </summary>
    public string? UserActionDescription { get; init; }

    /// <summary>
    /// Input mapper - map previous output to this phase's input
    /// Func<previousOutput, sharedData, userInput, thisPhaseInput>
    /// </summary>
    public Func<object?, Dictionary<string, object>?, Dictionary<string, object>?, object>? InputMapper { get; init; }
}
```

### 6. Chain Registry

```csharp
/// <summary>
/// Registry for task chain configurations
/// </summary>
public interface IChainRegistry
{
    /// <summary>
    /// Register a chain configuration
    /// </summary>
    void RegisterChain(ChainConfiguration config);

    /// <summary>
    /// Get chain configuration by ID
    /// </summary>
    ChainConfiguration? GetChain(string chainId);

    /// <summary>
    /// Get all registered chains
    /// </summary>
    IEnumerable<ChainConfiguration> GetAllChains();

    /// <summary>
    /// Check if chain is registered
    /// </summary>
    bool IsRegistered(string chainId);
}

public class ChainRegistry : IChainRegistry
{
    private readonly ConcurrentDictionary<string, ChainConfiguration> _chains = new();

    public void RegisterChain(ChainConfiguration config)
    {
        if (_chains.ContainsKey(config.ChainId))
        {
            throw new InvalidOperationException($"Chain '{config.ChainId}' is already registered");
        }

        _chains[config.ChainId] = config;
    }

    public ChainConfiguration? GetChain(string chainId)
    {
        return _chains.TryGetValue(chainId, out var config) ? config : null;
    }

    public IEnumerable<ChainConfiguration> GetAllChains() => _chains.Values;
    public bool IsRegistered(string chainId) => _chains.ContainsKey(chainId);
}
```

---

## Implementation Plan

### Phase 1: Core Infrastructure
1. Create `TaskProcessorMetadata.cs`
2. Update `ITaskProcessor<TInput, TOutput>` to include Metadata property
3. Create `ITaskRegistry.cs` and `TaskRegistry.cs`
4. Create `ITaskProcessorFactory.cs` and `TaskProcessorFactory.cs`
5. Create `ChainConfiguration.cs` and `ChainPhase.cs`
6. Create `IChainRegistry.cs` and `ChainRegistry.cs`

### Phase 2: Update Existing Processors
1. Add Metadata property to `ModImportTaskProcessor`
2. Add Metadata property to `CompressFolderTaskProcessor`
3. Add Metadata property to `ImportFromTempTaskProcessor`

### Phase 3: Refactor TaskQueueService
1. Inject `ITaskProcessorFactory` instead of `IServiceProvider`
2. Replace switch statement (lines 208-214) with factory call
3. Remove manual processor retrieval (lines 285-361)
4. Implement complete `ContinueChainAsync` with auto-task-creation

### Phase 4: Refactor TaskQueueFacade
1. Remove task-type switch statement (lines 64-70)
2. Simplify `AddTaskAsync` to use factory pattern
3. Update `ContinueChainAsync` to support chain registry

### Phase 5: Update DI Registration
1. Register `ITaskRegistry` as singleton
2. Register `ITaskProcessorFactory` as singleton
3. Register `IChainRegistry` as singleton
4. Register all processors via registry in `TaskQueueServiceExtensions`

### Phase 6: Define Standard Chains
1. Create "folder_import_chain" configuration
2. Define phase 1: compress_folder
3. Define phase 2: import_from_temp with input mapper
4. Register in startup

### Phase 7: Frontend Integration
1. Add task metadata query endpoint (`GET_TASK_METADATA`)
2. Add chain configuration query endpoint (`GET_CHAIN_CONFIG`)
3. Update TypeScript types to match new models
4. Update UI to use metadata for display names

### Phase 8: Parallel Execution (Future)
1. Add `MaxParallelChains` enforcement
2. Add correlation-based queue management
3. Update semaphore logic for parallel chains

---

## Example Usage

### Register a New Task Processor

```csharp
// Define task processor
public class MyCustomTaskProcessor : ITaskProcessor<MyInput, MyOutput>
{
    public string TaskType => "my_custom_task";

    public TaskProcessorMetadata Metadata => new()
    {
        TaskType = "my_custom_task",
        DisplayName = "My Custom Task",
        Description = "Does something custom",
        InputType = typeof(MyInput),
        OutputType = typeof(MyOutput),
        EstimatedDurationSeconds = 30,
        SupportsCancellation = true,
        SupportsChaining = true,
        Tags = new[] { "custom", "utility" }
    };

    public async Task<MyOutput> ProcessAsync(
        MyInput input,
        IProgressReporter progress,
        CancellationToken ct)
    {
        await progress.ReportProgressAsync(0, "Starting");
        // ... do work ...
        await progress.ReportProgressAsync(100, "Done");
        return new MyOutput { Result = "Success" };
    }

    public Task<bool> ValidateInputAsync(MyInput input)
    {
        return Task.FromResult(input != null);
    }
}

// Register in DI
services.AddSingleton<MyCustomTaskProcessor>();

// Register in task registry (in TaskQueueServiceExtensions)
var registry = serviceProvider.GetRequiredService<ITaskRegistry>();
var processor = serviceProvider.GetRequiredService<MyCustomTaskProcessor>();
registry.Register(processor);
```

### Define a Chain

```csharp
var folderImportChain = new ChainConfiguration
{
    ChainId = "folder_import_chain",
    DisplayName = "Folder Import",
    Description = "Compresses folder and imports as mod with user metadata",
    MaxParallelChains = 3,  // Allow 3 parallel folder imports
    Phases = new List<ChainPhase>
    {
        new()
        {
            PhaseNumber = 1,
            TaskType = "compress_folder",
            RequiresUserAction = true,
            UserActionDescription = "Please provide metadata for the mod import"
        },
        new()
        {
            PhaseNumber = 2,
            TaskType = "import_from_temp",
            RequiresUserAction = false,
            InputMapper = (prevOutput, sharedData, userInput) =>
            {
                var compressOutput = (CompressFolderTaskOutput)prevOutput!;
                return new ImportFromTempTaskInput
                {
                    TempArchivePath = compressOutput.TempArchivePath,
                    Name = userInput?["name"]?.ToString() ?? sharedData?["metadata_name"]?.ToString(),
                    Author = userInput?["author"]?.ToString(),
                    // ... map other fields
                };
            }
        }
    },
    Tags = new[] { "import", "mod" }
};

// Register chain
var chainRegistry = serviceProvider.GetRequiredService<IChainRegistry>();
chainRegistry.RegisterChain(folderImportChain);
```

---

## Benefits

1. **No Hardcoded Switches** - Task types dynamically discovered from registry
2. **Easy Extension** - Add new task by implementing interface and registering
3. **Type Safety** - Metadata includes type info for deserialization
4. **Declarative Chains** - Chain config separates workflow from implementation
5. **Parallel Support** - MaxParallelChains allows concurrent chain execution
6. **Frontend Discovery** - UI can query available tasks/chains and display properly
7. **Correlation Tracking** - CorrelationId groups related tasks, TaskId tracks individual progress

---

## Migration Strategy

### Backward Compatibility
- Keep existing facade methods during transition
- Add deprecation warnings
- Remove after 1-2 releases

### Testing Strategy
1. Unit tests for registry/factory
2. Integration tests for chain execution
3. E2E tests for folder import workflow
4. Performance tests for parallel chains

### Rollout Plan
1. Deploy infrastructure (registry/factory) - non-breaking
2. Migrate existing processors - non-breaking
3. Update facade to use factory - breaking change
4. Update frontend to use metadata - breaking change
5. Add new chains - additive

---

## Future Enhancements

1. **Task Priority** - Add priority field to queue tasks
2. **Retry Logic** - Auto-retry failed tasks with exponential backoff
3. **Timeout Support** - Cancel tasks exceeding duration
4. **Task Dependencies** - DAG-based task execution
5. **Persistent Queue** - Survive app restarts
6. **Task Scheduling** - Cron-like task execution
7. **Webhook Support** - Notify external systems on completion
8. **Task Templates** - Pre-configured common workflows

---

## Files to Create/Modify

### New Files
- `TaskProcessorMetadata.cs`
- `ITaskRegistry.cs` + `TaskRegistry.cs`
- `ITaskProcessorFactory.cs` + `TaskProcessorFactory.cs`
- `ChainConfiguration.cs`
- `IChainRegistry.cs` + `ChainRegistry.cs`
- `ChainDefinitions.cs` (standard chain configs)

### Modified Files
- `ITaskProcessor.cs` (add Metadata property)
- `ModImportTaskProcessor.cs` (add Metadata)
- `CompressFolderTaskProcessor.cs` (add Metadata)
- `ImportFromTempTaskProcessor.cs` (add Metadata)
- `TaskQueueService.cs` (use factory, fix chain continuation)
- `TaskQueueFacade.cs` (remove switches, add metadata endpoints)
- `TaskQueueServiceExtensions.cs` (register new services)
- Frontend TypeScript types

---

This design provides a solid foundation for a scalable, maintainable task processing system that can grow to support many different types of tasks and complex workflows.
