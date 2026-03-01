# Workflows - Code Generation Patterns

**Last Updated:** 2026-02-23
**Purpose:** Essential patterns for creating new code components

---

## Adding Backend Service

### 1. Create Service Interface & Implementation

```csharp
// Location: D3dxSkinManager/Modules/{ModuleName}/Services/

// IYourService.cs
public interface IYourService
{
    Task<Result> DoSomethingAsync(string param);
}

// YourService.cs
public class YourService : IYourService
{
    private readonly ILogger _logger;
    private readonly IFileService _fileService;

    public YourService(ILogger logger, IFileService fileService)
    {
        _logger = logger;
        _fileService = fileService;
    }

    public async Task<Result> DoSomethingAsync(string param)
    {
        // Implementation
    }
}
```

### 2. Register in Module Extensions

```csharp
// Location: D3dxSkinManager/Modules/{ModuleName}/{ModuleName}ServiceExtensions.cs

public static IServiceCollection Add{ModuleName}Services(this IServiceCollection services)
{
    services.AddSingleton<IYourService, YourService>();
    return services;
}
```

### 3. Add to Facade (if IPC needed)

```csharp
// Location: D3dxSkinManager/Modules/{ModuleName}/{ModuleName}Facade.cs

case "DO_SOMETHING":
    var result = await _yourService.DoSomethingAsync(payload.GetString("param"));
    return MessageResponse.Success(result);
```

---

## Adding Frontend Service

### 1. Create Service Class

```typescript
// Location: D3dxSkinManager.Client/src/services/yourService.ts

import { BaseModuleService } from './baseModuleService';

class YourService extends BaseModuleService {
  constructor() {
    super('YOUR_MODULE'); // Module name from backend
  }

  async doSomething(param: string) {
    return this.sendMessage<Result>('DO_SOMETHING', { param });
  }
}

export const yourService = new YourService();
```

### 2. Add Types

```typescript
// Location: D3dxSkinManager.Client/src/types/your.types.ts

export interface YourData {
  id: string;
  // ... fields
}
```

---

## Adding React Component

### 1. Create Component

```tsx
// Location: D3dxSkinManager.Client/src/modules/{module}/components/YourComponent.tsx

import React, { FC, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CompactButton } from 'shared/components/compact';

interface YourComponentProps {
  onAction: () => void;
}

export const YourComponent: FC<YourComponentProps> = ({ onAction }) => {
  const { t } = useTranslation();
  const [data, setData] = useState<YourData>();

  useEffect(() => {
    // Load data
  }, []);

  return (
    <div>
      <CompactButton onClick={onAction}>
        {t('your.action.label')}
      </CompactButton>
    </div>
  );
};
```

### 2. Add to Context (if stateful)

```tsx
// Location: D3dxSkinManager.Client/src/modules/{module}/context/YourContext.tsx

const YourContext = createContext<YourContextValue | undefined>(undefined);

export const YourProvider: FC<{ children: React.ReactNode }> = ({ children }) => {
  const [state, setState] = useState<YourState>();

  return (
    <YourContext.Provider value={{ state, setState }}>
      {children}
    </YourContext.Provider>
  );
};

export const useYour = () => {
  const context = useContext(YourContext);
  if (!context) throw new Error('useYour must be used within YourProvider');
  return context;
};
```

---

## Adding IPC Message

### 1. Backend: Add to Facade

```csharp
// In {Module}Facade.cs
case "NEW_MESSAGE":
    var param = payload.GetString("param");
    var result = await _service.ProcessAsync(param);
    return MessageResponse.Success(result);
```

### 2. Frontend: Add to Service

```typescript
// In service class
async newMessage(param: string) {
  return this.sendMessage<Result>('NEW_MESSAGE', { param });
}
```

---

## Batch Operations Pattern

### When to Use
Use batch operations when users need to perform the same action on multiple items (delete, update, resume, etc.)

### 1. Backend: Repository Methods

```csharp
// In I{Entity}Repository.cs
Task<int> DeleteBatchAsync(IEnumerable<string> ids);
Task<List<TEntity>> GetByIdsAsync(IEnumerable<string> ids);

// In {Entity}Repository.cs
public async Task<int> DeleteBatchAsync(IEnumerable<string> ids)
{
    var idList = ids.ToList();
    if (idList.Count == 0) return 0;

    // Build parameterized SQL with IN clause
    var parameters = idList.Select((id, index) => $"@id{index}").ToList();
    var inClause = string.Join(",", parameters);

    var cmd = connection.CreateCommand();
    cmd.CommandText = $"DELETE FROM TableName WHERE Id IN ({inClause})";

    for (int i = 0; i < idList.Count; i++)
    {
        cmd.Parameters.AddWithValue($"@id{i}", idList[i]);
    }

    return await cmd.ExecuteNonQueryAsync();
}
```

### 2. Backend: Facade Handler with Cleanup

```csharp
// In {Module}Facade.cs
case "BATCH_DELETE_ITEMS":
    return await BatchDeleteItemsAsync(request);

private async Task<BatchOperationResult> BatchDeleteItemsAsync(IpcRequest request)
{
    var data = JsonHelper.Deserialize<BatchDeleteRequest>(request.Data);
    var result = new BatchOperationResult
    {
        TotalRequested = data.ItemIds.Count
    };

    foreach (var itemId in data.ItemIds)
    {
        try
        {
            // Optional: Call cleanup handlers before deletion
            await _handler.CleanupAsync(itemId);
            await _repository.DeleteAsync(itemId);
            result.Successful.Add(itemId);
        }
        catch (Exception ex)
        {
            result.Failed.Add(new FailedItem
            {
                ItemId = itemId,
                Error = ex.Message
            });
        }
    }

    return result;
}

// BatchOperationResult model
public class BatchOperationResult
{
    public int TotalRequested { get; set; }
    public List<string> Successful { get; set; } = new();
    public List<FailedItem> Failed { get; set; } = new();
}

public class FailedItem
{
    public string ItemId { get; set; }
    public string Error { get; set; }
}
```

### 3. Frontend: Service Methods

```typescript
// In service class (extends BaseModuleService)
export interface BatchOperationResult {
  totalRequested: number;
  successful: string[];
  failed: Array<{
    itemId: string;
    error: string;
  }>;
}

async batchDeleteItems(profileId: string, itemIds: string[]): Promise<BatchOperationResult> {
  return this.sendMessage<BatchOperationResult>('BATCH_DELETE_ITEMS', profileId, {
    itemIds,
  });
}

async batchResumeItems(profileId: string, itemIds: string[]): Promise<BatchOperationResult> {
  return this.sendMessage<BatchOperationResult>('BATCH_RESUME_ITEMS', profileId, {
    itemIds,
  });
}
```

### 4. Frontend: Component Usage

```typescript
const handleBatchDelete = async () => {
  if (!profileId || selectedIds.length === 0) return;

  try {
    const result = await service.batchDeleteItems(profileId, selectedIds);

    // Clear selection and refresh
    setSelectedIds([]);
    refresh();

    // Handle partial failures
    if (result.failed.length > 0) {
      console.warn(
        `Batch delete: ${result.successful.length} successful, ${result.failed.length} failed`,
        result.failed
      );
    }
  } catch (error) {
    handleError(error);
  }
};
```

### Key Principles
- ✅ Use parameterized SQL IN clauses (prevents SQL injection)
- ✅ Return detailed results (successful + failed items)
- ✅ Handle partial failures gracefully
- ✅ Call cleanup handlers before deletion
- ✅ Clear selection after successful operation
- ✅ Log partial failures for debugging

---

## Database Schema Changes

### 1. Modify Entity

```csharp
// In Models/{Entity}.cs
public class YourEntity
{
    public int Id { get; set; }
    public string NewField { get; set; } // Add new field
}
```

### 2. Create Migration

```bash
cd D3dxSkinManager
dotnet ef migrations add AddNewFieldToEntity
dotnet ef database update
```

---

## Testing Patterns

### Backend Unit Test

```csharp
public class YourServiceTests
{
    private readonly YourService _service;
    private readonly Mock<IFileService> _fileServiceMock;

    public YourServiceTests()
    {
        _fileServiceMock = new Mock<IFileService>();
        _service = new YourService(_fileServiceMock.Object);
    }

    [Fact]
    public async Task DoSomething_ValidInput_ReturnsSuccess()
    {
        // Arrange
        _fileServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DoSomethingAsync("test");

        // Assert
        result.Success.Should().BeTrue();
    }
}
```

### Frontend Component Test

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { YourComponent } from './YourComponent';

describe('YourComponent', () => {
  it('calls onAction when button clicked', () => {
    const onAction = jest.fn();
    render(<YourComponent onAction={onAction} />);

    fireEvent.click(screen.getByRole('button'));
    expect(onAction).toHaveBeenCalled();
  });
});
```

---

## Quick Command Reference

```bash
# Build
dotnet build                    # Backend
npm run build                   # Frontend

# Development
npm run dev                     # Start dev server with TypeScript checking
npx tsc --noEmit                # Manual TypeScript check

# Test
dotnet test                     # Backend tests
npm test                        # Frontend tests

# Database
dotnet ef migrations add {Name} # Create migration
dotnet ef database update       # Apply migrations

# Git (ALWAYS ask user first!)
git add -A                      # Stage all
git commit -m "message"         # Commit
git status                      # Check status
```

---

## TypeScript Best Practices

### React.useRef with Initial Value

```typescript
// ✅ CORRECT: Provide initial value for optional types
const draggedNodeKeyRef = React.useRef<string>(undefined);
const screenIdRef = useRef<string>(undefined);

// ❌ INCORRECT: Missing initial value
const draggedNodeKeyRef = React.useRef<string>();  // TS Error!
const screenIdRef = useRef<string | undefined>();  // Verbose
```

### Ref Callbacks with setState Functions

```typescript
// When using useDragDrop or custom hooks that return setState functions:

// ✅ CORRECT: Wrap setState in callback ref
const { containerRef } = useDragDrop(...handlers);
<div ref={(el) => containerRef(el || undefined)} />

// ❌ INCORRECT: Direct assignment (type mismatch)
<div ref={containerRef} />  // TS Error!
```

### Migration Wizard Updates

When updating migration options:
1. Update backend enum in `MigrationOptions.cs`
2. Update frontend enum in `migrationService.ts`
3. Update UI component in `OptionsStep.tsx`
4. Update i18n keys in `Languages/en.json` and `Languages/cn.json`
5. Update documentation in `MIGRATION_ARCHITECTURE.md`

### Vite TypeScript Checking

The dev server uses `vite-plugin-checker` to show TypeScript errors in real-time:

```typescript
// vite.config.ts
import checker from 'vite-plugin-checker';

export default defineConfig({
  plugins: [
    checker({
      typescript: true,
      overlay: {
        initialIsOpen: false,
        position: 'br',
      },
      enableBuild: false, // Only check during dev
    }),
  ],
});
```

Benefits:
- TypeScript errors shown in terminal during `npm run dev`
- Error overlay appears in browser (bottom-right)
- Catches type errors immediately without manual `tsc` runs

---

## Common Patterns

### Error Handling

```csharp
// Backend
try
{
    var result = await _service.ProcessAsync();
    return MessageResponse.Success(result);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process");
    return MessageResponse.Error($"Processing failed: {ex.Message}");
}
```

```typescript
// Frontend
try {
  const result = await service.process();
  if (result.success) {
    // Handle success
  } else {
    message.error(result.error || 'Unknown error');
  }
} catch (error) {
  console.error('Failed:', error);
  message.error('Operation failed');
}
```

### Progress Reporting

```csharp
// Backend - for operations >1 second
public async Task ProcessAsync(IProgress<int> progress)
{
    progress?.Report(0);
    // ... do work
    progress?.Report(50);
    // ... more work
    progress?.Report(100);
}
```

---

**Remember:** Focus on patterns, not verbose explanations. Code speaks louder than words.