---
name: ipc-service
description: Use when adding frontend TypeScript methods that call backend IPC endpoints. Generates typed IPC service class extending BaseModuleService with singleton export.
---

# Frontend IPC Service Generator

Generate a TypeScript IPC service that communicates with backend facades.

## Arguments

**Format**: `/ipc-service <ServiceName> <Module> <Methods>`

**Example**:
```
/ipc-service TextureService MOD getTextures,validateTexture,importTexture
```

**Parameters**:
- `ServiceName` - Name of the service class (e.g., TextureService)
- `Module` - Module constant name (e.g., MOD, PROFILE, WORKFLOW - must match Module enum)
- `Methods` - Comma-separated list of method names (e.g., getTextures,validateTexture)

## What This Skill Generates

1. **Service file** (`{serviceName}.ts` - camelCase)
   - Class extending BaseModuleService
   - Constructor passing module name to base
   - Type-safe methods using generics
   - Singleton instance export
   - Proper imports from shared types

2. **Updates index.ts** (adds export)
   - Export the service singleton
   - Add to consolidated `api` object

## Pattern to Follow

```typescript
// Location: shared/services/ipc/{serviceName}.ts

import { BaseModuleService } from '../baseModuleService';
import type { TypeFromShared } from '@/shared/types/moduleTypes';

/**
 * IPC service for {module} operations
 */
export class {ServiceName} extends BaseModuleService {
  constructor() {
    super('{MODULE}');  // module name string, e.g. 'MOD', 'TOOL', 'LAUNCH'
  }

  /**
   * {Method description}
   * Backend: {BackendFacade}.Handle{Method}Async
   */
  async methodName(profileId: string, params: ParamType): Promise<ReturnType> {
    return this.sendMessage<ReturnType>('MESSAGE_TYPE', profileId, params);
  }

  /**
   * {Method description} (returns array)
   * Backend: {BackendFacade}.Handle{Method}Async
   */
  async getMultiple(profileId: string, params: ParamType): Promise<ReturnType[]> {
    return this.sendArrayMessage<ReturnType>('MESSAGE_TYPE', profileId, params);
  }

  /**
   * {Method description} (no return value)
   * Backend: {BackendFacade}.Handle{Method}Async
   */
  async executeAction(profileId: string, params: ParamType): Promise<void> {
    await this.sendMessage<void>('MESSAGE_TYPE', profileId, params);
  }
}

// Export singleton instance
export const {serviceName} = new {ServiceName}();
```

## BaseModuleService Methods

**Preferred (typed):** `sendTypedMessage` / `sendTypedBoolean` / `sendTypedArray` / `sendTypedOptional`
— generic over a `TRequests` map type so the message type + payload are checked at compile time
(see `shared/types/ipc/modIpcRequests.ts` for the request-map pattern).

**Legacy (still common, marked `@deprecated`):** `sendMessage<T>` / `sendBooleanMessage` /
`sendArrayMessage<T>` / `sendOptionalMessage<T>`. There is NO `sendGlobalMessage` — global
operations simply omit `profileId` (pass `undefined`).

Pick by return type:

- **`sendMessage<T>(type, profileId, payload)`** - Returns single object or void
  ```typescript
  async getMod(profileId: string, id: string): Promise<ModInfo> {
    return this.sendMessage<ModInfo>('GET_BY_ID', profileId, { id });
  }
  ```

- **`sendArrayMessage<T>(type, profileId, payload)`** - Returns array
  ```typescript
  async getAllMods(profileId: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_ALL', profileId);
  }
  ```

- **Global operation** - omit profileId
  ```typescript
  async getGlobalSettings(): Promise<Settings> {
    return this.sendMessage<Settings>('GET_GLOBAL');
  }
  ```

## Steps to Execute

1. **Find or create service file**:
   - Path: `D3dxSkinManager.Client/src/shared/services/ipc/{serviceName}.ts` (camelCase)
   - If file exists, add new methods to existing class
   - If new file, create with full pattern

2. **Create class structure**:
   - Import BaseModuleService from `'../baseModuleService'`
   - Import necessary types from shared/types
   - Class extends BaseModuleService
   - Constructor calls super with the module name string (e.g. `super('MOD')`)

3. **Generate methods**:
   - For each method in the list:
     - Determine return type (single object, array, or void)
     - Use appropriate sendMessage variant
     - Add JSDoc comment with backend facade reference
     - Use proper TypeScript types for parameters and return

4. **Export singleton**:
   - After class definition: `export const {serviceName} = new {ServiceName}();`
   - Use camelCase for instance name (e.g., `modService`, `textureService`)

5. **Update index.ts**:
   - Path: `D3dxSkinManager.Client/src/shared/services/ipc/index.ts`
   - Add export: `export { {serviceName} } from './{serviceName}';`
   - Add to `api` object: `{serviceName},`

6. **Verify imports**:
   - Check if types exist in `shared/types/`
   - If types missing, suggest creating them or using `any` temporarily

## Example Output

For: `/ipc-service TextureService MOD getTextures,validateTexture`

Creates/Updates:
- `shared/services/ipc/textureService.ts`
- `shared/services/ipc/index.ts` (adds export)

```typescript
// textureService.ts
import { BaseModuleService } from '../baseModuleService';
import type { TextureInfo } from '@/shared/types/modTypes';

export class TextureService extends BaseModuleService {
  constructor() {
    super('MOD');
  }

  /**
   * Get all textures for a mod
   * Backend: ModFacade.HandleGetTexturesAsync
   */
  async getTextures(profileId: string, modId: string): Promise<TextureInfo[]> {
    return this.sendArrayMessage<TextureInfo>('GET_TEXTURES', profileId, { modId });
  }

  /**
   * Validate texture file format
   * Backend: ModFacade.HandleValidateTextureAsync
   */
  async validateTexture(
    profileId: string,
    filePath: string
  ): Promise<{ valid: boolean; error?: string }> {
    return this.sendMessage('VALIDATE_TEXTURE', profileId, { filePath });
  }
}

export const textureService = new TextureService();
```

## Message Type Naming Convention

Convert method names to UPPER_SNAKE_CASE for message types:

- `getMods` → `GET_MODS`
- `validateTexture` → `VALIDATE_TEXTURE`
- `importModFromArchive` → `IMPORT_MOD_FROM_ARCHIVE`
- `batchUpdateMetadata` → `BATCH_UPDATE_METADATA`

## Important Rules

- ✅ Extend BaseModuleService (not raw IPC)
- ✅ Use singleton export pattern (lowercase instance name)
- ✅ Use type-safe generics for all methods
- ✅ Add JSDoc comments referencing backend facade/method
- ✅ Use sendArrayMessage/sendTypedArray for array returns
- ✅ Use sendMessage/sendTypedMessage for single object or void
- ✅ Global operations omit profileId (there is NO sendGlobalMessage)
- ✅ Message types are UPPER_SNAKE_CASE
- ✅ Export both class and singleton instance
- ❌ Don't create new IPC mechanisms (use BaseModuleService)
- ❌ Don't call backend services directly (use IPC)
- ❌ Don't handle errors in service (let handleError utility handle it)

## Consolidated API Object

All IPC services are available via the consolidated `api` object:

```typescript
import { api } from '@/shared/services/ipc';

// Usage
const mods = await api.mod.getAllMods(profileId);
const textures = await api.texture.getTextures(profileId, modId);
```

## Reference Examples

Look at these existing services for patterns:
- `shared/services/ipc/modService.ts` - Service with multiple methods
- `shared/services/ipc/profileService.ts` - Global vs profile-scoped methods
- `shared/services/ipc/workflowService.ts` - Complex parameter types
- `shared/services/ipc/index.ts` - Export consolidation pattern
