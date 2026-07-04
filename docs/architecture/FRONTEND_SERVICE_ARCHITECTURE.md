# Frontend Service Architecture

**Last Updated:** 2026-02-23
**Status:** Current Implementation

## Overview

Module-based service classes provide type-safe IPC communication through `BaseModuleService` with generic type parameters.

## BaseModuleService

```typescript
export abstract class BaseModuleService {
  protected readonly moduleName: ModuleName;  // Union type

  constructor(moduleName: ModuleName) {
    this.moduleName = moduleName;
  }

  // Core method with dual generics: <TResponse, TPayload>
  protected async sendMessage<T, TPayload = unknown>(
    type: string,
    profileId?: string,
    payload?: TPayload
  ): Promise<T> {
    return bridgeService.sendMessage<T>({
      module: this.moduleName,
      type,
      profileId,
      payload
    });
  }

  // Convenience methods
  protected async sendBooleanMessage<TPayload = unknown>(
    type: string,
    profileId?: string,
    payload?: TPayload
  ): Promise<boolean> {
    return this.sendMessage<boolean, TPayload>(type, profileId, payload);
  }

  protected async sendArrayMessage<T, TPayload = unknown>(
    type: string,
    profileId?: string,
    payload?: TPayload
  ): Promise<T[]> {
    return this.sendMessage<T[], TPayload>(type, profileId, payload);
  }
}
```

**Key Features:**
- Dual generics: `<TResponse, TPayload = unknown>`
- Profile ID as separate parameter
- ModuleName union type prevents typos
- No `any` types

## Module Services

| Module | Service | Module Name | Purpose |
|--------|---------|-------------|---------|
| Mods | `ModService` | `'MOD'` | Mod management |
| Profiles | `ProfileService` | `'PROFILE'` | Profile operations |
| Launch | `LaunchService` | `'LAUNCH'` | Game launching |
| Tools | `ToolsService` | `'TOOL'` | Cache, validation |
| Settings | `SettingsService` | `'SETTING'` | App settings |
| System | `SystemService` | `'SYSTEM'` | System utilities |
| Migration | `MigrationService` | `'MIGRATION'` | Python migration |
| Plugins | `PluginsService` | `'PLUGIN'` | Plugin management |

## Implementation Pattern

```typescript
class ModService extends BaseModuleService {
  constructor() {
    super('MOD');  // Module name fixed
  }

  async getAllMods(profileId?: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_ALL', profileId);
  }

  async loadMod(profileId: string, id: string): Promise<boolean> {
    return this.sendBooleanMessage('LOAD', profileId, { id });
  }

  async deleteMod(profileId: string, id: string): Promise<boolean> {
    return this.sendBooleanMessage('DELETE', profileId, { id });
  }
}

export const modService = new ModService();
```

## Usage in Components

```typescript
// Import service
import { modService } from '../services/modService';
import { useProfile } from '../../shared/context/ProfileContext';

// Use in component
const ModList = () => {
  const { selectedProfileId } = useProfile();
  const [mods, setMods] = useState<ModInfo[]>([]);

  useEffect(() => {
    if (selectedProfileId) {
      modService.getAllMods(selectedProfileId).then(setMods);
    }
  }, [selectedProfileId]);

  const handleLoad = async (id: string) => {
    if (selectedProfileId) {
      await modService.loadMod(selectedProfileId, id);
      // Refresh list
      const updated = await modService.getAllMods(selectedProfileId);
      setMods(updated);
    }
  };

  return <div>{/* UI */}</div>;
};
```

## Profile-Aware Pattern

Services require profileId for profile-scoped operations:

```typescript
// Profile-scoped operation
await modService.getAllMods(profileId);  // Requires profile

// Global operation
await settingsService.getGlobalSettings();  // No profile needed

// Mixed service
class LaunchService extends BaseModuleService {
  // Profile-scoped
  async launchCustomProgram(profileId: string, executablePath: string): Promise<boolean> {
    return this.sendBooleanMessage('LAUNCH_CUSTOM', profileId, { executablePath });
  }

  // Global
  async getAvailableVersions(profileId: string): Promise<D3DMigotoVersion[]> {
    return this.sendArrayMessage<D3DMigotoVersion>('LAUNCH_GET_VERSIONS', profileId);
  }
}
```

## Error Handling

```typescript
class ModService extends BaseModuleService {
  async loadMod(profileId: string, id: string): Promise<boolean> {
    try {
      return await this.sendBooleanMessage('LOAD', profileId, { id });
    } catch (error: unknown) {
      const msg = error instanceof Error ? error.message : 'Failed to load mod';
      console.error('Load mod error:', error);
      throw new Error(msg);
    }
  }
}
```

## Benefits

1. **Type Safety** - Module name encapsulated, generics for payload/response
2. **Consistency** - All services follow same pattern
3. **Maintainability** - Changes to IPC format only affect base class
4. **Discoverability** - Service methods provide clear API
5. **Profile Awareness** - Explicit profileId parameter

## Migration from Direct IPC

```typescript
// ❌ Old: Direct bridgeService calls
await bridgeService.sendMessage({
  module: 'MOD',
  type: 'GET_ALL',
  profileId: selectedProfileId
});

// ✅ New: Service method
await modService.getAllMods(selectedProfileId);
```

## Testing

Services are easily testable with dependency injection:

```typescript
// Mock bridgeService for testing
jest.mock('../../shared/services/bridgeService', () => ({
  sendMessage: jest.fn().mockResolvedValue([])
}));

// Test service
describe('ModService', () => {
  it('should get all mods', async () => {
    const mods = await modService.getAllMods('profile-123');
    expect(bridgeService.sendMessage).toHaveBeenCalledWith({
      module: 'MOD',
      type: 'GET_ALL',
      profileId: 'profile-123'
    });
  });
});
```

## Related Documentation

- [CURRENT_ARCHITECTURE.md](CURRENT_ARCHITECTURE.md) - System overview
- [FRONTEND_CONTEXT_ARCHITECTURE.md](FRONTEND_CONTEXT_ARCHITECTURE.md) - React context
- [baseModuleService.ts](../../D3dxSkinManager.Client/src/shared/services/baseModuleService.ts) - Implementation