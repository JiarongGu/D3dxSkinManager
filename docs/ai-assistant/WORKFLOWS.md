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