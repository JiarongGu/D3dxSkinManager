# AppFacade Refactoring - Module-Based Routing

## Overview

This document describes the refactoring that introduced a top-level `AppFacade` for centralized IPC message routing using an explicit `Module` field instead of prefix parsing.

## Goals

1. **Explicit Module Routing** - Use a dedicated `Module` field in `MessageRequest` instead of parsing message type prefixes
2. **Centralized Routing Logic** - Extract all routing logic from `Program.cs` into a dedicated `AppFacade` class
3. **Cleaner Architecture** - Separate concerns: Program.cs handles IPC, AppFacade handles routing, module facades handle business logic
4. **Backward Compatibility** - Maintain support for legacy message types without a Module field

## Architecture

### Request Flow

```
Frontend IPC Message
    ↓
Program.cs (IPC Handler)
    ↓
Plugin Interception (optional)
    ↓
AppFacade (Top-Level Router)
    ↓
Module Facade (Module-Level Router)
    ↓
Service Layer (Business Logic)
    ↓
Response
```

### Three-Tier Routing Strategy

The `AppFacade` implements a three-tier routing strategy to handle different message formats:

#### 1. Module-Based Routing (Preferred)

**New Format:**
```json
{
  "id": "123",
  "module": "MOD",
  "type": "GET_ALL",
  "payload": {}
}
```

**Routing:** Direct lookup by `request.Module` → `IModFacade`

**Benefits:**
- Explicit and unambiguous
- No string parsing required
- Supports module name aliases (MOD/MODS, TOOL/TOOLS, etc.)
- Clear separation between module and action

#### 2. Legacy Exact Match

**Legacy Format:**
```json
{
  "id": "123",
  "type": "GET_ALL_MODS",
  "payload": {}
}
```

**Routing:** Exact match lookup in legacy handler dictionary

**Benefits:**
- Full backward compatibility
- No changes required to existing frontend code
- Explicit mapping of old message types to new facades

#### 3. Prefix-Based Routing (Fallback)

**Prefix Format:**
```json
{
  "id": "123",
  "type": "MOD_GET_ALL",
  "payload": {}
}
```

**Routing:** Extract prefix before first underscore → Module facade

**Benefits:**
- Works with new naming convention
- Graceful fallback for messages without Module field
- Natural migration path from old to new format

## Implementation

### 1. MessageRequest Model

Added optional `Module` field:

```csharp
// D3dxSkinManager/Modules/Core/Models/MessageRequest.cs
public class MessageRequest
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Module { get; set; }  // NEW: Explicit module routing
    public JsonElement? Payload { get; set; }
}
```

### 2. IModuleFacade Interface

Created common interface for polymorphic routing:

```csharp
// D3dxSkinManager/Facades/AppFacade.cs
public interface IModuleFacade
{
    Task<MessageResponse> HandleMessageAsync(MessageRequest request);
}
```

All module facades now inherit from `IModuleFacade`:

```csharp
// Example: D3dxSkinManager/Modules/Mods/IModFacade.cs
public interface IModFacade : IModuleFacade
{
    // Module-specific methods
    Task<List<ModInfo>> GetAllModsAsync();
    Task<bool> LoadModAsync(string sha);
    // ...
}
```

### 3. IAppFacade & AppFacade

Created top-level facade for centralized routing:

```csharp
// D3dxSkinManager/Facades/IAppFacade.cs
public interface IAppFacade
{
    Task<MessageResponse> HandleMessageAsync(MessageRequest request);
}

// D3dxSkinManager/Facades/AppFacade.cs
public class AppFacade : IAppFacade
{
    private readonly IModFacade _modFacade;
    private readonly ID3DMigotoFacade _d3dMigotoFacade;
    // ... other facades

    private readonly Dictionary<string, Func<MessageRequest, Task<MessageResponse>>> _legacyMessageHandlers;

    public AppFacade(/* inject all module facades */)
    {
        // Initialize facades and legacy mappings
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        // Strategy 1: Route by Module field (new preferred method)
        if (!string.IsNullOrEmpty(request.Module))
        {
            return await RouteByModule(request);
        }

        // Strategy 2: Try exact legacy message type match
        if (_legacyMessageHandlers.TryGetValue(request.Type, out var legacyHandler))
        {
            return await legacyHandler(request);
        }

        // Strategy 3: Try prefix-based routing
        var prefix = request.Type.Split('_')[0];
        var facade = GetFacadeByModuleName(prefix);
        if (facade != null)
        {
            return await facade.HandleMessageAsync(request);
        }

        throw new InvalidOperationException($"No handler found");
    }

    private IModuleFacade? GetFacadeByModuleName(string moduleName)
    {
        return moduleName.ToUpperInvariant() switch
        {
            "MOD" or "MODS" => _modFacade,
            "D3DMIGOTO" => _d3dMigotoFacade,
            "GAME" => _gameFacade,
            "WAREHOUSE" => _warehouseFacade,
            "TOOLS" or "TOOL" => _toolsFacade,
            "PLUGINS" or "PLUGIN" => _pluginsFacade,
            "SETTINGS" or "SETTING" => _settingsFacade,
            "MIGRATION" or "MIGRATE" => _migrationFacade,
            "PROFILE" or "PROFILES" => _profileFacade,
            _ => null
        };
    }
}
```

### 4. Service Registration

Registered `AppFacade` in DI container:

```csharp
// D3dxSkinManager/Configuration/ServiceCollectionExtensions.cs
public static IServiceCollection AddD3dxSkinManagerServices(...)
{
    // ... register all module services and facades

    // Register top-level application facade
    services.AddSingleton<IAppFacade, AppFacade>();

    return services;
}
```

### 5. Program.cs Simplification

Removed all routing logic from `Program.cs`:

**Before:**
```csharp
// 80+ lines of facade resolution and mapping
private static Dictionary<string, Func<MessageRequest, Task<MessageResponse>>> _facadeHandlers = new();

private static void InitializeServices()
{
    var modFacade = _serviceProvider.GetRequiredService<IModFacade>();
    var d3dMigotoFacade = _serviceProvider.GetRequiredService<ID3DMigotoFacade>();
    // ... resolve 9 facades

    _facadeHandlers["MOD"] = modFacade.HandleMessageAsync;
    _facadeHandlers["GET_ALL_MODS"] = modFacade.HandleMessageAsync;
    // ... 40+ message mappings
}

private static async Task<MessageResponse> RouteToFacade(MessageRequest request)
{
    // Routing logic
}
```

**After:**
```csharp
// Clean and simple
private static IAppFacade? _appFacade;

private static void InitializeServices()
{
    _serviceProvider = services.BuildServiceProvider();
    _appFacade = _serviceProvider.GetRequiredService<IAppFacade>();
}

// In HandleWebMessage
response = await _appFacade.HandleMessageAsync(request);
```

**Result:** Reduced `Program.cs` from ~450 lines to ~370 lines (18% reduction)

## Module Facade Responsibilities

Each module facade is responsible for:

1. **Message Handling** - Implement `HandleMessageAsync` from `IModuleFacade`
2. **Type Routing** - Route by `request.Type` to appropriate service methods
3. **Response Building** - Build `MessageResponse` with success/error
4. **Backward Compatibility** - Handle both new and legacy message types

**Example: ModFacade**

```csharp
public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
{
    object? responseData = request.Type switch
    {
        // New format
        "GET_ALL" or "LIST" => await GetAllModsAsync(),
        "LOAD" => await LoadModAsync(request),
        "UNLOAD" => await UnloadModAsync(request),

        // Legacy format (backward compatibility)
        "GET_ALL_MODS" => await GetAllModsAsync(),
        "LOAD_MOD" => await LoadModAsync(request),
        "UNLOAD_MOD" => await UnloadModAsync(request),

        _ => throw new InvalidOperationException($"Unknown type: {request.Type}")
    };

    return MessageResponse.CreateSuccess(request.Id, responseData);
}
```

## Migration Guide

### For Frontend Developers

#### Option 1: Use New Module Field (Recommended)

```typescript
// Old way
const response = await window.ipc.sendMessage('GET_ALL_MODS', {});

// New way (preferred)
const response = await window.ipc.sendMessage({
  module: 'MOD',
  type: 'GET_ALL',
  payload: {}
});
```

#### Option 2: Use Prefix Format

```typescript
// Works without Module field
const response = await window.ipc.sendMessage('MOD_GET_ALL', {});
```

#### Option 3: Continue Using Legacy

```typescript
// Still works (backward compatible)
const response = await window.ipc.sendMessage('GET_ALL_MODS', {});
```

### For Backend Developers

#### Adding a New Module

1. **Create Facade Interface:**
```csharp
public interface INewModuleFacade : IModuleFacade
{
    // Module methods
}
```

2. **Create Facade Implementation:**
```csharp
public class NewModuleFacade : INewModuleFacade
{
    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        // Route by request.Type
    }
}
```

3. **Register in DI:**
```csharp
services.AddSingleton<INewModuleFacade, NewModuleFacade>();
```

4. **Update AppFacade Constructor:**
```csharp
public AppFacade(
    // ... existing facades
    INewModuleFacade newModuleFacade)
{
    _newModuleFacade = newModuleFacade;
}
```

5. **Add to GetFacadeByModuleName:**
```csharp
"NEWMODULE" => _newModuleFacade,
```

## Benefits

### Code Organization

- **Separation of Concerns** - Program.cs handles IPC, AppFacade routes, module facades handle business logic
- **Single Responsibility** - Each class has one clear purpose
- **Testability** - Can test AppFacade routing independently
- **Maintainability** - Changes to routing logic isolated to AppFacade

### Performance

- **Fewer String Operations** - Direct module lookup instead of prefix parsing
- **Dictionary Lookups** - O(1) for legacy messages
- **No Reflection** - All routing is compile-time type-safe

### Flexibility

- **Module Aliases** - Support multiple names per module (MOD/MODS, TOOL/TOOLS)
- **Easy Extension** - Add new modules by updating GetFacadeByModuleName
- **Graceful Migration** - Three-tier strategy supports gradual frontend migration

### Type Safety

- **Compile-Time Checking** - All facades must implement IModuleFacade
- **No Magic Strings** - Module names in switch expression
- **Clear Contracts** - Interface-based design

## File Changes

### Created Files

- `D3dxSkinManager/Facades/IAppFacade.cs` - Top-level facade interface
- `D3dxSkinManager/Facades/AppFacade.cs` - Top-level facade implementation with IModuleFacade definition
- `docs/architecture/APP_FACADE_REFACTORING.md` - This document

### Modified Files

- `D3dxSkinManager/Modules/Core/Models/MessageRequest.cs` - Added Module field
- `D3dxSkinManager/Modules/*/I*Facade.cs` (9 files) - Inherit from IModuleFacade
- `D3dxSkinManager/Configuration/ServiceCollectionExtensions.cs` - Register AppFacade
- `D3dxSkinManager/Program.cs` - Simplified to use AppFacade (~80 lines removed)

## Build Status

```
✅ BUILD SUCCESS
0 Errors
8 Warnings (pre-existing, unrelated)
```

## Testing Recommendations

### Unit Tests

```csharp
[Fact]
public async Task AppFacade_RoutesModuleField_ToCorrectFacade()
{
    // Arrange
    var request = new MessageRequest
    {
        Id = "123",
        Module = "MOD",
        Type = "GET_ALL"
    };

    // Act
    var response = await _appFacade.HandleMessageAsync(request);

    // Assert
    Assert.True(response.Success);
}

[Fact]
public async Task AppFacade_RoutesLegacyMessage_ToCorrectFacade()
{
    // Arrange
    var request = new MessageRequest
    {
        Id = "123",
        Type = "GET_ALL_MODS"
    };

    // Act
    var response = await _appFacade.HandleMessageAsync(request);

    // Assert
    Assert.True(response.Success);
}

[Fact]
public async Task AppFacade_RoutesPrefixMessage_ToCorrectFacade()
{
    // Arrange
    var request = new MessageRequest
    {
        Id = "123",
        Type = "MOD_GET_ALL"
    };

    // Act
    var response = await _appFacade.HandleMessageAsync(request);

    // Assert
    Assert.True(response.Success);
}
```

### Integration Tests

1. **Test all routing strategies** - Verify Module field, legacy, and prefix routing
2. **Test module aliases** - Verify MOD/MODS, TOOL/TOOLS work correctly
3. **Test plugin interception** - Verify plugins still intercept before AppFacade
4. **Test error handling** - Verify unknown modules/types throw correct exceptions

## Future Enhancements

### Frontend Type Safety

Create TypeScript types for module-based messages:

```typescript
type ModuleMessage<M extends Module, T extends string> = {
  module: M;
  type: T;
  payload?: any;
};

type ModMessage =
  | ModuleMessage<'MOD', 'GET_ALL'>
  | ModuleMessage<'MOD', 'LOAD'>
  | ModuleMessage<'MOD', 'UNLOAD'>;

// Usage
const msg: ModMessage = {
  module: 'MOD',
  type: 'GET_ALL',
  payload: {}
};
```

### Module Discovery

Add reflection-based module discovery:

```csharp
public IModuleFacade? GetFacadeByModuleName(string moduleName)
{
    // Use reflection to find all IModuleFacade implementations
    // and build routing table dynamically
}
```

### Routing Middleware

Add middleware pipeline for cross-cutting concerns:

```csharp
public class LoggingMiddleware : IRoutingMiddleware
{
    public async Task<MessageResponse> InvokeAsync(
        MessageRequest request,
        Func<Task<MessageResponse>> next)
    {
        _logger.LogInfo($"Routing: {request.Module}/{request.Type}");
        return await next();
    }
}
```

## Conclusion

The AppFacade refactoring successfully:

- ✅ Introduced explicit module-based routing
- ✅ Centralized all routing logic
- ✅ Maintained backward compatibility
- ✅ Simplified Program.cs
- ✅ Improved code organization
- ✅ Enabled graceful migration path

The architecture now has clear separation of concerns with three distinct layers:
1. **IPC Layer** (Program.cs) - Handles Photino WebMessage protocol
2. **Routing Layer** (AppFacade) - Routes to appropriate module
3. **Business Logic Layer** (Module Facades + Services) - Handles operations

This provides a solid foundation for future enhancements while maintaining full backward compatibility with existing frontend code.
