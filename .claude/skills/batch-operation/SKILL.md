---
name: batch-operation
description: Use when implementing bulk delete/update on multiple items by ID list. Generates parameterized SQL batch query + C# handler + TypeScript method as a complete set.
---

# Batch Operation Generator

Generate the complete batch operation triple: Backend SQL + Facade handler + Frontend service method.

**Purpose**: Batch operations (delete, update, resume) require synchronized code across 3 layers. This skill generates all 3 together to ensure consistency.

## Arguments

**Format**: `/batch-operation <Module> <Operation> <EntityType> <Parameters>`

**Example**:
```
/batch-operation Mod Delete Mod ids:string[]
/batch-operation Workflow Resume Workflow ids:string[]
/batch-operation Mod UpdateMetadata Mod ids:string[],metadata:Dictionary
```

**Parameters**:
- `Module` - Module name (Mod, Profile, Workflow)
- `Operation` - Operation name (Delete, Resume, Update, etc.)
- `EntityType` - Entity type being operated on (Mod, Workflow, Profile)
- `Parameters` - Comma-separated params with types (ids always required)

## What This Skill Generates

1. **Backend Repository Method** (adds to `{Module}Repository.cs`)
   - SQL query with parameterized IN clause
   - Transaction handling
   - Proper error handling

2. **Backend Facade Handler** (adds to `{Module}Facade.cs`)
   - IPC handler method
   - Loops through IDs with try-catch
   - Result aggregation (success/failure tracking)
   - Partial failure handling

3. **Frontend Service Method** (adds to `{moduleName}Service.ts`)
   - Type-safe async method
   - Batch IPC message
   - Result type handling

## Backend Repository Pattern (SQL with IN Clause)

```csharp
/// <summary>
/// Batch {operation} {entities}
/// </summary>
public async Task<int> Batch{Operation}Async(List<string> ids)
{
    // Validate
    if (ids == null || ids.Count == 0)
        return 0;

    // Build parameterized IN clause (prevents SQL injection)
    var parameters = ids.Select((id, index) => $"@id{index}").ToArray();
    var inClause = string.Join(",", parameters);

    var sql = $@"
        DELETE FROM {TableName}
        WHERE Id IN ({inClause})
    ";

    // Execute with parameters
    using var connection = GetConnection();
    var command = connection.CreateCommand();
    command.CommandText = sql;

    for (int i = 0; i < ids.Count; i++)
    {
        command.Parameters.AddWithValue($"@id{i}", ids[i]);
    }

    return await command.ExecuteNonQueryAsync();
}
```

**Key Pattern**: Parameterized IN clause prevents SQL injection.

## Backend Facade Pattern (Result Aggregation)

```csharp
/// <summary>
/// IPC handler for batch {operation}
/// IPC Message: BATCH_{OPERATION}_{ENTITIES}
/// Payload: { ids: string[] }
/// </summary>
private async Task<BatchResult> HandleBatch{Operation}Async(IpcRequest request)
{
    var ids = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "ids");

    var result = new BatchResult
    {
        TotalCount = ids.Count,
        SuccessCount = 0,
        FailureCount = 0,
        Errors = new List<BatchError>()
    };

    // Try each ID individually (partial failure support)
    foreach (var id in ids)
    {
        try
        {
            await _service.{Operation}Async(id);
            result.SuccessCount++;
        }
        catch (Exception ex)
        {
            result.FailureCount++;
            result.Errors.Add(new BatchError
            {
                Id = id,
                ErrorMessage = ex.Message
            });
            _logger.Warn($"Failed to {operation} {id}: {ex.Message}");
        }
    }

    _logger.Info($"Batch {operation}: {result.SuccessCount}/{result.TotalCount} successful");
    return result;
}
```

**Key Pattern**: Loop with try-catch allows partial success.

## Frontend Service Pattern

```typescript
/**
 * Batch {operation} {entities}
 * Backend: {Module}Facade.HandleBatch{Operation}Async
 */
async batch{Operation}(
  profileId: string,
  ids: string[]
): Promise<BatchResult> {
  return this.sendMessage<BatchResult>(
    'BATCH_{OPERATION}_{ENTITIES}',
    profileId,
    { ids }
  );
}
```

## BatchResult Type

The skill generates this result type (if it doesn't exist):

```csharp
// Backend
public class BatchResult
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<BatchError> Errors { get; set; } = new();
}

public class BatchError
{
    public string Id { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
```

```typescript
// Frontend
export interface BatchResult {
  totalCount: number;
  successCount: number;
  failureCount: number;
  errors: BatchError[];
}

export interface BatchError {
  id: string;
  errorMessage: string;
}
```

## Steps to Execute

1. **Determine operation type**:
   - Delete → SQL DELETE with IN clause
   - Update → SQL UPDATE with IN clause + SET clause
   - Resume/Pause → SQL UPDATE with status change

2. **Add repository method**:
   - Path: `Modules/{Module}/Repositories/{Module}Repository.cs`
   - Generate parameterized IN clause
   - Add transaction if needed
   - Return affected row count

3. **Add facade handler**:
   - Path: `Modules/{Module}/Facades/{Module}Facade.cs`
   - Extract IDs from IPC payload
   - Loop with try-catch for each ID
   - Aggregate results (success/failure counts)
   - Return BatchResult

4. **Add frontend method**:
   - Path: `shared/services/ipc/{moduleName}Service.ts`
   - Async method with ids parameter
   - Send BATCH_{OPERATION}_{ENTITIES} message
   - Return BatchResult type

5. **Verify BatchResult type exists**:
   - Backend: Check `Modules/Core/Models/` or create
   - Frontend: Check `shared/types/` or create

## Example Outputs

### Example 1: Batch Delete Mods

For: `/batch-operation Mod Delete Mod ids:string[]`

**Backend Repository** (`Modules/Mod/Repositories/ModRepository.cs`):
```csharp
public async Task<int> BatchDeleteAsync(List<string> ids)
{
    if (ids == null || ids.Count == 0) return 0;

    var parameters = ids.Select((id, i) => $"@id{i}").ToArray();
    var inClause = string.Join(",", parameters);

    var sql = $"DELETE FROM Mods WHERE Id IN ({inClause})";

    using var connection = GetConnection();
    var command = connection.CreateCommand();
    command.CommandText = sql;

    for (int i = 0; i < ids.Count; i++)
        command.Parameters.AddWithValue($"@id{i}", ids[i]);

    return await command.ExecuteNonQueryAsync();
}
```

**Backend Facade** (`Modules/Mod/Facades/ModFacade.cs`):
```csharp
private async Task<BatchResult> HandleBatchDeleteModsAsync(IpcRequest request)
{
    var ids = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "ids");

    var result = new BatchResult
    {
        TotalCount = ids.Count,
        SuccessCount = 0,
        FailureCount = 0,
        Errors = new List<BatchError>()
    };

    foreach (var id in ids)
    {
        try
        {
            await _modService.DeleteAsync(id);
            result.SuccessCount++;
        }
        catch (Exception ex)
        {
            result.FailureCount++;
            result.Errors.Add(new BatchError
            {
                Id = id,
                ErrorMessage = ex.Message
            });
        }
    }

    return result;
}
```

**Frontend Service** (`shared/services/ipc/modService.ts`):
```typescript
/**
 * Batch delete mods
 * Backend: ModFacade.HandleBatchDeleteModsAsync
 */
async batchDeleteMods(
  profileId: string,
  ids: string[]
): Promise<BatchResult> {
  return this.sendMessage<BatchResult>(
    'BATCH_DELETE_MODS',
    profileId,
    { ids }
  );
}
```

### Example 2: Batch Update Metadata

For: `/batch-operation Mod UpdateMetadata Mod ids:string[],updates:Dictionary<string,object>`

**Backend Repository**:
```csharp
public async Task<int> BatchUpdateMetadataAsync(
    List<string> ids,
    Dictionary<string, string> updates)
{
    // Build SET clause for updates
    var setClauses = updates.Select(kvp => $"{kvp.Key} = @{kvp.Key}");
    var setClause = string.Join(", ", setClauses);

    // Build IN clause
    var parameters = ids.Select((id, i) => $"@id{i}").ToArray();
    var inClause = string.Join(",", parameters);

    var sql = $"UPDATE Mods SET {setClause} WHERE Id IN ({inClause})";

    using var connection = GetConnection();
    var command = connection.CreateCommand();
    command.CommandText = sql;

    // Add update parameters
    foreach (var kvp in updates)
        command.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);

    // Add ID parameters
    for (int i = 0; i < ids.Count; i++)
        command.Parameters.AddWithValue($"@id{i}", ids[i]);

    return await command.ExecuteNonQueryAsync();
}
```

## SQL Injection Prevention

**CRITICAL**: Always use parameterized IN clause, never string concatenation!

```csharp
// ❌ WRONG - SQL INJECTION VULNERABLE
var sql = $"DELETE FROM Mods WHERE Id IN ('{string.Join("','", ids)}')";

// ✅ CORRECT - PARAMETERIZED
var parameters = ids.Select((id, i) => $"@id{i}").ToArray();
var inClause = string.Join(",", parameters);
var sql = $"DELETE FROM Mods WHERE Id IN ({inClause})";
for (int i = 0; i < ids.Count; i++)
    command.Parameters.AddWithValue($"@id{i}", ids[i]);
```

## Partial Failure Handling

Batch operations should support partial success:

```typescript
// Frontend displays result
const result = await api.mod.batchDeleteMods(profileId, selectedIds);

if (result.failureCount > 0) {
  notification.warning(
    `Deleted ${result.successCount}/${result.totalCount} mods. ` +
    `${result.failureCount} failed.`
  );
  // Show error details
  console.error('Failed IDs:', result.errors);
} else {
  notification.success(`Deleted all ${result.successCount} mods.`);
}
```

## Important Rules

- ✅ Always use parameterized IN clause (prevent SQL injection)
- ✅ Support partial success (try-catch loop in facade)
- ✅ Return BatchResult with success/failure counts
- ✅ Log failures at Warn level (not Error - expected for some items)
- ✅ Frontend shows detailed results
- ❌ Don't fail entire batch on first error
- ❌ Don't use string concatenation for SQL
- ❌ Don't disable UI during batch (show progress instead)

## Integration with Other Skills

Use after creating entity operations:

```bash
# 1. Create single-item operation
/backend-service ModService Mod IModRepository DeleteAsync

# 2. Create batch operation (this skill)
/batch-operation Mod Delete Mod ids:string[]

# 3. Frontend uses batch for multi-select delete
```

## Reference Examples

See existing batch operations:
- `Modules/Mod/Repositories/ModRepository.cs` - BatchDeleteAsync
- `Modules/Workflow/Facades/WorkflowFacade.cs` - HandleBatchResumeWorkflowsAsync
- `shared/services/ipc/modService.ts` - batchDeleteMods

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial batch operation skill

**How to update this skill**:
1. If SQL pattern changes (e.g., new DB engine), update SQL template
2. If result aggregation changes, update BatchResult type
3. If partial failure strategy changes, update facade pattern
4. Update reference examples as better implementations emerge
