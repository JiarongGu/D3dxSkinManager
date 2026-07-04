---
name: ipc-message-pair
description: Use when adding a new IPC command that needs both backend and frontend. Generates C# facade handler + TypeScript service method together to prevent sync mismatches.
---

# IPC Message Pair Generator

Generate BOTH backend facade handler AND frontend service method for a single IPC operation. This ensures frontend and backend stay perfectly synchronized.

**CRITICAL**: This skill prevents IPC integration errors by generating both sides of the IPC call together.

## Arguments

**Format**: `/ipc-message-pair <Module> <MessageType> <FacadeMethod> <ServiceMethod> <BackendService> <Parameters> <ReturnType>`

**Example**:
```
/ipc-message-pair Mod VALIDATE_TEXTURE HandleValidateTextureAsync validateTexture ITextureValidationService filePath:string ValidationResult
```

**Parameters**:
- `Module` - Module name (MOD, PROFILE, WORKFLOW)
- `MessageType` - IPC message name in UPPER_SNAKE_CASE (e.g., VALIDATE_TEXTURE)
- `FacadeMethod` - Backend method name (e.g., HandleValidateTextureAsync)
- `ServiceMethod` - Frontend method name in camelCase (e.g., validateTexture)
- `BackendService` - Service interface to delegate to (e.g., ITextureValidationService)
- `Parameters` - Comma-separated params with types (e.g., filePath:string,format:string)
- `ReturnType` - Return type (e.g., ValidationResult, ModInfo[], void)

## What This Skill Generates

1. **Backend Facade Handler** (adds to `{Module}Facade.cs`)
   - Private IPC handler method
   - Parameter extraction from IPC payload
   - Delegation to service
   - Proper error handling

2. **Frontend Service Method** (adds to `{serviceName}.ts`)
   - Type-safe async method
   - Proper IPC message sending
   - Return type handling (single/array/void)

## Backend Pattern (Facade Handler)

```csharp
/// <summary>
/// IPC handler for {operation description}
/// IPC Message: {MESSAGE_TYPE}
/// Payload: { param1: type, param2: type }
/// </summary>
private async Task<ReturnType> Handle{MethodName}Async(IpcRequest request)
{
    // Extract parameters from IPC payload
    var param1 = _payloadHelper.GetRequiredValue<Type>(request.Payload, "param1");
    var param2 = _payloadHelper.GetRequiredValue<Type>(request.Payload, "param2");

    // Delegate to service (facade has NO business logic)
    return await _serviceName.ServiceMethodAsync(param1, param2);
}
```

## Frontend Pattern (Service Method)

```typescript
/**
 * {Method description}
 * Backend: {FacadeName}.Handle{MethodName}Async
 */
async methodName(
  profileId: string,
  param1: Type,
  param2: Type
): Promise<ReturnType> {
  return this.sendMessage<ReturnType>(
    '{MESSAGE_TYPE}',
    profileId,
    { param1, param2 }
  );
}
```

## Return Type Handling

The skill automatically chooses the correct IPC method based on return type:

**Single Object or Void**:
```typescript
// Backend returns: Task<ModInfo> or Task<void>
return this.sendMessage<ModInfo>('MESSAGE_TYPE', profileId, params);
```

**Array**:
```typescript
// Backend returns: Task<List<ModInfo>>
return this.sendArrayMessage<ModInfo>('MESSAGE_TYPE', profileId, params);
```

**Void (No Return)**:
```typescript
// Backend returns: Task (no value)
await this.sendMessage<void>('MESSAGE_TYPE', profileId, params);
```

## Parameter Mapping

Parameters are automatically converted between C# and TypeScript:

| C# Type | TypeScript Type | IPC Payload |
|---------|-----------------|-------------|
| `string` | `string` | `{ param: string }` |
| `int` | `number` | `{ param: number }` |
| `bool` | `boolean` | `{ param: boolean }` |
| `List<T>` | `T[]` | `{ param: T[] }` |
| `Dictionary<string,string>` | `Record<string,string>` | `{ param: object }` |

## Steps to Execute

1. **Find backend facade**:
   - Path: `Modules/{Module}/{Module}Facade.cs`
   - Verify service is injected in constructor

2. **Add backend handler method**:
   - Private method (BaseFacade calls via reflection)
   - Name: `Handle{MethodName}Async`
   - Extract each parameter using `_payloadHelper.GetRequiredValue<T>`
   - Delegate to injected service
   - Return service result

3. **Find frontend service**:
   - Path: `D3dxSkinManager/shared/services/ipc/{moduleName}Service.ts`
   - If service doesn't exist, create it

4. **Add frontend method**:
   - Async method matching {ServiceMethod} parameter
   - JSDoc comment referencing backend facade/method
   - Use correct sendMessage variant based on return type
   - camelCase payload property names

5. **Verify parameter names match**:
   - Backend: `_payloadHelper.GetRequiredValue(request.Payload, "filePath")`
   - Frontend: `{ filePath }` in payload
   - MUST match exactly (case-sensitive!)

## Example Output

For: `/ipc-message-pair Mod VALIDATE_TEXTURE HandleValidateTextureAsync validateTexture ITextureValidationService filePath:string ValidationResult`

**Backend** (`Modules/Mod/ModFacade.cs`):
```csharp
/// <summary>
/// IPC handler for texture validation
/// IPC Message: VALIDATE_TEXTURE
/// Payload: { filePath: string }
/// </summary>
private async Task<ValidationResult> HandleValidateTextureAsync(IpcRequest request)
{
    var filePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "filePath");
    return await _textureValidationService.ValidateTextureAsync(filePath);
}
```

**Frontend** (`shared/services/ipc/modService.ts`):
```typescript
/**
 * Validate texture file format
 * Backend: ModFacade.HandleValidateTextureAsync
 */
async validateTexture(
  profileId: string,
  filePath: string
): Promise<ValidationResult> {
  return this.sendMessage<ValidationResult>(
    'VALIDATE_TEXTURE',
    profileId,
    { filePath }
  );
}
```

## Message Type Naming Convention

Convert method names to UPPER_SNAKE_CASE:

- `getModById` → `GET_MOD_BY_ID`
- `validateTexture` → `VALIDATE_TEXTURE`
- `batchDeleteMods` → `BATCH_DELETE_MODS`
- `importFromArchive` → `IMPORT_FROM_ARCHIVE`

## Service Injection Requirement

**IMPORTANT**: Backend facade MUST have the service injected:

```csharp
public class ModFacade : BaseFacade, IModFacade
{
    private readonly ITextureValidationService _textureValidationService;

    public ModFacade(
        ITextureValidationService textureValidationService  // Must be injected!
    )
    {
        _textureValidationService = textureValidationService;
    }

    // Handler methods can now use _textureValidationService
}
```

If service not injected yet:
1. Add to constructor parameters
2. Add private field
3. Assign in constructor body

## Common Patterns

### Pattern 1: Simple Get Operation
```
/ipc-message-pair Mod GET_TEXTURE HandleGetTextureAsync getTexture ITextureService id:string TextureInfo
```

### Pattern 2: Batch Operation
```
/ipc-message-pair Mod BATCH_DELETE_MODS HandleBatchDeleteModsAsync batchDeleteMods IModLifecycleService ids:string[] BatchResult
```

### Pattern 3: No Return (void)
```
/ipc-message-pair Mod CLEAR_CACHE HandleClearCacheAsync clearCache IModCacheService void void
```

### Pattern 4: Multiple Parameters
```
/ipc-message-pair Mod UPDATE_MOD_METADATA HandleUpdateModMetadataAsync updateModMetadata IModService id:string,name:string,author:string ModInfo
```

## Important Rules

- ✅ Backend method is PRIVATE (called by BaseFacade reflection)
- ✅ Backend extracts ALL parameters from IPC payload
- ✅ Backend delegates to service (NO business logic in facade)
- ✅ Frontend payload names match backend parameter extraction (case-sensitive!)
- ✅ Message type is UPPER_SNAKE_CASE
- ✅ Use correct sendMessage variant (single vs array vs void)
- ❌ Don't add business logic to facade (use service)
- ❌ Don't forget to inject service in facade constructor
- ❌ Don't mismatch parameter names (backend "filePath" ≠ frontend "path")

## Error Prevention

This skill prevents common IPC errors:

❌ **Backend/Frontend Mismatch**:
```csharp
// Backend expects "filePath"
var path = _payloadHelper.GetRequiredValue<string>(request.Payload, "filePath");

// Frontend sends "path"  ❌ WRONG!
{ path: filePath }

// ✅ CORRECT (skill generates matching names)
{ filePath }
```

❌ **Wrong Return Type Handler**:
```typescript
// Backend returns Task<List<ModInfo>>
// ❌ WRONG - should use sendArrayMessage
return this.sendMessage<ModInfo[]>('GET_MODS', profileId);

// ✅ CORRECT - skill chooses sendArrayMessage
return this.sendArrayMessage<ModInfo>('GET_MODS', profileId);
```

## Integration with Other Skills

Use after creating backend service:

```bash
# 1. Create backend service
/backend-service TextureValidationService Mod IFileHelper ValidateTextureAsync

# 2. Add to facade (if needed)
/backend-facade ModFacade Mod ITextureValidationService

# 3. Create IPC message pair (this skill)
/ipc-message-pair Mod VALIDATE_TEXTURE HandleValidateTextureAsync validateTexture ITextureValidationService filePath:string ValidationResult

# Now backend and frontend are perfectly synchronized!
```

## Reference Examples

Look at existing IPC pairs:
- `Modules/Mod/ModFacade.cs` + `shared/services/ipc/modService.ts`
- `Modules/Profile/ProfileFacade.cs` + `shared/services/ipc/profileService.ts`
- `Modules/Workflow/WorkflowFacade.cs` + `shared/services/ipc/workflowService.ts`

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial version

**How to update this skill**:
1. If IPC mechanism changes (e.g., new payload format), update both patterns
2. If parameter extraction changes, update backend pattern
3. If sendMessage API changes, update frontend pattern
4. Always test backend + frontend together after updates
