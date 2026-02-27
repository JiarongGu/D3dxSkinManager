# TaskQueue Architecture

## Overview

The TaskQueue system provides a **node-based, event-driven workflow engine** for handling long-running operations. It supports individual tasks and complex workflows (**TaskChains**) with conditional routing, branching logic, and real-time progress reporting.

## Core Concepts

### 1. **Task** - Individual Unit of Work
A task is a single operation (e.g., compress folder, import mod, validate archive).

### 2. **TaskChain** - Workflow of Connected Tasks
A TaskChain is a workflow composed of multiple **TaskChainNodes** connected by **routing rules**.

### 3. **TaskChainNode** - Single Step in Workflow
Each node in a chain represents one task with:
- **Input mapping** (where data comes from)
- **Output mapping** (where data goes)
- **Routing rules** (conditional next-node selection)

### 4. **Routing Rules** - Conditional Branching
Rules that determine the next node based on conditions (output values, status, user input).

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                          Frontend                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │ Add Task     │  │ Progress Bar │  │ Chain Status │         │
│  │ Button       │  │ (Live)       │  │ Viewer       │         │
│  └──────┬───────┘  └──────▲───────┘  └──────▲───────┘         │
└─────────┼──────────────────┼────────────────┼──────────────────┘
          │IPC: ADD_TASK     │Subscribe       │IPC: GET_CHAIN
          ▼                  │TASK_QUEUE/*    ▼
┌─────────────────────────────────────────────────────────────────┐
│                 TaskQueueFacade (IPC Router)                    │
│  Task Operations:                                               │
│  - ADD_TASK           → Add individual task or start chain     │
│  - PROCESS_NEXT       → Process next pending task              │
│  - CANCEL_TASK        → Cancel running task                    │
│  - REMOVE_TASK        → Remove task from queue                 │
│                                                                 │
│  Query Operations:                                              │
│  - GET_ALL_TASKS      → Get all tasks in queue                 │
│  - GET_TASK           → Get specific task by ID                │
│  - CLEAR_COMPLETED    → Remove completed tasks                 │
│                                                                 │
│  Chain Operations:                                              │
│  - CONTINUE_CHAIN     → Continue paused chain with user input  │
│  - GET_ALL_CHAINS     → Get all task chains                    │
│  - GET_CHAIN_CONFIG   → Get chain configuration                │
│                                                                 │
│  Metadata Operations:                                           │
│  - GET_TASK_METADATA  → Get metadata for task type             │
│  - GET_ALL_TASK_METADATA → Get all task type metadata          │
└────────────────────────────┬────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    TaskQueueService                             │
│  ┌───────────────────────────────────────────────────────┐     │
│  │ Task Queue (ConcurrentQueue)                          │     │
│  │ [ Task1 | Task2 | Task3 | ... ]                      │     │
│  └───────────────────────────────────────────────────────┘     │
│                                                                 │
│  Chain Management:                                              │
│  ├─ TaskChainRepository (stores chains)                        │
│  ├─ Routing Engine (evaluates conditions)                      │
│  ├─ Context Manager (shared data across nodes)                 │
│  └─ Event Emitter (chain progress, pauses)                     │
└────────────────────────────┬────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│              Task Processors (ITaskProcessor)                   │
│                                                                 │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────┐ │
│  │ ModImport        │  │ CompressFolder   │  │ ExtractZip   │ │
│  │ Processor        │  │ Processor        │  │ Processor    │ │
│  └──────────────────┘  └──────────────────┘  └──────────────┘ │
│                                                                 │
│  Each processor:                                                │
│  ├─ Implements ITaskProcessor<TInput, TOutput>                 │
│  ├─ Reports progress via callback                              │
│  ├─ Returns typed output (mapped to chain context)             │
│  └─ Can be used standalone or within a chain                   │
└─────────────────────────────────────────────────────────────────┘
```

## Core Models

### TaskChainInfo

Represents a workflow instance with its current state.

```csharp
public class TaskChainInfo
{
    public string Id { get; init; }                    // Unique chain ID
    public string? ChainType { get; init; }            // Chain template type
    public string? ChainConfiguration { get; set; }    // JSON: TaskChainConfiguration
    public TaskChainStatus Status { get; set; }        // Pending, Processing, Completed, Failed
    public string? Context { get; set; }               // JSON: Shared data across nodes
    public string? Input { get; set; }                 // JSON: Initial input
    public string? Output { get; set; }                // JSON: Final output
    public string? ErrorMessage { get; set; }          // Error if failed
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
```

### TaskChainConfiguration

Defines the workflow structure.

```csharp
public class TaskChainConfiguration
{
    // All nodes in the workflow (keyed by NodeId)
    public Dictionary<string, TaskChainNode> Nodes { get; init; }

    // Starting node ID
    public string StartNodeId { get; init; }
}
```

### TaskChainNode

Defines a single step in the workflow.

```csharp
public class TaskChainNode
{
    public string NodeId { get; init; }              // e.g., "compress_folder"
    public string TaskType { get; init; }            // e.g., "compress_folder" (from TaskTypes)

    // Input Mapping: Where to get input data
    // Format: { "paramName": "source.path.to.data" }
    public Dictionary<string, string> InputMapping { get; init; }

    // Output Mapping: Where to store output data
    // Format: { "outputField": "contextKey" }
    public Dictionary<string, string> OutputMapping { get; init; }

    // Routing Rules: Conditional next-node selection
    public List<NodeRoutingRule> RoutingRules { get; init; }

    // Default next node if no rules match
    public string? DefaultNextNode { get; init; }

    // Custom metadata for this node
    public Dictionary<string, object> Metadata { get; init; }
}
```

### NodeRoutingRule

Defines conditional routing logic.

```csharp
public class NodeRoutingRule
{
    public string? Name { get; init; }                // Rule description
    public RoutingCondition Condition { get; init; }  // When to route
    public string NextNodeId { get; init; }           // Where to route
    public int Priority { get; init; } = 0;           // Rule priority (higher = first)
}
```

### RoutingCondition

Powerful condition evaluation system.

```csharp
public class RoutingCondition
{
    public ConditionType Type { get; init; }

    // Field to evaluate (supports dot notation)
    public string? Field { get; init; }

    // Comparison operator
    public ComparisonOperator Operator { get; init; } = ComparisonOperator.Equals;

    // Value to compare against
    public object? Value { get; init; }

    // For composite conditions (And, Or)
    public List<RoutingCondition>? SubConditions { get; init; }
}
```

#### ConditionType Options

```csharp
public enum ConditionType
{
    TaskStatus,        // Check task completion status
    OutputField,       // Evaluate field from task output
    SharedDataField,   // Evaluate field from chain context
    HasError,          // Check if error occurred
    UserInput,         // Check user-provided input
    And,               // All sub-conditions must be true
    Or,                // Any sub-condition must be true
    Not,               // Negate sub-condition
    Always,            // Always true (unconditional)
    Custom             // Custom evaluator function
}
```

#### ComparisonOperator Options

```csharp
public enum ComparisonOperator
{
    Equals, NotEquals,
    GreaterThan, GreaterThanOrEqual,
    LessThan, LessThanOrEqual,
    Contains, NotContains,
    StartsWith, EndsWith,
    Matches,    // Regex
    In, NotIn,  // Value in list
    IsNull, IsNotNull,
    IsEmpty, IsNotEmpty
}
```

## Input/Output Mapping

### Input Mapping Sources

```csharp
// Literal value
"paramName": "literal:Some Value"

// From chain initial input
"paramName": "input.fieldName"

// From previous node output
"paramName": "nodeId.output.fieldName"

// From shared context
"paramName": "contextKey"

// From user input (when chain paused)
"paramName": "user_fieldName"

// Array indexing
"paramName": "nodeId.output.items[0]"

// Nested paths (dot notation)
"paramName": "nodeId.output.data.nested.field"
```

### Output Mapping

```csharp
// Store output field to context
"outputFieldName": "contextKeyName"

// Example: Store temp path for later nodes
"tempArchivePath": "tempArchivePath"

// Example: Store mod ID for final result
"modId": "finalModId"
```

## Predefined Chains

### 1. Folder Import Chain (Interactive)

**Visual Flow:**
```
┌─────────────────────────────────────────────────────────┐
│ Folder Import Chain                                     │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  START: compress_folder                                 │
│  ├─ Input: folderPath (from chain input)              │
│  ├─ Output: tempArchivePath, folderName, fileCount    │
│  └─ Routing:                                           │
│      ├─ IF TaskStatus = "AwaitingConfirmation"        │
│      │   → import_from_temp                           │
│      └─ ELSE → End (cancelled)                        │
│                                                         │
│  NODE: import_from_temp                                 │
│  ├─ Input:                                             │
│  │   ├─ tempArchivePath (from compress_folder output) │
│  │   ├─ name, author, description (from user input)   │
│  │   └─ category, tags (from user input)              │
│  ├─ Output: modId, importPath                         │
│  └─ Routing: None (end of chain)                      │
└─────────────────────────────────────────────────────────┘
```

**Configuration Example:**
```csharp
["compress_folder"] = new TaskChainNode
{
    NodeId = "compress_folder",
    TaskType = TaskTypes.COMPRESS_FOLDER,
    InputMapping = new() { ["folderPath"] = "input.folderPath" },
    OutputMapping = new()
    {
        ["tempArchivePath"] = "tempArchivePath",
        ["folderName"] = "folderName"
    },
    RoutingRules = new()
    {
        new NodeRoutingRule
        {
            Name = "User confirms metadata",
            Condition = new RoutingCondition
            {
                Type = ConditionType.TaskStatus,
                Value = "AwaitingConfirmation"
            },
            NextNodeId = "import_from_temp"
        }
    },
    DefaultNextNode = null // End if cancelled
}
```

### 2. Validation Chain (Conditional Branching)

**Visual Flow:**
```
┌──────────────────────────────────────────────────────────┐
│ Validation Chain                                         │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  START: validate_archive                                 │
│  ├─ Input: archivePath                                  │
│  ├─ Output: isValid, errors, warnings                   │
│  └─ Routing (Priority-based):                           │
│      ├─ [P10] IF isValid = false                        │
│      │        → handle_validation_error                 │
│      ├─ [P5]  IF isValid = true AND warnings not empty  │
│      │        → import_with_warnings                    │
│      └─ [P1]  IF isValid = true                         │
│               → import_archive                          │
│                                                          │
│  NODE: handle_validation_error                           │
│  └─ Routing: None (end chain on error)                  │
│                                                          │
│  NODE: import_with_warnings                              │
│  ├─ Metadata: { logWarnings: true }                     │
│  └─ Routing: None (end)                                 │
│                                                          │
│  NODE: import_archive                                    │
│  └─ Routing: None (end)                                 │
└──────────────────────────────────────────────────────────┘
```

**Features:**
- Priority-based routing
- Composite AND/OR conditions
- Error handling nodes
- Node metadata

### 3. Batch Processing Chain

**Visual Flow:**
```
┌───────────────────────────────────────────────────────────┐
│ Batch Processing Chain                                    │
├───────────────────────────────────────────────────────────┤
│                                                           │
│  START: get_batch_items                                   │
│  ├─ Output: items, itemCount                             │
│  └─ Routing:                                             │
│      ├─ IF itemCount = 0 → no_items_found               │
│      ├─ IF itemCount = 1 → process_single_item          │
│      └─ IF itemCount > 1 → process_batch                │
│                                                           │
│  NODE: process_batch                                      │
│  ├─ Output: successCount, failedCount                    │
│  └─ Routing:                                             │
│      └─ IF successCount > 0 → report_success            │
│                                                           │
│  NODE: report_success                                     │
│  └─ Generates final report                               │
└───────────────────────────────────────────────────────────┘
```

## Chain Execution Flow

### 1. Start Chain

```csharp
// Backend - Create and start chain
var chainId = await taskQueueService.AddTaskAsync(
    taskType: TaskTypes.COMPRESS_FOLDER,
    input: new CompressFolderTaskInput { FolderPath = path },
    chainId: Guid.NewGuid().ToString(),
    nodeId: "compress_folder"
);

// Events emitted:
// TASK_QUEUE:CHAIN_STARTED { chainId, chainType }
```

### 2. Node Processing

```
For each node:
  1. Resolve input (from mapping)
  2. Execute task processor
  3. Store output (to context via mapping)
  4. Evaluate routing rules
  5. Determine next node

  If next node exists:
    → Queue next node task
  Else:
    → Mark chain as completed

Events:
  - TASK_QUEUE:CHAIN_NODE_COMPLETED { chainId, nodeId, output }
```

### 3. User Input Pausing

```csharp
// Task processor signals need for user input
return new CompressFolderTaskOutput
{
    Status = "AwaitingConfirmation",
    TempArchivePath = tempPath,
    Metadata = new { CategoryOptions, ObjectOptions }
};

// Events emitted:
// TASK_QUEUE:CHAIN_PAUSED { chainId, nodeId, metadata }

// User provides input via frontend
await ipc.send("TASK_QUEUE", "CONTINUE_CHAIN", {
    chainId: "chain-123",
    nodeId: "compress_folder",
    userInput: {
        name: "My Mod",
        category: "Character"
    }
});

// Chain resumes with next node
// Events: TASK_QUEUE:CHAIN_RESUMED { chainId }
```

### 4. Conditional Routing Example

```csharp
// Node completes with output
output = { isValid: true, warnings: ["Minor issue"] }

// Routing rules evaluated in priority order:
RoutingRules = {
    [P10] IF isValid = false → error_handler        // Skip
    [P5]  IF isValid = true AND warnings.length > 0 // MATCH!
          → import_with_warnings
    [P1]  IF isValid = true → import_archive        // Skip
}

// Next node: import_with_warnings
```

## Event System

### Chain Events

```typescript
// Chain lifecycle
TASK_QUEUE:CHAIN_STARTED         // { chainId, chainType, startNodeId }
TASK_QUEUE:CHAIN_NODE_COMPLETED  // { chainId, nodeId, output }
TASK_QUEUE:CHAIN_PAUSED          // { chainId, nodeId, metadata }
TASK_QUEUE:CHAIN_RESUMED         // { chainId, nodeId }
TASK_QUEUE:CHAIN_COMPLETED       // { chainId, finalOutput }
TASK_QUEUE:CHAIN_FAILED          // { chainId, nodeId, error }

// Task events (per node)
TASK_QUEUE:TASK_STARTED          // { taskId, nodeId, chainId }
TASK_QUEUE:TASK_PROGRESS         // { taskId, progress }
TASK_QUEUE:TASK_COMPLETED        // { taskId, output }
```

## Frontend Integration

### React Hook for Chain Monitoring

```typescript
export function useTaskChain(chainId: string) {
    const [chain, setChain] = useState<TaskChainInfo | null>(null);
    const [currentNode, setCurrentNode] = useState<string | null>(null);
    const [paused, setPaused] = useState(false);
    const [pauseMetadata, setPauseMetadata] = useState<any>(null);

    useEffect(() => {
        // Subscribe to chain events
        const unsubs = [
            ipc.subscribe("TASK_QUEUE", "CHAIN_NODE_COMPLETED", (e) => {
                if (e.payload.chainId === chainId) {
                    setCurrentNode(e.payload.nodeId);
                }
            }),
            ipc.subscribe("TASK_QUEUE", "CHAIN_PAUSED", (e) => {
                if (e.payload.chainId === chainId) {
                    setPaused(true);
                    setPauseMetadata(e.payload.metadata);
                }
            })
        ];

        loadChain();
        return () => unsubs.forEach(u => u());
    }, [chainId]);

    const continueChain = async (userInput: any) => {
        await ipc.send("TASK_QUEUE", "CONTINUE_CHAIN", {
            chainId,
            nodeId: currentNode,
            userInput
        });
        setPaused(false);
    };

    return { chain, currentNode, paused, pauseMetadata, continueChain };
}
```

## Best Practices

### 1. Node Naming
Use descriptive, action-based names:
```csharp
// Good
"compress_folder", "validate_archive", "import_mod"

// Bad
"node1", "step2", "task_a"
```

### 2. Input/Output Mapping Clarity
Be explicit about data sources:
```csharp
// Good - Clear source
InputMapping = new()
{
    ["archivePath"] = "compress_folder.output.tempArchivePath",
    ["modName"] = "user_name"
}

// Bad - Ambiguous
InputMapping = new()
{
    ["archivePath"] = "tempArchivePath" // Where from?
}
```

### 3. Routing Rule Priority
Use priorities for overlapping conditions:
```csharp
RoutingRules = new()
{
    new() { Priority = 100, Condition = ..., NextNodeId = "critical_error" },
    new() { Priority = 50,  Condition = ..., NextNodeId = "warning_path" },
    new() { Priority = 10,  Condition = ..., NextNodeId = "normal_path" }
}
```

## Performance Characteristics

- **Single-Threaded**: One task processes at a time
- **Node-Based**: Workflows as connected nodes
- **Conditional**: Routing based on runtime conditions
- **Event-Driven**: Real-time updates via EventBus
- **Persistent**: Chains stored in repository
- **Resumable**: Can pause and resume with user input

## Related Documentation

- [EVENT_HUB_ARCHITECTURE.md](./EVENT_HUB_ARCHITECTURE.md) - Event system
- [PLUGIN_ARCHITECTURE.md](./PLUGIN_ARCHITECTURE.md) - Plugin integration
- [MODULE_ARCHITECTURE.md](./MODULE_ARCHITECTURE.md) - Module structure
