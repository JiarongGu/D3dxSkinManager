# AppFacade - Centralized IPC Routing

**Last Updated:** 2026-02-23
**Status:** Current Implementation

## Overview

Top-level `AppFacade` provides centralized IPC message routing using explicit `Module` field instead of prefix parsing.

## Architecture

```
Frontend IPC Message
    ↓
Program.cs (IPC Handler)
    ↓
Plugin Interception (optional)
    ↓
AppFacade (Top-Level Router)
    ↓
Module Facade (Module Router)
    ↓
Service Layer (Business Logic)
```

## Three-Tier Routing Strategy

### 1. Module-Based Routing (Preferred)
```json
{
  "id": "123",
  "module": "MOD",
  "type": "GET_ALL",
  "payload": {}
}
```
Routes directly to module facade via `request.Module`.

### 2. Legacy Exact Match
```json
{
  "id": "123",
  "type": "GET_ALL_MODS",
  "payload": {}
}
```
Maintains backward compatibility via exact match dictionary.

### 3. Prefix-Based Fallback
```json
{
  "id": "123",
  "type": "MOD_GET_ALL",
  "payload": {}
}
```
Extracts prefix before underscore for routing.

## Implementation

### MessageRequest Model
```csharp
public class MessageRequest {
  public string Id { get; set; }
  public string Type { get; set; }
  public string? Module { get; set; }  // Explicit module routing
  public string? ProfileId { get; set; }  // Profile context
  public JsonElement? Payload { get; set; }
}
```

### AppFacade Router
```csharp
public class AppFacade : IAppFacade {
  // DI injected facades
  private readonly IModFacade _modFacade;
  private readonly IProfileFacade _profileFacade;
  // ... other facades

  public async Task<MessageResponse> HandleMessageAsync(MessageRequest request) {
    // 1. Try module-based routing
    if (!string.IsNullOrEmpty(request.Module)) {
      return await RouteByModule(request);
    }

    // 2. Try legacy exact match
    if (_legacyHandlers.TryGetValue(request.Type, out var handler)) {
      return await handler(request);
    }

    // 3. Try prefix-based routing
    return await RouteByPrefix(request);
  }

  private IModuleFacade? GetFacadeByModuleName(string moduleName) =>
    moduleName.ToUpperInvariant() switch {
      "MOD" or "MODS" => _modFacade,
      "PROFILE" or "PROFILES" => _profileFacade,
      "SETTINGS" => _settingsFacade,
      "SYSTEM" => _systemUtilsFacade,
      "TOOLS" or "TOOL" => _toolsFacade,
      "LAUNCH" => _launchFacade,
      "PLUGINS" or "PLUGIN" => _pluginsFacade,
      "MIGRATION" => _migrationFacade,
      _ => null
    };
}
```

### Module Facade Pattern
```csharp
public interface IModuleFacade {
  Task<MessageResponse> HandleMessageAsync(MessageRequest request);
}

public class ModFacade : IModFacade {
  public async Task<MessageResponse> HandleMessageAsync(MessageRequest request) {
    object? responseData = request.Type switch {
      "GET_ALL" => await GetAllModsAsync(),
      "LOAD" => await LoadModAsync(request),
      _ => throw new InvalidOperationException($"Unknown: {request.Type}")
    };
    return MessageResponse.CreateSuccess(request.Id, responseData);
  }
}
```

## Benefits

1. **Explicit Routing** - Module field eliminates ambiguity
2. **Centralized Logic** - All routing in one place
3. **Clean Separation** - Program.cs handles IPC, AppFacade routes, facades handle logic
4. **Backward Compatible** - Legacy messages still work
5. **Profile-Aware** - ProfileId at message level, not in payload
6. **Plugin Ready** - Plugins can intercept before AppFacade

## Module Aliases

Supports flexible naming:
- `MOD` / `MODS`
- `PROFILE` / `PROFILES`
- `TOOL` / `TOOLS`
- `PLUGIN` / `PLUGINS`

## Legacy Handler Examples

```csharp
_legacyHandlers = new Dictionary<string, Func<MessageRequest, Task<MessageResponse>>> {
  ["GET_ALL_MODS"] = async (req) => await _modFacade.HandleMessageAsync(
    new MessageRequest { Id = req.Id, Module = "MOD", Type = "GET_ALL", Payload = req.Payload }
  ),
  ["CREATE_PROFILE"] = async (req) => await _profileFacade.HandleMessageAsync(
    new MessageRequest { Id = req.Id, Module = "PROFILE", Type = "CREATE", Payload = req.Payload }
  )
};
```

## Frontend Integration

### New Pattern (Recommended)
```typescript
// BaseModuleService automatically includes module
class ModService extends BaseModuleService {
  constructor() { super('MOD'); }

  async getAllMods(): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_ALL');
  }
}
```

### Legacy Support
```typescript
// Old pattern still works
await bridgeService.sendMessage({
  type: 'GET_ALL_MODS',
  payload: {}
});
```

## Migration Path

1. **Phase 1** - Add Module field support (✅ Complete)
2. **Phase 2** - Update frontend services to use Module field (✅ Complete)
3. **Phase 3** - Deprecate legacy handlers (Future)
4. **Phase 4** - Remove prefix-based routing (Future)

## Related Documentation

- [CURRENT_ARCHITECTURE.md](CURRENT_ARCHITECTURE.md) - System overview
- [MODULE_ARCHITECTURE.md](MODULE_ARCHITECTURE.md) - Module structure
- [FRONTEND_SERVICE_ARCHITECTURE.md](FRONTEND_SERVICE_ARCHITECTURE.md) - Frontend patterns