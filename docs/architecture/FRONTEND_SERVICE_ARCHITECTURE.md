# Frontend Service Architecture - Module-Based Services

## Overview

Implemented dedicated service classes for each frontend module that mirror the backend module structure, providing type-safe IPC communication through a shared base class.

## Architecture

### Base Service Class

**File:** [baseModuleService.ts](../../D3dxSkinManager.Client/src/shared/services/baseModuleService.ts)

```typescript
/**
 * Abstract base class for module services
 * Each module service extends this to provide typed operations
 */
export abstract class BaseModuleService {
  protected readonly moduleName: ModuleName;

  constructor(moduleName: ModuleName) {
    this.moduleName = moduleName;
  }

  // Core method - routes to specific module
  protected async sendMessage<T>(type: string, payload?: any): Promise<T> {
    return photinoService.sendMessage<T>(this.moduleName, type, payload);
  }

  // Convenience methods for common return types
  protected async sendBooleanMessage(type: string, payload?: any): Promise<boolean>
  protected async sendArrayMessage<T>(type: string, payload?: any): Promise<T[]>
  protected async sendNullableMessage<T>(type: string, payload?: any): Promise<T | null>
}
```

### Module Services

Each module has a dedicated service class that extends `BaseModuleService`:

| Module | Service Class | Module Name | File |
|--------|---------------|-------------|------|
| Mods | `ModService` | `'MOD'` | [modService.ts](../../D3dxSkinManager.Client/src/modules/mods/services/modService.ts) |
| Profiles | `ProfileService` | `'PROFILE'` | [profileService.ts](../../D3dxSkinManager.Client/src/modules/profiles/services/profileService.ts) |
| D3DMigoto | `D3DMigotoService` | `'D3DMIGOTO'` | [d3dMigotoService.ts](../../D3dxSkinManager.Client/src/modules/tools/services/d3dMigotoService.ts) |
| Cache | `CacheService` | `'TOOLS'` | [cacheService.ts](../../D3dxSkinManager.Client/src/modules/tools/services/cacheService.ts) |
| Validation | `ValidationService` | `'TOOLS'` | [validationService.ts](../../D3dxSkinManager.Client/src/modules/tools/services/validationService.ts) |
| Migration | `MigrationService` | `'MIGRATION'` | [migrationService.ts](../../D3dxSkinManager.Client/src/modules/migration/services/migrationService.ts) |
| File Dialog | `FileDialogService` | `'SETTINGS'` | [fileDialogService.ts](../../D3dxSkinManager.Client/src/shared/services/fileDialogService.ts) |

## Benefits

### 1. Type Safety

**Before:**
```typescript
// Direct photinoService calls - easy to make mistakes
await photinoService.sendMessage('MOD', 'GET_ALL');  // string literals
await photinoService.sendMessage('MODS', 'GET_ALL'); // typo! wrong module
```

**After:**
```typescript
// Service encapsulates module name - impossible to get wrong
await modService.getAllMods();  // module name is fixed in constructor
```

### 2. Encapsulation

**Before:**
```typescript
// Module name repeated everywhere
await photinoService.sendMessage<ModInfo[]>('MOD', 'GET_ALL');
await photinoService.sendMessage<boolean>('MOD', 'LOAD', { sha });
await photinoService.sendMessage<boolean>('MOD', 'UNLOAD', { sha });
```

**After:**
```typescript
// Module name encapsulated in service
await modService.getAllMods();      // returns ModInfo[]
await modService.loadMod(sha);      // returns boolean
await modService.unloadMod(sha);    // returns boolean
```

### 3. Convenience Methods

**Before:**
```typescript
// Manual type annotations everywhere
const mods = await photinoService.sendMessage<ModInfo[]>('MOD', 'GET_ALL');
const success = await photinoService.sendMessage<boolean>('MOD', 'LOAD', { sha });
const profile = await photinoService.sendMessage<Profile | null>('PROFILE', 'GET_BY_ID', { profileId });
```

**After:**
```typescript
// Base class convenience methods
class ModService extends BaseModuleService {
  async getAllMods() {
    return this.sendArrayMessage<ModInfo>('GET_ALL');  // returns ModInfo[]
  }

  async loadMod(sha: string) {
    return this.sendBooleanMessage('LOAD', { sha });    // returns boolean
  }
}

class ProfileService extends BaseModuleService {
  async getProfileById(profileId: string) {
    return this.sendNullableMessage<Profile>('GET_BY_ID', { profileId });  // returns Profile | null
  }
}
```

### 4. Self-Documenting

**Before:**
```typescript
// What module does this belong to? What does it return?
await photinoService.sendMessage('GET_ALL');
```

**After:**
```typescript
// Clear which module and what it does
await modService.getAllMods();      // MOD module, returns all mods
await profileService.getAllProfiles();  // PROFILE module, returns all profiles
```

### 5. Easier Testing

**Before:**
```typescript
// Hard to mock photinoService for specific modules
jest.mock('../../../shared/services/photino');
```

**After:**
```typescript
// Mock individual module services
jest.mock('../services/modService');
jest.mock('../services/profileService');

// Or create test implementations
class MockModService extends BaseModuleService {
  constructor() {
    super('MOD');
  }
  async getAllMods() {
    return mockMods;
  }
}
```

## Implementation Examples

### ModService

**File:** [modService.ts](../../D3dxSkinManager.Client/src/modules/mods/services/modService.ts)

```typescript
export class ModService extends BaseModuleService {
  constructor() {
    super('MOD');  // Fixed module name
  }

  async getAllMods(): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_ALL');
  }

  async loadMod(sha: string): Promise<boolean> {
    return this.sendBooleanMessage('LOAD', { sha });
  }

  async getModBySha(sha: string): Promise<ModInfo | null> {
    return this.sendNullableMessage<ModInfo>('GET_BY_SHA', { sha });
  }

  async updateMetadata(sha: string, metadata: Partial<ModInfo>): Promise<boolean> {
    return this.sendBooleanMessage('UPDATE_METADATA', { sha, ...metadata });
  }
}

// Export singleton
export const modService = new ModService();
```

### ProfileService

**File:** [profileService.ts](../../D3dxSkinManager.Client/src/modules/profiles/services/profileService.ts)

```typescript
class ProfileService extends BaseModuleService {
  constructor() {
    super('PROFILE');  // Fixed module name
  }

  async getAllProfiles(): Promise<ProfileListResponse> {
    return this.sendMessage<ProfileListResponse>('GET_ALL');
  }

  async getActiveProfile(): Promise<Profile | null> {
    return this.sendNullableMessage<Profile>('GET_ACTIVE');
  }

  async createProfile(request: CreateProfileRequest): Promise<Profile> {
    return this.sendMessage<Profile>('CREATE', request);
  }

  async switchProfile(profileId: string): Promise<ProfileSwitchResult> {
    return this.sendMessage<ProfileSwitchResult>('SWITCH', { profileId });
  }

  // Helper methods can be included
  formatBytes(bytes: number): string {
    // ... formatting logic
  }
}

// Export singleton
export const profileService = new ProfileService();
```

### CacheService

**File:** [cacheService.ts](../../D3dxSkinManager.Client/src/modules/tools/services/cacheService.ts)

```typescript
export class CacheService extends BaseModuleService {
  constructor() {
    super('TOOLS');  // Part of TOOLS module
  }

  async scanCache(): Promise<CacheItem[]> {
    return this.sendArrayMessage<CacheItem>('SCAN_CACHE');
  }

  async getStatistics(): Promise<CacheStatistics> {
    return this.sendMessage<CacheStatistics>('GET_CACHE_STATISTICS');
  }

  async cleanCache(category: CacheCategory): Promise<number> {
    return this.sendMessage<number>('CLEAN_CACHE', { category });
  }

  async deleteCacheItem(sha: string): Promise<boolean> {
    return this.sendBooleanMessage('DELETE_CACHE_ITEM', { sha });
  }

  // Utility methods
  formatBytes(bytes: number): string {
    // ... formatting logic
  }
}

export const cacheService = new CacheService();
```

## Migration Guide

### Before (Direct PhotinoService)

```typescript
import { photinoService } from '../../../shared/services/photino';

// Component or hook
const MyComponent = () => {
  const [mods, setMods] = useState<ModInfo[]>([]);

  useEffect(() => {
    const loadMods = async () => {
      const result = await photinoService.sendMessage<ModInfo[]>('MOD', 'GET_ALL');
      setMods(result);
    };
    loadMods();
  }, []);

  const handleLoadMod = async (sha: string) => {
    await photinoService.sendMessage<boolean>('MOD', 'LOAD', { sha });
  };

  // ...
};
```

### After (Module Service)

```typescript
import { modService } from '../services/modService';

// Component or hook
const MyComponent = () => {
  const [mods, setMods] = useState<ModInfo[]>([]);

  useEffect(() => {
    const loadMods = async () => {
      const result = await modService.getAllMods();  // Cleaner!
      setMods(result);
    };
    loadMods();
  }, []);

  const handleLoadMod = async (sha: string) => {
    await modService.loadMod(sha);  // More readable!
  };

  // ...
};
```

## Adding a New Module Service

### Step 1: Create Service Class

```typescript
// src/modules/newmodule/services/newModuleService.ts
import { BaseModuleService } from '../../../shared/services/baseModuleService';

export class NewModuleService extends BaseModuleService {
  constructor() {
    super('NEWMODULE');  // Match backend module name
  }

  async getSomething(): Promise<SomeType> {
    return this.sendMessage<SomeType>('GET_SOMETHING');
  }

  async doAction(param: string): Promise<boolean> {
    return this.sendBooleanMessage('DO_ACTION', { param });
  }
}

// Export singleton
export const newModuleService = new NewModuleService();
```

### Step 2: Use in Components

```typescript
import { newModuleService } from '../services/newModuleService';

const result = await newModuleService.getSomething();
await newModuleService.doAction('value');
```

## Convenience Method Guidelines

Use the appropriate convenience method based on return type:

### sendArrayMessage\<T\>

For operations that return arrays:
```typescript
async getAll(): Promise<Item[]> {
  return this.sendArrayMessage<Item>('GET_ALL');
}
```

### sendBooleanMessage

For operations that return success/failure:
```typescript
async delete(id: string): Promise<boolean> {
  return this.sendBooleanMessage('DELETE', { id });
}
```

### sendNullableMessage\<T\>

For operations that might not find a result:
```typescript
async getById(id: string): Promise<Item | null> {
  return this.sendNullableMessage<Item>('GET_BY_ID', { id });
}
```

### sendMessage\<T\>

For operations with custom return types:
```typescript
async getStatistics(): Promise<Statistics> {
  return this.sendMessage<Statistics>('GET_STATISTICS');
}
```

## Service Organization

### Option 1: Single Service Per Module

```
modules/mods/services/
  └── modService.ts          (all mod operations)
```

### Option 2: Multiple Services Per Module (if needed)

```
modules/tools/services/
  ├── toolsService.ts        (aggregator/index)
  ├── cacheService.ts        (cache operations)
  ├── d3dMigotoService.ts    (d3dmigoto operations)
  └── validationService.ts   (validation operations)
```

Both approaches are valid. Use multiple services when a module has distinct sub-domains.

## Testing

### Unit Testing a Service

```typescript
import { ModService } from './modService';
import { photinoService } from '../../../shared/services/photino';

jest.mock('../../../shared/services/photino');

describe('ModService', () => {
  let service: ModService;

  beforeEach(() => {
    service = new ModService();
  });

  it('should get all mods', async () => {
    const mockMods: ModInfo[] = [/* ... */];
    (photinoService.sendMessage as jest.Mock).mockResolvedValue(mockMods);

    const result = await service.getAllMods();

    expect(photinoService.sendMessage).toHaveBeenCalledWith('MOD', 'GET_ALL', undefined);
    expect(result).toEqual(mockMods);
  });

  it('should load a mod', async () => {
    (photinoService.sendMessage as jest.Mock).mockResolvedValue(true);

    const result = await service.loadMod('abc123');

    expect(photinoService.sendMessage).toHaveBeenCalledWith('MOD', 'LOAD', { sha: 'abc123' });
    expect(result).toBe(true);
  });
});
```

### Integration Testing

```typescript
// Test with real photinoService in dev mode
describe('ModService Integration', () => {
  it('should communicate with backend', async () => {
    const mods = await modService.getAllMods();
    expect(Array.isArray(mods)).toBe(true);
  });
});
```

## Comparison: Before vs After

### Code Clarity

| Aspect | Before | After |
|--------|--------|-------|
| Module identification | String literal `'MOD'` | Service class `modService` |
| Return type | Manual annotation | Inferred from method |
| Method discovery | None (IDE can't help) | Full IDE autocomplete |
| Documentation | Comments only | TSDoc + type signatures |

### Type Safety

| Scenario | Before | After |
|----------|--------|-------|
| Wrong module name | Runtime error | Compile error (if typed) |
| Wrong message type | Runtime error | IDE warning |
| Wrong payload shape | Runtime error | Type error |
| Wrong return type | Silent bug | Type error |

### Maintainability

| Task | Before | After |
|------|--------|-------|
| Find all MOD operations | Search for `'MOD'` | Look at ModService class |
| Change module name | Find/replace everywhere | Change in one place |
| Add new operation | Add function + call site | Add method to service |
| Refactor return type | Change all call sites | Change method signature |

## Summary

**Created:**
- ✅ BaseModuleService abstract class
- ✅ 7 module service implementations

**Benefits:**
- ✅ Type safety - module names encapsulated
- ✅ Encapsulation - repeated code eliminated
- ✅ Discoverability - IDE autocomplete works
- ✅ Maintainability - single place to update
- ✅ Testability - easy to mock
- ✅ Documentation - self-documenting code

**Architecture:**
```
Component/Hook
    ↓
Module Service (ModService, ProfileService, etc.)
    ↓
BaseModuleService (encapsulates module name)
    ↓
PhotinoService (low-level IPC)
    ↓
Backend AppFacade → Module Facade → Service
```

The frontend now mirrors the backend's module-based architecture, providing a consistent and type-safe way to communicate across the IPC boundary! 🚀
