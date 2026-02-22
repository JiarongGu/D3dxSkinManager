# AI Assistant Guidelines

> **🤖 FOR AI ASSISTANTS:** Critical coding patterns, best practices, and common mistakes to avoid.

**Purpose:** Prevent common errors and establish consistent coding patterns.

**Last Updated:** 2026-02-19

---

## Table of Contents

1. [Critical DO's and DON'Ts](#critical-dos-and-donts)
2. [Backend (.NET/C#) Guidelines](#backend-netc-guidelines)
3. [Frontend (React/TypeScript) Guidelines](#frontend-reacttypescript-guidelines)
4. [Database Guidelines](#database-guidelines)
5. [IPC Communication Guidelines](#ipc-communication-guidelines)
6. [Documentation Guidelines](#documentation-guidelines)
7. [Common Mistakes to Avoid](#common-mistakes-to-avoid)

---

## Critical DO's and DON'Ts

### ✅ ALWAYS DO

1. **Ask before committing**
   - NEVER run `git commit` without explicit user approval
   - ALWAYS check current branch with `git branch`
   - ALWAYS create feature branches for changes

2. **Test before committing**
   - Run `dotnet build` for backend changes
   - Run `npm run build` for frontend changes
   - Test the application actually works

3. **Update documentation**
   - Update `docs/CHANGELOG.md` for all changes
   - Update `docs/KEYWORDS_INDEX.md` for new files
   - Create feature docs for new features

4. **Use proper types**
   - TypeScript: Avoid `any`, use explicit interfaces
   - C#: Use nullable reference types, avoid non-null assertions

5. **Handle errors properly**
   - Use try-catch blocks
   - Provide user-friendly error messages
   - Log errors for debugging

### ❌ NEVER DO

1. **Never commit without permission**
   - Don't create commits automatically
   - Don't push to main branch directly

2. **Never use `any` type (TypeScript)**
   ```typescript
   // ❌ Bad
   const data: any = await fetchData();

   // ✅ Good
   interface DataResponse {
     id: string;
     name: string;
   }
   const data: DataResponse = await fetchData();
   ```

3. **Never ignore exceptions**
   ```csharp
   // ❌ Bad
   try {
       await DoSomething();
   } catch { }

   // ✅ Good
   try {
       await DoSomething();
   } catch (Exception ex) {
       Console.WriteLine($"Error: {ex.Message}");
       // Handle or rethrow
   }
   ```

4. **Never use synchronous I/O**
   ```csharp
   // ❌ Bad
   var data = File.ReadAllText(path);

   // ✅ Good
   var data = await File.ReadAllTextAsync(path);
   ```

5. **Never manipulate DOM directly (React)**
   ```typescript
   // ❌ Bad
   document.getElementById('myElement').innerHTML = 'text';

   // ✅ Good
   const [text, setText] = useState('');
   <div>{text}</div>
   ```

---

## Backend (.NET/C#) Guidelines

### Service Architecture

#### ✅ DO: Use Interfaces
```csharp
// Define interface
public interface IModService
{
    Task<List<ModInfo>> GetAllModsAsync();
    Task<bool> LoadModAsync(string sha);
}

// Implement interface
public class ModService : IModService
{
    public async Task<List<ModInfo>> GetAllModsAsync()
    {
        // Implementation
    }
}
```

#### ✅ DO: Use Async/Await
```csharp
// All I/O operations should be async
public async Task<bool> LoadModAsync(string sha)
{
    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = "UPDATE Mods SET IsLoaded = 1 WHERE SHA = @sha";
    command.Parameters.AddWithValue("@sha", sha);

    var affected = await command.ExecuteNonQueryAsync();
    return affected > 0;
}
```

#### ✅ DO: Use `using` for IDisposable
```csharp
using var connection = new SqliteConnection(connectionString);
await connection.OpenAsync();
// Connection automatically disposed
```

#### ❌ DON'T: Use non-null assertion without checking
```csharp
// ❌ Bad
var result = someNullableValue!.Property;

// ✅ Good
if (someNullableValue != null)
{
    var result = someNullableValue.Property;
}

// ✅ Also Good (pattern matching)
if (someNullableValue is not null)
{
    var result = someNullableValue.Property;
}
```

### Exception Handling

#### ✅ DO: Catch Specific Exceptions
```csharp
try
{
    await modService.LoadModAsync(sha);
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"Mod file not found: {ex.Message}");
}
catch (SqliteException ex)
{
    Console.WriteLine($"Database error: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
    throw; // Rethrow if can't handle
}
```

### Naming Conventions

- **PascalCase**: Classes, Methods, Properties, Public Fields
- **camelCase**: Local variables, Parameters, Private fields (with `_` prefix)
- **UPPER_CASE**: Constants

```csharp
public class ModService  // PascalCase
{
    private readonly string _connectionString;  // camelCase with underscore
    private const int MAX_RETRIES = 3;  // UPPER_CASE

    public async Task<bool> LoadModAsync(string modSha)  // PascalCase method, camelCase param
    {
        var localVariable = "value";  // camelCase
        return true;
    }
}
```

---

## Frontend (React/TypeScript) Guidelines

### Component Structure

#### ✅ DO: Use Functional Components with Hooks
```typescript
import React, { useState, useEffect } from 'react';

interface MyComponentProps {
  title: string;
  onAction: (id: string) => void;
}

const MyComponent: React.FC<MyComponentProps> = ({ title, onAction }) => {
  const [data, setData] = useState<string[]>([]);

  useEffect(() => {
    // Load data
  }, []);

  return (
    <div>
      <h1>{title}</h1>
      {/* Component content */}
    </div>
  );
};

export default MyComponent;
```

#### ❌ DON'T: Use Class Components
```typescript
// ❌ Avoid class components
class MyComponent extends React.Component {
  // ...
}
```

### Type Safety

#### ✅ DO: Define Interfaces for All Data
```typescript
// Define interface
interface ModInfo {
  sha: string;
  objectName: string;
  name: string;
  isLoaded: boolean;
}

// Use interface
const [mods, setMods] = useState<ModInfo[]>([]);

// Function parameters
const loadMod = async (mod: ModInfo): Promise<void> => {
  // Implementation
};
```

#### ❌ DON'T: Use `any`
```typescript
// ❌ Bad
const data: any = await fetch();

// ✅ Good
interface ApiResponse {
  success: boolean;
  data: ModInfo[];
}
const response: ApiResponse = await fetch();
```

#### ✅ DO: Use Generic Types for IPC Messages ⭐ NEW (2026-02-19, Updated 2026-02-22)
```typescript
// Define generic message types
interface IpcRequest<TPayload = unknown> {
  id: string;
  module: ModuleName;  // Union type, not string
  type: MessageType;
  profileId?: string;
  payload?: TPayload;  // Type-safe payload
}

interface IpcResponse<TData = unknown> {
  id: string;
  success: boolean;
  data?: TData;  // Type-safe response data
  error?: string;
}

// Use in service methods
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

// Example usage with specific types
async createProfile(request: CreateProfileRequest): Promise<Profile> {
  return this.sendMessage<Profile, CreateProfileRequest>('CREATE', undefined, request);
}
```

**Key Points:**
- Use `unknown` as default generic parameter for backward compatibility
- Define specific types when available (e.g., `CreateProfileRequest`)
- ModuleName should be union type: `'MOD' | 'PROFILE' | ...`
- Never use `any` for generic parameters

### State Management

#### ✅ DO: Keep State Minimal and Derived
```typescript
// ✅ Good - Keep only source data
const [mods, setMods] = useState<ModInfo[]>([]);

// Derive computed values
const loadedMods = mods.filter(m => m.isLoaded);
const modCount = mods.length;
```

#### ❌ DON'T: Duplicate State
```typescript
// ❌ Bad - Duplicated/derived state
const [mods, setMods] = useState<ModInfo[]>([]);
const [loadedMods, setLoadedMods] = useState<ModInfo[]>([]);  // Redundant
const [modCount, setModCount] = useState(0);  // Derived
```

### Error Handling ⭐ UPDATED (2026-02-19)

#### ✅ DO: Use Standardized Error Handling Pattern
```typescript
const loadMod = async (sha: string) => {
  try {
    await modService.loadMod(sha);
    message.success('Mod loaded successfully');
    await refreshMods();
  } catch (error: unknown) {
    // ALWAYS use 'error: unknown' and type guard
    const errorMessage = error instanceof Error ? error.message : 'An unexpected error occurred';
    message.error(`Failed to load mod: ${errorMessage}`);
    console.error('Load mod error:', error);
  }
};
```

#### ❌ DON'T: Use Loose Error Typing
```typescript
// ❌ Bad - Uses 'any' type
catch (error: any) {
  message.error(error.message);
}

// ❌ Bad - Implicit 'any'
catch (error) {
  message.error(error.message);
}

// ❌ Bad - Assumes error is Error type
catch (error: Error) {
  message.error(error.message);
}

// ✅ Good - Type-safe with guard
catch (error: unknown) {
  const errorMessage = error instanceof Error ? error.message : 'Unknown error';
  message.error(errorMessage);
}
```

#### ✅ DO: Silent Handling for Expected Errors
```typescript
try {
  const config = await profileService.getProfileConfig(profileId);
} catch (error: unknown) {
  const errorMessage = error instanceof Error ? error.message : '';
  // Only show error if it's not an expected condition
  if (!errorMessage.includes('Profile ID is required')) {
    message.error('Failed to load profile configuration');
    console.error('Failed to load profile config:', error);
  }
}
```

### Theme and Styling ⭐ NEW (2026-02-18)

#### ✅ DO: Use CSS Variables for Colors

```typescript
// ✅ Good - Uses theme-aware CSS variables
<div style={{
  background: 'var(--color-bg-container)',
  color: 'var(--color-text-base)',
  border: '1px solid var(--color-border-secondary)'
}}>
  Content
</div>

// Status colors
<CheckCircleOutlined style={{ color: 'var(--color-success)' }} />
<ExclamationCircleOutlined style={{ color: 'var(--color-error)' }} />
```

#### ❌ DON'T: Hardcode Colors

```typescript
// ❌ Bad - Hardcoded colors break dark theme
<div style={{
  background: '#ffffff',
  color: '#000000',
  border: '1px solid #d9d9d9'
}}>
  Content
</div>
```

#### Color Selection Guide

| Use Case | CSS Variable |
|----------|-------------|
| Card background | `var(--color-card-bg)` |
| Primary text | `var(--color-text-base)` |
| Secondary text | `var(--color-text-secondary)` |
| Muted text | `var(--color-text-tertiary)` |
| Borders | `var(--color-border-secondary)` |
| Success status | `var(--color-success)` |
| Error status | `var(--color-error)` |
| Warning status | `var(--color-warning)` |
| Info background | `var(--color-info-bg)` |
| Sidebar | `var(--color-sider-bg)` |

#### ✅ DO: Use Theme Hook When Needed

```typescript
import { useTheme } from '../shared/context/ThemeContext';

function MyComponent() {
  const { theme, effectiveTheme, setTheme } = useTheme();

  // Use effectiveTheme for conditional logic
  const isDark = effectiveTheme === 'dark';

  return <div>Theme: {effectiveTheme}</div>;
}
```

#### ✅ DO: Test Both Themes

Before committing UI changes:
1. Test in light theme
2. Test in dark theme
3. Verify text readability
4. Check border visibility
5. Ensure status colors are clear

**Reference:** See [THEME_SYSTEM.md](../features/THEME_SYSTEM.md) for complete guide

### UI Components ⭐ NEW (2026-02-19)

#### ✅ DO: Use Compact Components

```typescript
// ✅ Good - Uses Compact components for consistency
import { CompactButton, CompactCard, CompactSpace } from '../../../shared/components/compact';

<CompactCard>
  <CompactSpace direction="vertical">
    <CompactButton type="primary" onClick={handleSave}>
      Save
    </CompactButton>
    <CompactButton onClick={handleCancel}>
      Cancel
    </CompactButton>
  </CompactSpace>
</CompactCard>
```

#### ❌ DON'T: Use Direct Ant Design Components for UI Elements

```typescript
// ❌ Bad - Direct Ant Design components cause inconsistent sizing
import { Button, Card, Space } from 'antd';

<Card>
  <Space>
    <Button type="primary">Save</Button>
    <Button>Cancel</Button>
  </Space>
</Card>
```

**Available Compact Components:**
- `CompactButton` - Buttons with consistent sizing
- `CompactCard` - Cards with proper spacing
- `CompactSpace` - Layout spacing
- `CompactDivider` - Section dividers
- `CompactText` - Typography
- `CompactAlert` - Alerts and notifications
- `CompactSection` - Page sections

**When to Use:**
- ✅ Use Compact components for all buttons, cards, and layout elements
- ✅ Import from `shared/components/compact`
- ❌ Don't use direct Ant Design components for these elements
- ✅ Ant Design components like Form, Input, Select are still used directly

**Location:** `D3dxSkinManager.Client/src/shared/components/compact/`

---

## Database Guidelines

### Parameterized Queries

#### ✅ DO: Always Use Parameters
```csharp
// ✅ Good - Prevents SQL injection
command.CommandText = "SELECT * FROM Mods WHERE SHA = @sha";
command.Parameters.AddWithValue("@sha", sha);
```

#### ❌ DON'T: Concatenate SQL Strings
```csharp
// ❌ Bad - SQL injection risk
command.CommandText = $"SELECT * FROM Mods WHERE SHA = '{sha}'";
```

### Database Schema Changes

When modifying the database schema:

1. Update `InitializeDatabaseAsync()` in `ModService.cs`
2. Consider migration strategy for existing databases
3. Update `ModInfo` class to match schema
4. Update all queries using affected columns
5. Document changes in CHANGELOG.md

---

## IPC Communication Guidelines

### Message Structure (Updated 2026-02-22 for WebView2)

#### ✅ DO: Use Typed Messages
```typescript
// Frontend
interface IpcRequest<TPayload = unknown> {
  id: string;
  module: ModuleName;
  type: MessageType;
  profileId?: string;
  payload?: TPayload;
}

// Backend
public class IpcRequest
{
    public string Id { get; set; }
    public string Module { get; set; }
    public string Type { get; set; }
    public string? ProfileId { get; set; }
    public JsonElement? Payload { get; set; }
}
```

### Adding New IPC Message Types

1. **Add type to TypeScript:**
   ```typescript
   // src/shared/types/message.types.ts
   export type MessageType =
     | 'LOAD_MOD'
     | 'UNLOAD_MOD'
     | 'YOUR_NEW_TYPE';  // Add here
   ```

2. **Handle in backend facade:**
   ```csharp
   // Modules/YourModule/YourModuleFacade.cs
   public override async Task<object?> HandleMessageAsync(string type, JsonElement? payload)
   {
       return type switch
       {
           "YOUR_NEW_TYPE" => await YourMethod(payload),
           _ => throw new InvalidOperationException($"Unknown message type: {type}")
       };
   }
   ```

3. **Create frontend module service method:**
   ```typescript
   // modules/yourModule/services/yourModuleService.ts
   class YourModuleService extends BaseModuleService {
     constructor() { super('YOUR_MODULE'); }

     async yourMethod(param: string): Promise<Result> {
       return this.sendMessage<Result>('YOUR_NEW_TYPE', undefined, { param });
     }
   }
   ```

---

## Documentation Guidelines

### When to Create Documentation

| Trigger | Action | File |
|---------|--------|------|
| New feature | Create feature doc | `docs/features/FEATURE_NAME.md` |
| New class/service | Update keywords | `docs/KEYWORDS_INDEX.md` |
| Bug fix | Update changelog | `docs/CHANGELOG.md` |
| Found issue after >5 min | Update troubleshooting | `docs/ai-assistant/TROUBLESHOOTING.md` |
| Discovered pattern | Update this file | `docs/ai-assistant/GUIDELINES.md` |

### Documentation Format

```markdown
# Feature Name

**Purpose:** One sentence description
**Location:** `path/to/file.cs:lineNumber`
**Status:** ✅ Complete / ⏳ In Progress / 📋 Planned

## Overview

Brief description of what this feature does.

## Usage

Code examples showing how to use the feature.

## Implementation Details

Technical details, architecture decisions.

## Related Files

- `file1.cs` - Description
- `file2.tsx` - Description
```

---

## Common Mistakes to Avoid

### 1. Forgetting to Update Documentation

❌ **Mistake:** Making changes without updating docs

✅ **Solution:** Update docs in same commit as code changes

```bash
# Before committing
1. Update code
2. Update docs/CHANGELOG.md
3. Update docs/KEYWORDS_INDEX.md (if new files)
4. Ask user for commit approval
```

### 2. Using Wrong Namespace

❌ **Mistake:** Using old namespace `D3dxSkinManage.App`

✅ **Solution:** Use correct namespace `D3dxSkinManager`

### 3. Not Testing Before Commit

❌ **Mistake:** Committing without building

✅ **Solution:** Always build before asking to commit

```bash
# Backend
cd D3dxSkinManager
dotnet build

# Frontend
cd D3dxSkinManager.Client
npm run build
```

### 4. Ignoring TypeScript Errors

❌ **Mistake:** Using `@ts-ignore` or `any` to suppress errors

✅ **Solution:** Fix the actual type issues

```typescript
// ❌ Bad
// @ts-ignore
const result = someFunction();

// ✅ Good
interface Result {
  id: string;
  name: string;
}
const result: Result = someFunction();
```

### 5. Not Checking Git Branch

❌ **Mistake:** Committing to main branch

✅ **Solution:** Always check and use feature branches

```bash
# Before ANY commit
git branch  # Verify not on main

# If on main, create branch
git checkout -b feature/your-feature-name
```

---

## Code Review Checklist

Before asking for commit approval, verify:

### Backend Changes
- [ ] All methods are async where appropriate
- [ ] Using statements for IDisposable resources
- [ ] Proper exception handling
- [ ] XML documentation for public APIs
- [ ] Builds without errors: `dotnet build`

### Frontend Changes
- [ ] All types defined (no `any`)
- [ ] Functional components with hooks
- [ ] Proper error handling with user feedback
- [ ] No direct DOM manipulation
- [ ] Builds without errors: `npm run build`

### Database Changes
- [ ] Parameterized queries (no SQL injection)
- [ ] Schema changes documented
- [ ] Migration strategy considered

### Documentation Changes
- [ ] CHANGELOG.md updated
- [ ] KEYWORDS_INDEX.md updated (if new files)
- [ ] Feature docs created/updated

### Git
- [ ] On correct branch (not main)
- [ ] Descriptive commit message ready
- [ ] User approval requested

---

*This guide is maintained by AI assistants. Update it when you discover new patterns!*

*Last updated: 2026-02-17*
