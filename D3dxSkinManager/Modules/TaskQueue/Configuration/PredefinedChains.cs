using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Configuration;

/// <summary>
/// Predefined chain configurations for common workflows
/// </summary>
public static class PredefinedChains
{
    /// <summary>
    /// Interactive folder import chain with user metadata input
    /// </summary>
    public static TaskChainConfiguration FolderImportChain => new()
    {
        StartNodeId = "compress_folder",
        Nodes = new Dictionary<string, TaskChainNode>
        {
            ["compress_folder"] = new TaskChainNode
            {
                NodeId = "compress_folder",
                TaskType = "compress_folder",
                InputMapping = new Dictionary<string, string>
                {
                    ["folderPath"] = "input.folderPath"
                },
                OutputMapping = new Dictionary<string, string>
                {
                    ["tempArchivePath"] = "tempArchivePath",
                    ["folderName"] = "folderName",
                    ["fileCount"] = "fileCount"
                },
                RoutingRules = new List<NodeRoutingRule>
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
                DefaultNextNode = null // End chain if not awaiting confirmation
            },
            ["import_from_temp"] = new TaskChainNode
            {
                NodeId = "import_from_temp",
                TaskType = "import_from_temp",
                InputMapping = new Dictionary<string, string>
                {
                    ["tempArchivePath"] = "compress_folder.output.tempArchivePath",
                    ["name"] = "user_name",
                    ["author"] = "user_author",
                    ["description"] = "user_description",
                    ["category"] = "user_category",
                    ["tags"] = "user_tags"
                },
                OutputMapping = new Dictionary<string, string>
                {
                    ["modId"] = "modId",
                    ["importPath"] = "importPath"
                },
                RoutingRules = new List<NodeRoutingRule>(),
                DefaultNextNode = null // End of chain
            }
        }
    };

    /// <summary>
    /// Quick folder import with automatic metadata extraction
    /// </summary>
    public static TaskChainConfiguration QuickFolderImportChain => new()
    {
        StartNodeId = "compress_folder",
        Nodes = new Dictionary<string, TaskChainNode>
        {
            ["compress_folder"] = new TaskChainNode
            {
                NodeId = "compress_folder",
                TaskType = "compress_folder",
                InputMapping = new Dictionary<string, string>
                {
                    ["folderPath"] = "input.folderPath"
                },
                OutputMapping = new Dictionary<string, string>
                {
                    ["tempArchivePath"] = "tempArchivePath",
                    ["folderName"] = "folderName"
                },
                Metadata = new Dictionary<string, object>
                {
                    ["skipUserConfirmation"] = true
                },
                RoutingRules = new List<NodeRoutingRule>
                {
                    new NodeRoutingRule
                    {
                        Name = "Auto-continue to import",
                        Condition = new RoutingCondition
                        {
                            Type = ConditionType.Always
                        },
                        NextNodeId = "auto_import"
                    }
                },
                DefaultNextNode = "auto_import"
            },
            ["auto_import"] = new TaskChainNode
            {
                NodeId = "auto_import",
                TaskType = "import_from_temp",
                InputMapping = new Dictionary<string, string>
                {
                    ["tempArchivePath"] = "compress_folder.output.tempArchivePath",
                    ["name"] = "compress_folder.output.folderName", // Use folder name as default
                    ["author"] = "default.author",
                    ["description"] = "default.description"
                },
                OutputMapping = new Dictionary<string, string>
                {
                    ["modId"] = "modId",
                    ["importPath"] = "importPath"
                },
                RoutingRules = new List<NodeRoutingRule>(),
                DefaultNextNode = null
            }
        }
    };

    /// <summary>
    /// Archive validation chain with error handling
    /// </summary>
    public static TaskChainConfiguration ValidatedImportChain => new()
    {
        StartNodeId = "validate_archive",
        Nodes = new Dictionary<string, TaskChainNode>
        {
            ["validate_archive"] = new TaskChainNode
            {
                NodeId = "validate_archive",
                TaskType = "validate_archive",
                InputMapping = new Dictionary<string, string>
                {
                    ["archivePath"] = "input.archivePath"
                },
                OutputMapping = new Dictionary<string, string>
                {
                    ["isValid"] = "isValid",
                    ["errors"] = "errors",
                    ["warnings"] = "warnings"
                },
                RoutingRules = new List<NodeRoutingRule>
                {
                    new NodeRoutingRule
                    {
                        Name = "Validation failed",
                        Priority = 10,
                        Condition = new RoutingCondition
                        {
                            Type = ConditionType.OutputField,
                            Field = "isValid",
                            Operator = ComparisonOperator.Equals,
                            Value = false
                        },
                        NextNodeId = "handle_validation_error"
                    },
                    new NodeRoutingRule
                    {
                        Name = "Has warnings but valid",
                        Priority = 5,
                        Condition = new RoutingCondition
                        {
                            Type = ConditionType.And,
                            SubConditions = new List<RoutingCondition>
                            {
                                new RoutingCondition
                                {
                                    Type = ConditionType.OutputField,
                                    Field = "isValid",
                                    Operator = ComparisonOperator.Equals,
                                    Value = true
                                },
                                new RoutingCondition
                                {
                                    Type = ConditionType.OutputField,
                                    Field = "warnings",
                                    Operator = ComparisonOperator.IsNotEmpty
                                }
                            }
                        },
                        NextNodeId = "import_with_warnings"
                    },
                    new NodeRoutingRule
                    {
                        Name = "Clean validation",
                        Priority = 1,
                        Condition = new RoutingCondition
                        {
                            Type = ConditionType.OutputField,
                            Field = "isValid",
                            Operator = ComparisonOperator.Equals,
                            Value = true
                        },
                        NextNodeId = "import_archive"
                    }
                },
                DefaultNextNode = null
            },
            ["handle_validation_error"] = new TaskChainNode
            {
                NodeId = "handle_validation_error",
                TaskType = "log_error",
                InputMapping = new Dictionary<string, string>
                {
                    ["errors"] = "validate_archive.output.errors",
                    ["archivePath"] = "input.archivePath"
                },
                RoutingRules = new List<NodeRoutingRule>(),
                DefaultNextNode = null // End chain on error
            },
            ["import_with_warnings"] = new TaskChainNode
            {
                NodeId = "import_with_warnings",
                TaskType = "import_archive",
                InputMapping = new Dictionary<string, string>
                {
                    ["archivePath"] = "input.archivePath",
                    ["warnings"] = "validate_archive.output.warnings"
                },
                Metadata = new Dictionary<string, object>
                {
                    ["logWarnings"] = true
                },
                RoutingRules = new List<NodeRoutingRule>(),
                DefaultNextNode = null
            },
            ["import_archive"] = new TaskChainNode
            {
                NodeId = "import_archive",
                TaskType = "import_archive",
                InputMapping = new Dictionary<string, string>
                {
                    ["archivePath"] = "input.archivePath"
                },
                RoutingRules = new List<NodeRoutingRule>(),
                DefaultNextNode = null
            }
        }
    };

    /// <summary>
    /// Batch processing chain with conditional branching
    /// </summary>
    public static TaskChainConfiguration BatchProcessingChain => new()
    {
        StartNodeId = "get_batch_items",
        Nodes = new Dictionary<string, TaskChainNode>
        {
            ["get_batch_items"] = new TaskChainNode
            {
                NodeId = "get_batch_items",
                TaskType = "get_batch_items",
                InputMapping = new Dictionary<string, string>
                {
                    ["sourcePath"] = "input.sourcePath",
                    ["filter"] = "input.filter"
                },
                OutputMapping = new Dictionary<string, string>
                {
                    ["items"] = "items",
                    ["itemCount"] = "itemCount"
                },
                RoutingRules = new List<NodeRoutingRule>
                {
                    new NodeRoutingRule
                    {
                        Name = "No items found",
                        Condition = new RoutingCondition
                        {
                            Type = ConditionType.OutputField,
                            Field = "itemCount",
                            Operator = ComparisonOperator.Equals,
                            Value = 0
                        },
                        NextNodeId = "no_items_found"
                    },
                    new NodeRoutingRule
                    {
                        Name = "Single item",
                        Condition = new RoutingCondition
                        {
                            Type = ConditionType.OutputField,
                            Field = "itemCount",
                            Operator = ComparisonOperator.Equals,
                            Value = 1
                        },
                        NextNodeId = "process_single_item"
                    },
                    new NodeRoutingRule
                    {
                        Name = "Multiple items",
                        Condition = new RoutingCondition
                        {
                            Type = ConditionType.OutputField,
                            Field = "itemCount",
                            Operator = ComparisonOperator.GreaterThan,
                            Value = 1
                        },
                        NextNodeId = "process_batch"
                    }
                },
                DefaultNextNode = null
            },
            ["no_items_found"] = new TaskChainNode
            {
                NodeId = "no_items_found",
                TaskType = "log_info",
                InputMapping = new Dictionary<string, string>
                {
                    ["message"] = "literal:No items found to process"
                },
                RoutingRules = new List<NodeRoutingRule>(),
                DefaultNextNode = null
            },
            ["process_single_item"] = new TaskChainNode
            {
                NodeId = "process_single_item",
                TaskType = "process_item",
                InputMapping = new Dictionary<string, string>
                {
                    ["item"] = "get_batch_items.output.items[0]"
                },
                RoutingRules = new List<NodeRoutingRule>(),
                DefaultNextNode = null
            },
            ["process_batch"] = new TaskChainNode
            {
                NodeId = "process_batch",
                TaskType = "process_batch",
                InputMapping = new Dictionary<string, string>
                {
                    ["items"] = "get_batch_items.output.items",
                    ["parallel"] = "input.parallel"
                },
                RoutingRules = new List<NodeRoutingRule>
                {
                    new NodeRoutingRule
                    {
                        Name = "Batch completed successfully",
                        Condition = new RoutingCondition
                        {
                            Type = ConditionType.OutputField,
                            Field = "successCount",
                            Operator = ComparisonOperator.GreaterThan,
                            Value = 0
                        },
                        NextNodeId = "report_success"
                    }
                },
                DefaultNextNode = "report_success"
            },
            ["report_success"] = new TaskChainNode
            {
                NodeId = "report_success",
                TaskType = "generate_report",
                InputMapping = new Dictionary<string, string>
                {
                    ["processedCount"] = "process_batch.output.successCount",
                    ["failedCount"] = "process_batch.output.failedCount"
                },
                RoutingRules = new List<NodeRoutingRule>(),
                DefaultNextNode = null
            }
        }
    };

    /// <summary>
    /// Get all predefined chains
    /// </summary>
    public static Dictionary<string, TaskChainConfiguration> GetAllChains()
    {
        return new Dictionary<string, TaskChainConfiguration>
        {
            ["folder_import"] = FolderImportChain,
            ["quick_folder_import"] = QuickFolderImportChain,
            ["validated_import"] = ValidatedImportChain,
            ["batch_processing"] = BatchProcessingChain
        };
    }
}