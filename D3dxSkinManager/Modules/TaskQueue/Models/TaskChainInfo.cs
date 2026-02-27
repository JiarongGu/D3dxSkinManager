using System.ComponentModel.DataAnnotations;

namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Main entity for task chains - represents a sequence of related tasks.
/// Simplified for SQLite storage without navigation properties.
/// </summary>
public class TaskChainInfo
{
    /// <summary>
    /// Unique identifier for the task chain
    /// </summary>
    [Key]
    public required string Id { get; init; }

    /// <summary>
    /// Type of chain (e.g., "folder_import_chain", "batch_import_chain")
    /// </summary>
    public string? ChainType { get; init; }

    /// <summary>
    /// Configuration for this chain instance (JSON serialized)
    /// </summary>
    public string? ChainConfiguration { get; set; }

    /// <summary>
    /// Current status of the chain
    /// </summary>
    public TaskChainStatus Status { get; set; }

    /// <summary>
    /// Context data for chain execution (JSON serialized)
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Initial input data for the chain (JSON serialized)
    /// </summary>
    public string? Input { get; set; }

    /// <summary>
    /// Final output data when chain completes (JSON serialized)
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Error message if chain failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the chain was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the chain started processing
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the chain completed (success or failure)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Status of a task chain
/// </summary>
public enum TaskChainStatus
{
    /// <summary>
    /// Chain is waiting to start
    /// </summary>
    Pending,

    /// <summary>
    /// Chain is currently processing tasks
    /// </summary>
    Processing,

    /// <summary>
    /// All tasks in chain completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Chain failed with error
    /// </summary>
    Failed,

    /// <summary>
    /// Chain was cancelled by user
    /// </summary>
    Cancelled
}

/// <summary>
/// Configuration for a specific task chain instance
/// </summary>
public class TaskChainConfiguration
{
    /// <summary>
    /// Nodes in this chain (keyed by NodeId)
    /// </summary>
    public required Dictionary<string, TaskChainNode> Nodes { get; init; }

    /// <summary>
    /// The starting node ID
    /// </summary>
    public required string StartNodeId { get; init; }
}

/// <summary>
/// A node in a task chain workflow
/// </summary>
public class TaskChainNode
{
    /// <summary>
    /// Unique identifier for this node
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// Task type to execute (from TaskNames)
    /// </summary>
    public required string TaskType { get; init; }

    /// <summary>
    /// Input mapping for this node (maps from previous task outputs or shared data)
    /// </summary>
    public Dictionary<string, string> InputMapping { get; init; } = new();

    /// <summary>
    /// Output mapping for this node (maps to shared data keys)
    /// </summary>
    public Dictionary<string, string> OutputMapping { get; init; } = new();

    /// <summary>
    /// Routing rules for determining the next node based on conditions
    /// Evaluated in order - first matching condition wins
    /// </summary>
    public List<NodeRoutingRule> RoutingRules { get; init; } = new();

    /// <summary>
    /// Default next node if no routing rules match
    /// </summary>
    public string? DefaultNextNode { get; init; }

    /// <summary>
    /// Optional metadata for this node (can be used by TaskProcessor for decision making)
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Routing rule that determines the next node based on conditions
/// </summary>
public class NodeRoutingRule
{
    /// <summary>
    /// Name/description of this rule (for debugging/logging)
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The condition to evaluate
    /// </summary>
    public required RoutingCondition Condition { get; init; }

    /// <summary>
    /// The node to route to if the condition is true
    /// </summary>
    public required string NextNodeId { get; init; }

    /// <summary>
    /// Optional priority (higher number = higher priority)
    /// Rules with same priority are evaluated in order
    /// </summary>
    public int Priority { get; init; } = 0;
}

/// <summary>
/// Represents a condition that can be evaluated for routing decisions
/// </summary>
public class RoutingCondition
{
    /// <summary>
    /// Type of condition evaluation
    /// </summary>
    public required ConditionType Type { get; init; }

    /// <summary>
    /// Field/property to evaluate (e.g., "status", "output.fileCount", "metadata.requiresValidation")
    /// Supports dot notation for nested properties
    /// </summary>
    public string? Field { get; init; }

    /// <summary>
    /// Operator for comparison
    /// </summary>
    public ComparisonOperator Operator { get; init; } = ComparisonOperator.Equals;

    /// <summary>
    /// Value to compare against (can be string, number, boolean, etc.)
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// For composite conditions (And, Or)
    /// </summary>
    public List<RoutingCondition>? SubConditions { get; init; }

    /// <summary>
    /// For custom condition evaluation (e.g., complex business logic)
    /// This would be a key that maps to a registered condition evaluator
    /// </summary>
    public string? CustomEvaluator { get; init; }
}

/// <summary>
/// Types of condition evaluation
/// </summary>
public enum ConditionType
{
    /// <summary>
    /// Check task completion status
    /// </summary>
    TaskStatus,

    /// <summary>
    /// Evaluate a field from task output
    /// </summary>
    OutputField,

    /// <summary>
    /// Evaluate a field from shared data/context
    /// </summary>
    SharedDataField,

    /// <summary>
    /// Check if an error occurred
    /// </summary>
    HasError,

    /// <summary>
    /// Check if user provided specific input
    /// </summary>
    UserInput,

    /// <summary>
    /// All sub-conditions must be true
    /// </summary>
    And,

    /// <summary>
    /// Any sub-condition must be true
    /// </summary>
    Or,

    /// <summary>
    /// Negate the sub-condition
    /// </summary>
    Not,

    /// <summary>
    /// Always true (unconditional routing)
    /// </summary>
    Always,

    /// <summary>
    /// Custom evaluator function
    /// </summary>
    Custom
}

/// <summary>
/// Comparison operators for condition evaluation
/// </summary>
public enum ComparisonOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    Matches, // Regex match
    In,      // Value is in a list
    NotIn,   // Value is not in a list
    IsNull,
    IsNotNull,
    IsEmpty,
    IsNotEmpty
}