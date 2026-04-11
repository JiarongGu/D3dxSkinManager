---
name: backend-service
description: Use when creating a new C# service class. Generates service + interface + DI injection + IProfileEventBus event emission + OperationException error handling.
---

# Backend Service Generator

Generate a complete backend service following the D3dxSkinManager architecture patterns.

## Arguments

**Format**: `/backend-service <ServiceName> <Module> <Dependencies> <Methods>`

**Example**:
```
/backend-service TextureValidationService Mod IFileHelper,IHashHelper ValidateTextureAsync,GetSupportedFormatsAsync
```

**Parameters**:
- `ServiceName` - Name of the service class (e.g., TextureValidationService)
- `Module` - Module name (e.g., Mod, Profile, Workflow, Category)
- `Dependencies` - Comma-separated list of interface dependencies (e.g., IFileHelper,IHashHelper)
- `Methods` - Comma-separated list of method names to generate (e.g., ValidateAsync,GetDataAsync)

## What This Skill Generates

1. **Interface file** (`I{ServiceName}.cs`)
   - Public interface with all methods
   - XML documentation comments
   - Proper namespace

2. **Implementation file** (`{ServiceName}.cs`)
   - Class implementing the interface
   - Constructor with DI injection
   - All dependencies injected
   - IProfileEventBus injected for event emission
   - ILogHelper injected for logging
   - Proper error handling with OperationException
   - Event emission after successful operations
   - Appropriate logging levels (Verbose/Info/Warn)
   - XML documentation comments

3. **Service registration** (updates `{Module}ServiceExtensions.cs`)
   - Adds singleton registration for the service

## Pattern to Follow

The service MUST follow this exact structure:

```csharp
// 1. Interface (I{ServiceName}.cs)
namespace D3dxSkinManager.Modules.{Module}.Services;

/// <summary>
/// Service for {brief description}
/// </summary>
public interface I{ServiceName}
{
    /// <summary>
    /// {Method description}
    /// </summary>
    Task<ResultType> MethodNameAsync(params);
}

// 2. Implementation ({ServiceName}.cs)
namespace D3dxSkinManager.Modules.{Module}.Services;

/// <summary>
/// Implementation of I{ServiceName}
/// </summary>
public class {ServiceName} : I{ServiceName}
{
    private readonly IDependency1 _dependency1;
    private readonly IDependency2 _dependency2;
    private readonly IProfileEventBus _eventBus;  // ALWAYS inject for events
    private readonly ILogHelper _logger;          // ALWAYS inject for logging

    public {ServiceName}(
        IDependency1 dependency1,
        IDependency2 dependency2,
        IProfileEventBus eventBus,
        ILogHelper logger)
    {
        _dependency1 = dependency1;
        _dependency2 = dependency2;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<ResultType> MethodNameAsync(params)
    {
        try
        {
            _logger.Info($"Starting operation...");

            // Business logic here
            var result = await PerformOperation();

            // ✅ ALWAYS emit event after successful operation
            await _eventBus.EmitAsync(
                ModuleNames.{MODULE_CONSTANT},
                {Module}Events.EVENT_NAME,
                new { /* event data */ }
            );

            _logger.Info($"Operation completed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Operation failed: {ex.Message}");
            throw new OperationException(
                "{ERROR_CODE}",
                new Dictionary<string, string> { { "param", value } },
                "Fallback error message"
            );
        }
    }
}
```

## Steps to Execute

1. **Find the module location**:
   - Search for `Modules/{Module}/Services/` directory
   - If module doesn't exist, ask user for clarification

2. **Create interface file**:
   - Path: `Modules/{Module}/Services/I{ServiceName}.cs`
   - Add XML documentation for interface and all methods
   - Use proper return types (Task<T> for async methods)

3. **Create implementation file**:
   - Path: `Modules/{Module}/Services/{ServiceName}.cs`
   - Add all provided dependencies to constructor
   - ALWAYS add IProfileEventBus (for events)
   - ALWAYS add ILogHelper (for logging)
   - Generate method stubs with proper error handling
   - Add TODO comments for business logic

4. **Add event emission**:
   - Determine appropriate event constant from {Module}Events
   - Emit event after successful operations
   - Include relevant data in event payload

5. **Add error handling**:
   - Use OperationException with error codes
   - Check `D3dxSkinManager/Languages/en.json` for existing error codes
   - If new error code needed, ask user or suggest code name

6. **Register service**:
   - Find `Modules/{Module}/{Module}ServiceExtensions.cs`
   - Add line: `services.AddSingleton<I{ServiceName}, {ServiceName}>();`
   - Place in alphabetical order with other service registrations

7. **Verify imports**:
   - Add necessary using statements
   - Common imports:
     ```csharp
     using D3dxSkinManager.Modules.Core.Events;
     using D3dxSkinManager.Modules.Core.Exceptions;
     using D3dxSkinManager.Modules.Core.Helpers;
     using D3dxSkinManager.Modules.{Module}.Constants;
     ```

## Example Output

For: `/backend-service TextureValidationService Mod IFileHelper,IHashHelper ValidateTextureAsync`

Creates:
- `Modules/Mod/Services/ITextureValidationService.cs`
- `Modules/Mod/Services/TextureValidationService.cs`
- Updates `Modules/Mod/ModServiceExtensions.cs`

## Important Rules

- ✅ ALWAYS inject IProfileEventBus (required for event emission)
- ✅ ALWAYS inject ILogHelper (required for logging)
- ✅ ALWAYS emit events after successful operations
- ✅ ALWAYS use OperationException for errors (never throw raw exceptions)
- ✅ ALWAYS add XML documentation comments
- ✅ Use async/await pattern (methods end with "Async")
- ✅ Use proper logging levels:
  - `Verbose`: Per-item details
  - `Info`: Milestone/completion messages
  - `Warn`: Recoverable issues
  - `Error`: Exceptions and failures
- ❌ NEVER put business logic in facades (use services only)
- ❌ NEVER emit events from facades (only services emit events)
- ❌ NEVER access other module's repositories directly

## Reference Examples

Look at these existing services for patterns:
- `Modules/Mod/Services/ModLifecycleService.cs` - Service with event emission
- `Modules/Category/Services/CategoryService.cs` - Service with caching
- `Modules/Workflow/Services/WorkflowExecutionService.cs` - Service with complex logic
