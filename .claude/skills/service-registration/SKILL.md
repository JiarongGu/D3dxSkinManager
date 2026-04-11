---
name: service-registration
description: Add service registration to Module ServiceExtensions file with proper singleton/scoped lifecycle
---

# Service Registration Skill

Add service registration to a module's ServiceExtensions.cs file.

## Arguments

**Format**: `/service-registration <Module> <Interface> <Implementation> <Lifecycle>`

**Example**:
```
/service-registration Mod ITextureValidationService TextureValidationService singleton
```

**Parameters**:
- `Module` - Module name (e.g., Mod, Profile, Workflow)
- `Interface` - Interface name (e.g., ITextureValidationService)
- `Implementation` - Implementation class name (e.g., TextureValidationService)
- `Lifecycle` - Service lifecycle: `singleton` or `scoped`

## What This Skill Does

Adds a single line of registration code to the correct location in `{Module}ServiceExtensions.cs`:

```csharp
services.AddSingleton<IServiceName, ServiceName>();
// or
services.AddScoped<IServiceName, ServiceName>();
```

## Pattern

**File Location**: `Modules/{Module}/{Module}ServiceExtensions.cs`

**Registration Pattern**:
```csharp
public static class {Module}ServiceExtensions
{
    public static IServiceCollection Add{Module}Services(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Repositories
        services.AddSingleton<IRepository1, Repository1>();
        services.AddSingleton<IRepository2, Repository2>();

        // Services
        services.AddSingleton<IService1, Service1>();
        services.AddSingleton<IService2, Service2>();
        services.AddSingleton<INewService, NewService>();  // ← ADD HERE (alphabetical)

        // Event Handlers (if exists)
        services.AddSingleton<IEventHandler, EventHandler>();

        // Facades
        services.AddSingleton<IFacade, Facade>();

        return services;
    }
}
```

## Service Lifecycle Guidelines

**Use Singleton when**:
- ✅ Service is stateless
- ✅ Service can be shared across all requests
- ✅ Most common choice (90% of services)

**Use Scoped when**:
- ✅ Service holds per-request state
- ✅ Service depends on IProfileContext (profile-specific)
- ✅ Rare - only when truly needed

**Examples**:
```csharp
// Singleton - Stateless services
services.AddSingleton<IModLifecycleService, ModLifecycleService>();
services.AddSingleton<IModQueryService, ModQueryService>();

// Scoped - Per-request state (rare)
services.AddScoped<IWorkflowExecutionService, WorkflowExecutionService>();
```

## Alphabetical Ordering

Services MUST be registered in alphabetical order within their section:

```csharp
// ✅ CORRECT - Alphabetical
services.AddSingleton<IModArchiveService, ModArchiveService>();
services.AddSingleton<IModCacheService, ModCacheService>();
services.AddSingleton<IModLifecycleService, ModLifecycleService>();
services.AddSingleton<IModQueryService, ModQueryService>();

// ❌ WRONG - Not alphabetical
services.AddSingleton<IModLifecycleService, ModLifecycleService>();
services.AddSingleton<IModArchiveService, ModArchiveService>();
services.AddSingleton<IModQueryService, ModQueryService>();
services.AddSingleton<IModCacheService, ModCacheService>();
```

## Section Organization

ServiceExtensions files are organized in sections:

1. **Repositories** - Data access (IRepository implementations)
2. **Services** - Business logic (IService implementations)
3. **Event Handlers** - Event consolidation (IEventHandler implementations)
4. **Facades** - IPC layer (IFacade implementations)

**Place registration in correct section based on service type.**

## Steps to Execute

1. **Find ServiceExtensions file**:
   - Path: `Modules/{Module}/{Module}ServiceExtensions.cs`
   - If file doesn't exist, create it with template

2. **Determine section**:
   - Service interface ends with "Service" → Services section
   - Service interface ends with "Repository" → Repositories section
   - Service interface ends with "EventHandler" → Event Handlers section
   - Service interface ends with "Facade" → Facades section

3. **Find insertion point**:
   - Within correct section
   - Find alphabetically correct position
   - Insert new registration line

4. **Add registration**:
   ```csharp
   services.Add{Lifecycle}<{Interface}, {Implementation}>();
   ```

5. **Verify imports**:
   - Ensure `using Microsoft.Extensions.DependencyInjection;`
   - Ensure `using D3dxSkinManager.Modules.{Module}.Services;` (or appropriate namespace)

## Example Output

For: `/service-registration Mod ITextureValidationService TextureValidationService singleton`

**Updates**: `Modules/Mod/ModServiceExtensions.cs`

```csharp
// Services section
services.AddSingleton<IModLifecycleService, ModLifecycleService>();
services.AddSingleton<IModQueryService, ModQueryService>();
services.AddSingleton<ITextureValidationService, TextureValidationService>();  // ← ADDED
```

## Common Mistakes to Avoid

❌ **Wrong lifecycle**:
```csharp
// Most services should be singleton, not scoped
services.AddScoped<IModService, ModService>();  // Wrong
services.AddSingleton<IModService, ModService>();  // Correct
```

❌ **Not alphabetical**:
```csharp
services.AddSingleton<IZService, ZService>();
services.AddSingleton<IAService, AService>();  // Wrong order!
```

❌ **Wrong section**:
```csharp
// Repositories section
services.AddSingleton<IModService, ModService>();  // Should be in Services!
```

❌ **Missing using statement**:
```csharp
// Need: using D3dxSkinManager.Modules.Mod.Services;
services.AddSingleton<IModService, ModService>();  // Won't compile
```

## Important Rules

- ✅ Always use singleton unless service has per-request state
- ✅ Register in alphabetical order within section
- ✅ Place in correct section (Services/Repositories/EventHandlers/Facades)
- ✅ Verify using statements are present
- ✅ One registration per service (no duplicates)
- ❌ Don't register same service twice
- ❌ Don't use transient lifecycle (not used in this project)

## Integration with Other Skills

This skill is typically used after:
- `/backend-service` - After generating service, register it
- `/backend-facade` - After generating facade, register it
- `/event-handler` - After generating handler, register it

**Workflow**:
```bash
# 1. Generate service
/backend-service TextureValidationService Mod IFileHelper ValidateTextureAsync

# 2. Register service (this skill)
/service-registration Mod ITextureValidationService TextureValidationService singleton

# Done!
```

## Reference Examples

See existing ServiceExtensions files:
- `Modules/Mod/ModServiceExtensions.cs` - Complete example with all sections
- `Modules/Profile/ProfileServiceExtensions.cs` - Simpler module
- `Modules/Workflow/WorkflowServiceExtensions.cs` - Complex module

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial version

**How to update this skill**:
1. If new service types added (beyond Services/Repositories/EventHandlers/Facades), add new section
2. If lifecycle guidelines change, update "Service Lifecycle Guidelines"
3. Update reference examples if better patterns emerge
