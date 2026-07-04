---
name: backend-facade
description: Use when adding a new module facade or wiring IPC endpoints to services. Generates thin C# facade that routes IPC requests to services without business logic.
---

# Backend Facade Generator

Generate a thin IPC facade that handles routing between frontend and backend services.

**CRITICAL**: Facades are THIN IPC layers. NO business logic, NO event emission. Just delegation.

## Arguments

**Format**: `/backend-facade <FacadeName> <Module> <Services>`

**Example**:
```
/backend-facade TextureFacade Mod ITextureValidationService,ITextureImportService
```

**Parameters**:
- `FacadeName` - Name of the facade class (e.g., TextureFacade)
- `Module` - Module name (e.g., Mod, Profile, Workflow)
- `Services` - Comma-separated list of service interfaces to inject (e.g., IModService,IModLifecycleService)

## What This Skill Generates

1. **Interface file** (`I{FacadeName}.cs`)
   - Empty interface extending IModuleFacade
   - Facades are called only via IPC routing, not by other code

2. **Implementation file** (`{FacadeName}.cs`)
   - Class extending BaseFacade and implementing interface
   - Constructor with service dependencies
   - Private IPC handler methods (called by BaseFacade routing)
   - NO event emission (services emit events)
   - NO business logic (services contain logic)

3. **Facade registration** (updates `{Module}ServiceExtensions.cs`)
   - Adds singleton registration for the facade

## Pattern to Follow

Facades MUST follow this THIN delegation pattern:

```csharp
// 1. Interface (I{FacadeName}.cs)
namespace D3dxSkinManager.Modules.{Module}.Facades;

/// <summary>
/// Facade for {module} IPC operations
/// </summary>
public interface I{FacadeName} : IModuleFacade
{
    // Empty interface - IPC routing handled by BaseFacade
}

// 2. Implementation ({FacadeName}.cs)
namespace D3dxSkinManager.Modules.{Module}.Facades;

/// <summary>
/// Thin IPC facade for {module} operations
/// Delegates to services without business logic or event emission
/// </summary>
public class {FacadeName} : BaseFacade, I{FacadeName}
{
    private readonly IService1 _service1;
    private readonly IService2 _service2;

    public {FacadeName}(
        IService1 service1,
        IService2 service2)
    {
        _service1 = service1;
        _service2 = service2;
    }

    /// <summary>
    /// IPC handler for {operation}
    /// IPC Message: {MESSAGE_TYPE}
    /// Payload: { param1: string, param2: int }
    /// </summary>
    private async Task<ResultType> HandleOperationAsync(IpcRequest request)
    {
        // Extract parameters from IPC payload
        var param1 = _payloadHelper.GetRequiredValue<string>(request.Payload, "param1");
        var param2 = _payloadHelper.GetRequiredValue<int>(request.Payload, "param2");

        // ✅ JUST DELEGATE - service handles everything
        return await _service1.PerformOperationAsync(param1, param2);
    }
}
```

## What Facades SHOULD NOT Do

❌ **NO Business Logic**:
```csharp
// ❌ WRONG - business logic in facade
public async Task<ModInfo> LoadModAsync(IpcRequest request)
{
    var id = GetParam(request, "id");
    var mod = await _repository.GetByIdAsync(id);

    // ❌ Category conflict resolution is business logic!
    if (mod.Category != null)
    {
        var conflicting = await _repository.GetByCategoryAsync(mod.Category);
        foreach (var c in conflicting)
            await UnloadModAsync(c.Id);
    }

    return mod;
}

// ✅ CORRECT - delegate to service
public async Task<ModInfo> LoadModAsync(IpcRequest request)
{
    var id = GetParam(request, "id");
    return await _modLifecycleService.LoadAsync(id);  // Service handles logic
}
```

❌ **NO Event Emission**:
```csharp
// ❌ WRONG - facade emits events
public async Task<bool> ImportModAsync(IpcRequest request)
{
    var result = await _importService.ImportAsync(path);
    await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.IMPORTED, data);  // ❌ NO!
    return result;
}

// ✅ CORRECT - service emits events
public async Task<bool> ImportModAsync(IpcRequest request)
{
    // Service handles import AND emits event
    return await _importService.ImportAsync(path);  // ✅ YES!
}
```

## Steps to Execute

1. **Find the module location**:
   - Facades live at the module root: `Modules/{Module}/{FacadeName}.cs`
   - (There is NO `Facades/` subdirectory in this codebase)

2. **Create interface + implementation in ONE file**:
   - Path: `Modules/{Module}/{FacadeName}.cs`
   - Interface `I{FacadeName} : IModuleFacade` at the top of the same file (see ToolFacade.cs)
   - Add XML documentation explaining purpose

3. **Implementation**:
   - Extend BaseFacade and implement I{FacadeName}
   - Inject only services (NO repositories, NO event bus)
   - Create private methods for IPC handlers
   - Each method just delegates to service

4. **IPC Handler Methods**:
   - Private methods (called by BaseFacade routing)
   - Name pattern: `Handle{Operation}Async`
   - Parameters: `IpcRequest request`
   - Extract payload using `_payloadHelper.GetRequiredValue<T>`
   - Delegate to injected service
   - Return service result directly

5. **Register facade**:
   - Find `Modules/{Module}/{Module}ServiceExtensions.cs`
   - Add line: `services.AddSingleton<I{FacadeName}, {FacadeName}>();`
   - Facades are registered AFTER services in the file

6. **Verify imports**:
   - Add necessary using statements:
     ```csharp
     using D3dxSkinManager.Modules.Core.Facades;
     using D3dxSkinManager.Modules.Core.IPC;
     using D3dxSkinManager.Modules.{Module}.Services;
     ```

## Example Output

For: `/backend-facade ModFacade Mod IModLifecycleService,IModQueryService`

Creates:
- `Modules/Mod/ModFacade.cs` (IModFacade interface + implementation extending BaseFacade)
- Updates `Modules/Mod/ModServiceExtensions.cs`

## Important Rules

- ✅ Facades are THIN - only parameter extraction and delegation
- ✅ Extend BaseFacade (handles IPC routing automatically)
- ✅ Private methods for IPC handlers (BaseFacade calls them via reflection)
- ✅ Use `_payloadHelper.GetRequiredValue<T>()` for parameter extraction
- ✅ Inject services (NOT repositories, NOT event bus, NOT logger)
- ✅ Return service results directly
- ❌ NO business logic (belongs in services)
- ❌ NO event emission (services emit events)
- ❌ NO direct repository access (use services)
- ❌ NO IProfileEventBus injection (facades don't emit events)

## Reference Examples

Look at these existing facades for patterns:
- `Modules/Mod/ModFacade.cs` - Thin facade that delegates to services
- `Modules/Profile/ProfileFacade.cs` - Simple delegation pattern
- `Modules/Workflow/WorkflowFacade.cs` - IPC parameter extraction
