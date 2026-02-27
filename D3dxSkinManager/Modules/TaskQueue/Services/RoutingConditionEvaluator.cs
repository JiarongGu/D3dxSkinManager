using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Services;

/// <summary>
/// Service for evaluating routing conditions to determine next node in workflow
/// </summary>
public interface IRoutingConditionEvaluator
{
    /// <summary>
    /// Evaluate routing rules to determine the next node
    /// </summary>
    /// <param name="currentNode">The current node in the workflow</param>
    /// <param name="taskInfo">The completed task information</param>
    /// <param name="sharedData">Shared data from the chain context</param>
    /// <returns>The ID of the next node to execute, or null if no match</returns>
    string? EvaluateRoutingRules(TaskChainNode currentNode, TaskInfo taskInfo, Dictionary<string, object>? sharedData);

    /// <summary>
    /// Evaluate a single routing condition
    /// </summary>
    bool EvaluateCondition(RoutingCondition condition, TaskInfo taskInfo, Dictionary<string, object>? sharedData);
}

public class RoutingConditionEvaluator : IRoutingConditionEvaluator
{
    private readonly ILogHelper _logger;
    private readonly Dictionary<string, Func<TaskInfo, Dictionary<string, object>?, bool>> _customEvaluators;

    public RoutingConditionEvaluator(ILogHelper logger)
    {
        _logger = logger;
        _customEvaluators = new Dictionary<string, Func<TaskInfo, Dictionary<string, object>?, bool>>();

        // Register built-in custom evaluators
        RegisterBuiltInEvaluators();
    }

    public string? EvaluateRoutingRules(TaskChainNode currentNode, TaskInfo taskInfo, Dictionary<string, object>? sharedData)
    {
        if (currentNode.RoutingRules == null || !currentNode.RoutingRules.Any())
        {
            _logger.Debug($"No routing rules for node {currentNode.NodeId}, using default", "RoutingEvaluator");
            return currentNode.DefaultNextNode;
        }

        // Sort by priority (higher first) then by original order
        var sortedRules = currentNode.RoutingRules
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => currentNode.RoutingRules.IndexOf(r));

        foreach (var rule in sortedRules)
        {
            try
            {
                if (EvaluateCondition(rule.Condition, taskInfo, sharedData))
                {
                    _logger.Info($"Routing rule '{rule.Name ?? "unnamed"}' matched, routing to node: {rule.NextNodeId}", "RoutingEvaluator");
                    return rule.NextNodeId;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error evaluating routing rule '{rule.Name}': {ex.Message}", "RoutingEvaluator");
            }
        }

        _logger.Debug($"No routing rules matched for node {currentNode.NodeId}, using default", "RoutingEvaluator");
        return currentNode.DefaultNextNode;
    }

    public bool EvaluateCondition(RoutingCondition condition, TaskInfo taskInfo, Dictionary<string, object>? sharedData)
    {
        return condition.Type switch
        {
            ConditionType.TaskStatus => EvaluateTaskStatus(condition, taskInfo),
            ConditionType.OutputField => EvaluateOutputField(condition, taskInfo),
            ConditionType.SharedDataField => EvaluateSharedDataField(condition, sharedData),
            ConditionType.HasError => !string.IsNullOrEmpty(taskInfo.ErrorMessage),
            ConditionType.UserInput => EvaluateUserInput(condition, sharedData),
            ConditionType.And => EvaluateAndCondition(condition, taskInfo, sharedData),
            ConditionType.Or => EvaluateOrCondition(condition, taskInfo, sharedData),
            ConditionType.Not => !EvaluateCondition(condition.SubConditions?.FirstOrDefault() ?? throw new InvalidOperationException("Not condition requires a sub-condition"), taskInfo, sharedData),
            ConditionType.Always => true,
            ConditionType.Custom => EvaluateCustomCondition(condition, taskInfo, sharedData),
            _ => throw new NotSupportedException($"Condition type {condition.Type} is not supported")
        };
    }

    private bool EvaluateTaskStatus(RoutingCondition condition, TaskInfo taskInfo)
    {
        var statusValue = condition.Value?.ToString();
        if (string.IsNullOrEmpty(statusValue))
            return false;

        return CompareValues(taskInfo.Status.ToString(), statusValue, condition.Operator);
    }

    private bool EvaluateOutputField(RoutingCondition condition, TaskInfo taskInfo)
    {
        if (string.IsNullOrEmpty(condition.Field) || string.IsNullOrEmpty(taskInfo.Output))
            return false;

        try
        {
            var outputData = JsonSerializer.Deserialize<Dictionary<string, object>>(taskInfo.Output);
            if (outputData == null)
                return false;

            var fieldValue = GetNestedValue(outputData, condition.Field);
            return CompareValues(fieldValue, condition.Value, condition.Operator);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to evaluate output field: {ex.Message}", "RoutingEvaluator");
            return false;
        }
    }

    private bool EvaluateSharedDataField(RoutingCondition condition, Dictionary<string, object>? sharedData)
    {
        if (string.IsNullOrEmpty(condition.Field) || sharedData == null)
            return false;

        var fieldValue = GetNestedValue(sharedData, condition.Field);
        return CompareValues(fieldValue, condition.Value, condition.Operator);
    }

    private bool EvaluateUserInput(RoutingCondition condition, Dictionary<string, object>? sharedData)
    {
        if (sharedData == null || string.IsNullOrEmpty(condition.Field))
            return false;

        // User input is typically prefixed with "user_"
        var userInputKey = condition.Field.StartsWith("user_") ? condition.Field : $"user_{condition.Field}";

        if (sharedData.TryGetValue(userInputKey, out var value))
        {
            return CompareValues(value, condition.Value, condition.Operator);
        }

        return false;
    }

    private bool EvaluateAndCondition(RoutingCondition condition, TaskInfo taskInfo, Dictionary<string, object>? sharedData)
    {
        if (condition.SubConditions == null || !condition.SubConditions.Any())
            return true;

        return condition.SubConditions.All(subCondition => EvaluateCondition(subCondition, taskInfo, sharedData));
    }

    private bool EvaluateOrCondition(RoutingCondition condition, TaskInfo taskInfo, Dictionary<string, object>? sharedData)
    {
        if (condition.SubConditions == null || !condition.SubConditions.Any())
            return false;

        return condition.SubConditions.Any(subCondition => EvaluateCondition(subCondition, taskInfo, sharedData));
    }

    private bool EvaluateCustomCondition(RoutingCondition condition, TaskInfo taskInfo, Dictionary<string, object>? sharedData)
    {
        if (string.IsNullOrEmpty(condition.CustomEvaluator))
            return false;

        if (_customEvaluators.TryGetValue(condition.CustomEvaluator, out var evaluator))
        {
            return evaluator(taskInfo, sharedData);
        }

        _logger.Warn($"Custom evaluator '{condition.CustomEvaluator}' not found", "RoutingEvaluator");
        return false;
    }

    private object? GetNestedValue(Dictionary<string, object> data, string path)
    {
        var parts = path.Split('.');
        object? current = data;

        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> dict)
            {
                if (!dict.TryGetValue(part, out current))
                    return null;
            }
            else if (current is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(part, out var prop))
                {
                    current = prop;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private bool CompareValues(object? actual, object? expected, ComparisonOperator op)
    {
        // Handle null cases
        if (op == ComparisonOperator.IsNull)
            return actual == null;
        if (op == ComparisonOperator.IsNotNull)
            return actual != null;

        // Convert JsonElement to appropriate type
        actual = ConvertJsonElement(actual);
        expected = ConvertJsonElement(expected);

        // Handle empty checks
        if (op == ComparisonOperator.IsEmpty)
        {
            return actual switch
            {
                null => true,
                string s => string.IsNullOrWhiteSpace(s),
                IEnumerable<object> list => !list.Any(),
                _ => false
            };
        }
        if (op == ComparisonOperator.IsNotEmpty)
        {
            return !CompareValues(actual, null, ComparisonOperator.IsEmpty);
        }

        // String operations
        var actualStr = actual?.ToString() ?? "";
        var expectedStr = expected?.ToString() ?? "";

        return op switch
        {
            ComparisonOperator.Equals => actualStr.Equals(expectedStr, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.NotEquals => !actualStr.Equals(expectedStr, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.Contains => actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.NotContains => !actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.StartsWith => actualStr.StartsWith(expectedStr, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.EndsWith => actualStr.EndsWith(expectedStr, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.Matches => Regex.IsMatch(actualStr, expectedStr),
            ComparisonOperator.In => EvaluateInOperator(actual, expected),
            ComparisonOperator.NotIn => !EvaluateInOperator(actual, expected),
            _ => CompareNumericValues(actual, expected, op)
        };
    }

    private bool EvaluateInOperator(object? actual, object? expected)
    {
        if (expected is IEnumerable<object> list)
        {
            var actualStr = actual?.ToString();
            return list.Any(item => item?.ToString()?.Equals(actualStr, StringComparison.OrdinalIgnoreCase) == true);
        }

        // If expected is a comma-separated string
        if (expected is string str)
        {
            var items = str.Split(',', StringSplitOptions.TrimEntries);
            var actualStr = actual?.ToString();
            return items.Any(item => item.Equals(actualStr, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private bool CompareNumericValues(object? actual, object? expected, ComparisonOperator op)
    {
        if (!TryConvertToDouble(actual, out var actualNum) || !TryConvertToDouble(expected, out var expectedNum))
            return false;

        return op switch
        {
            ComparisonOperator.GreaterThan => actualNum > expectedNum,
            ComparisonOperator.GreaterThanOrEqual => actualNum >= expectedNum,
            ComparisonOperator.LessThan => actualNum < expectedNum,
            ComparisonOperator.LessThanOrEqual => actualNum <= expectedNum,
            _ => false
        };
    }

    private bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;

        return value switch
        {
            double d => (result = d) >= 0 || result < 0,
            int i => (result = i) >= 0 || result < 0,
            long l => (result = l) >= 0 || result < 0,
            float f => (result = f) >= 0 || result < 0,
            decimal dec => (result = (double)dec) >= 0 || result < 0,
            string s => double.TryParse(s, out result),
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.TryGetDouble(out result),
            _ => false
        };
    }

    private object? ConvertJsonElement(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
        }
        return value;
    }

    private void RegisterBuiltInEvaluators()
    {
        // Example: Check if file size is too large
        _customEvaluators["FileSizeTooLarge"] = (task, data) =>
        {
            if (string.IsNullOrEmpty(task.Output))
                return false;

            try
            {
                var output = JsonSerializer.Deserialize<Dictionary<string, object>>(task.Output);
                if (output != null && output.TryGetValue("fileSize", out var size))
                {
                    return TryConvertToDouble(size, out var sizeNum) && sizeNum > 100_000_000; // 100MB
                }
            }
            catch { }
            return false;
        };

        // Example: Check if validation is required based on metadata
        _customEvaluators["RequiresValidation"] = (task, data) =>
        {
            return data?.ContainsKey("requiresValidation") == true &&
                   data["requiresValidation"]?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        };
    }

    /// <summary>
    /// Register a custom condition evaluator
    /// </summary>
    public void RegisterCustomEvaluator(string key, Func<TaskInfo, Dictionary<string, object>?, bool> evaluator)
    {
        _customEvaluators[key] = evaluator;
    }
}