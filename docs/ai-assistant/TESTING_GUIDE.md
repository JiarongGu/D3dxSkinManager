# Testing Guide for D3dxSkinManager

> **🤖 FOR AI ASSISTANTS:** This guide ensures all code changes are properly tested before committing.

**Last Updated:** 2026-02-18

---

## Table of Contents

1. [Why Testing Matters](#why-testing-matters)
2. [Testing Infrastructure](#testing-infrastructure)
3. [Backend Testing (.NET/C#)](#backend-testing-netc)
4. [Frontend Testing (React/TypeScript)](#frontend-testing-reacttypescript)
5. [Testing Checklist](#testing-checklist)
6. [Common Testing Patterns](#common-testing-patterns)
7. [When to Write Tests](#when-to-write-tests)

---

## Why Testing Matters

**The Problem:** We've had multiple frontend issues because code wasn't properly tested:
- Theme loading timeouts on startup
- setState during render errors
- Missing formatted properties causing UI bugs
- LocalStorage fallbacks causing state sync issues

**The Solution:** Write tests for critical paths BEFORE bugs happen.

### Testing Priority Levels

**P0 (MUST TEST - Critical):**
- Settings loading/saving (backend & frontend)
- IPC communication (message routing)
- Context providers (Theme, Annotation, Profile)
- Data persistence services
- Migration wizard logic

**P1 (SHOULD TEST - Important):**
- UI components with complex state
- Form validation logic
- File system operations
- Archive extraction
- Mod loading/unloading

**P2 (NICE TO TEST - Enhancement):**
- Simple UI components
- Utility functions
- Formatting helpers

---

## Testing Infrastructure

### Backend (.NET 10 + xUnit)

**Location:** `D3dxSkinManager.Tests/`

**Framework Stack:**
- xUnit 2.9+ (test framework)
- FluentAssertions 8.8+ (assertions)
- Moq 4.20+ (mocking)
- Microsoft.NET.Test.Sdk 17.12+

**Run Tests:**
```bash
# Run all tests
dotnet test D3dxSkinManager.Tests/D3dxSkinManager.Tests.csproj

# Run with verbosity
dotnet test D3dxSkinManager.Tests/D3dxSkinManager.Tests.csproj -v normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~ClassificationServiceTests"

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### Frontend (React 18 + Jest + React Testing Library)

**Location:** `D3dxSkinManager.Client/src/`

**Framework Stack:**
- Jest 27+ (test framework)
- React Testing Library (component testing)
- @testing-library/jest-dom (assertions)
- @testing-library/user-event (user interactions)

**Run Tests:**
```bash
cd D3dxSkinManager.Client

# Run all tests
npm test

# Run in watch mode
npm test -- --watch

# Run with coverage
npm test -- --coverage

# Run specific test file
npm test -- ThemeContext.test.tsx

# Update snapshots
npm test -- -u
```

---

## Backend Testing (.NET/C#)

### Test File Structure

```
D3dxSkinManager.Tests/
├── Services/
│   ├── GlobalSettingsServiceTests.cs
│   ├── SettingsFileServiceTests.cs
│   ├── ClassificationServiceTests.cs
│   └── FileServiceTests.cs
├── Facades/
│   ├── SettingsFacadeTests.cs
│   └── ModFacadeTests.cs
└── Modules/
    └── Migration/
        └── MigrationServiceTests.cs
```

### Backend Test Template

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Settings.Services;
using D3dxSkinManager.Modules.Settings.Models;

namespace D3dxSkinManager.Tests.Modules.Settings;

/// <summary>
/// Unit tests for GlobalSettingsService
/// </summary>
public class GlobalSettingsServiceTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly GlobalSettingsService _service;

    public GlobalSettingsServiceTests()
    {
        // Arrange - Create temp directory for each test
        _testDataPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataPath);
        _service = new GlobalSettingsService(_testDataPath);
    }

    public void Dispose()
    {
        // Cleanup - Delete temp directory after test
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, recursive: true);
        }
    }

    [Fact]
    public async Task GetSettingsAsync_WhenFileDoesNotExist_ShouldCreateDefaultSettings()
    {
        // Act
        var settings = await _service.GetSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.Theme.Should().Be("light");
        settings.LogLevel.Should().Be("INFO");
        settings.AnnotationLevel.Should().Be("all");
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldPersistChanges()
    {
        // Arrange
        var newSettings = new GlobalSettings
        {
            Theme = "dark",
            LogLevel = "DEBUG",
            AnnotationLevel = "more"
        };

        // Act
        await _service.UpdateSettingsAsync(newSettings);
        var retrieved = await _service.GetSettingsAsync();

        // Assert
        retrieved.Theme.Should().Be("dark");
        retrieved.LogLevel.Should().Be("DEBUG");
        retrieved.AnnotationLevel.Should().Be("more");
    }

    [Fact]
    public async Task GetSettingsAsync_WhenCalledTwice_ShouldReturnCachedValue()
    {
        // Act
        var first = await _service.GetSettingsAsync();
        var second = await _service.GetSettingsAsync();

        // Assert
        first.Should().BeSameAs(second); // Same object reference = cached
    }

    [Theory]
    [InlineData("theme", "dark")]
    [InlineData("logLevel", "ERROR")]
    [InlineData("annotationLevel", "none")]
    public async Task UpdateSettingAsync_ShouldUpdateSingleField(string key, string value)
    {
        // Act
        await _service.UpdateSettingAsync(key, value);
        var settings = await _service.GetSettingsAsync();

        // Assert
        var property = typeof(GlobalSettings).GetProperty(
            key,
            System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        );
        property.Should().NotBeNull();
        var actualValue = property!.GetValue(settings) as string;
        actualValue.Should().Be(value);
    }

    [Fact]
    public async Task UpdateSettingAsync_WithInvalidKey_ShouldThrowArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateSettingAsync("invalidKey", "value"));
    }
}
```

### Backend Testing Best Practices

**1. Use IDisposable for Cleanup**
```csharp
public class MyTests : IDisposable
{
    private readonly string _tempPath;

    public MyTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, recursive: true);
    }
}
```

**2. Test Async Code Properly**
```csharp
// ✅ Good - Returns Task
[Fact]
public async Task MyTest_ShouldWork()
{
    var result = await MyService.DoSomethingAsync();
    result.Should().BeTrue();
}

// ❌ Bad - Blocks thread
[Fact]
public void MyTest_ShouldWork()
{
    var result = MyService.DoSomethingAsync().Result; // DON'T DO THIS
    result.Should().BeTrue();
}
```

**3. Mock Dependencies with Moq**
```csharp
[Fact]
public async Task ProcessData_ShouldCallRepository()
{
    // Arrange
    var mockRepo = new Mock<IDataRepository>();
    mockRepo.Setup(r => r.GetDataAsync()).ReturnsAsync(new Data());
    var service = new DataService(mockRepo.Object);

    // Act
    await service.ProcessDataAsync();

    // Assert
    mockRepo.Verify(r => r.GetDataAsync(), Times.Once);
}
```

**4. Use FluentAssertions for Readable Assertions**
```csharp
// ✅ Good - Fluent and readable
result.Should().NotBeNull();
result.Count.Should().Be(5);
result.Should().Contain(x => x.Name == "test");

// ❌ Bad - xUnit style (still works but less readable)
Assert.NotNull(result);
Assert.Equal(5, result.Count);
Assert.Contains(result, x => x.Name == "test");
```

---

## Frontend Testing (React/TypeScript)

### Test File Structure

```
D3dxSkinManager.Client/src/
├── shared/
│   ├── context/
│   │   ├── ThemeContext.test.tsx
│   │   └── __tests__/
│   │       └── ThemeContext.test.tsx
│   └── components/
│       └── common/
│           └── __tests__/
│               └── TooltipSystem.test.tsx
├── modules/
│   ├── settings/
│   │   ├── services/
│   │   │   ├── __tests__/
│   │   │   │   ├── settingsService.test.ts
│   │   │   │   └── settingsFileService.test.ts
│   │   └── components/
│   │       └── __tests__/
│   │           └── SettingsView.test.tsx
│   └── migration/
│       └── context/
│           └── __tests__/
│               └── MigrationWizardContext.test.tsx
└── setupTests.ts
```

### Frontend Test Template

```typescript
import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThemeProvider, useTheme } from '../ThemeContext';
import { settingsService } from '../../../modules/settings/services/settingsService';

// Mock the settings service
jest.mock('../../../modules/settings/services/settingsService');

describe('ThemeContext', () => {
  // Helper component to access context
  const TestComponent = () => {
    const { theme, effectiveTheme, setTheme } = useTheme();
    return (
      <div>
        <div data-testid="theme">{theme}</div>
        <div data-testid="effective-theme">{effectiveTheme}</div>
        <button onClick={() => setTheme('dark')}>Set Dark</button>
      </div>
    );
  };

  beforeEach(() => {
    // Reset mocks before each test
    jest.clearAllMocks();

    // Default mock implementation
    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'light',
      logLevel: 'INFO',
      annotationLevel: 'all'
    });
  });

  it('should load theme from backend on mount', async () => {
    // Arrange
    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'dark',
      logLevel: 'INFO',
      annotationLevel: 'all'
    });

    // Act
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('dark');
    });
  });

  it('should retry on failure with exponential backoff', async () => {
    // Arrange
    let callCount = 0;
    (settingsService.getGlobalSettings as jest.Mock).mockImplementation(() => {
      callCount++;
      if (callCount < 3) {
        return Promise.reject(new Error('Network error'));
      }
      return Promise.resolve({ theme: 'light', logLevel: 'INFO', annotationLevel: 'all' });
    });

    // Act
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    }, { timeout: 5000 });

    expect(callCount).toBe(3); // Tried 3 times before success
  });

  it('should update backend when theme changes', async () => {
    // Arrange
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    });

    // Act
    const button = screen.getByText('Set Dark');
    await userEvent.click(button);

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('dark');
    });

    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('theme', 'dark');
  });

  it('should rollback on save failure', async () => {
    // Arrange
    (settingsService.updateGlobalSetting as jest.Mock).mockRejectedValue(
      new Error('Save failed')
    );
    (settingsService.getGlobalSettings as jest.Mock)
      .mockResolvedValueOnce({ theme: 'light', logLevel: 'INFO', annotationLevel: 'all' })
      .mockResolvedValueOnce({ theme: 'light', logLevel: 'INFO', annotationLevel: 'all' });

    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    });

    // Act
    const button = screen.getByText('Set Dark');
    await userEvent.click(button);

    // Assert - Should rollback to 'light'
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    });
  });
});
```

### Frontend Testing Best Practices

**1. Mock External Dependencies**
```typescript
// Mock Photino service
jest.mock('../../shared/services/photinoService', () => ({
  photinoService: {
    sendMessage: jest.fn(),
  },
}));

// Mock settings service
jest.mock('../../modules/settings/services/settingsService');
```

**2. Use waitFor for Async Operations**
```typescript
// ✅ Good - Waits for async update
await waitFor(() => {
  expect(screen.getByText('Loaded')).toBeInTheDocument();
});

// ❌ Bad - Doesn't wait
expect(screen.getByText('Loaded')).toBeInTheDocument(); // Will fail if async
```

**3. Test User Interactions**
```typescript
import userEvent from '@testing-library/user-event';

// ✅ Good - Simulates real user interaction
const button = screen.getByRole('button', { name: 'Submit' });
await userEvent.click(button);

// ❌ Bad - Direct event firing (less realistic)
fireEvent.click(button);
```

**4. Use data-testid for Hard-to-Select Elements**
```tsx
// Component
<div data-testid="status-message">{status}</div>

// Test
expect(screen.getByTestId('status-message')).toHaveTextContent('Success');
```

---

## Testing Checklist

### Before Committing Code

- [ ] **Backend: Run `dotnet test`** - All tests pass
- [ ] **Frontend: Run `npm test`** - All tests pass
- [ ] **No console errors** - Check browser console
- [ ] **Manual smoke test** - App actually works

### When Adding New Features

- [ ] **Write tests FIRST** (TDD approach)
- [ ] **Test happy path** - Feature works as expected
- [ ] **Test error paths** - Handles failures gracefully
- [ ] **Test edge cases** - Empty data, null values, etc.
- [ ] **Update test documentation** - Add examples here if needed

### For Critical Code (P0 Priority)

- [ ] **Context providers** - Test loading, saving, error handling
- [ ] **IPC communication** - Test message routing and responses
- [ ] **Data persistence** - Test save/load/delete operations
- [ ] **Settings management** - Test all CRUD operations
- [ ] **Migration logic** - Test analysis and migration process

---

## Common Testing Patterns

### Pattern 1: Testing Context Providers

```typescript
// Create a test wrapper component
const TestWrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  return (
    <ThemeProvider>
      <AnnotationProvider>
        {children}
      </AnnotationProvider>
    </ThemeProvider>
  );
};

// Use wrapper in tests
render(<MyComponent />, { wrapper: TestWrapper });
```

### Pattern 2: Testing Custom Hooks

```typescript
import { renderHook, act } from '@testing-library/react';

test('useCustomHook should work', () => {
  const { result } = renderHook(() => useCustomHook());

  act(() => {
    result.current.updateValue('new value');
  });

  expect(result.current.value).toBe('new value');
});
```

### Pattern 3: Testing Services with Retry Logic

```typescript
test('service should retry on failure', async () => {
  let callCount = 0;
  mockBackend.mockImplementation(() => {
    callCount++;
    if (callCount < 3) throw new Error('Temporary failure');
    return Promise.resolve({ success: true });
  });

  const result = await service.getData();

  expect(callCount).toBe(3);
  expect(result.success).toBe(true);
});
```

### Pattern 4: Testing File System Operations (Backend)

```csharp
[Fact]
public async Task SaveFile_ShouldCreateFile()
{
    // Arrange
    var filename = "test";
    var content = "{\"key\":\"value\"}";

    // Act
    await _service.SaveSettingsFileAsync(filename, content);

    // Assert
    var filePath = Path.Combine(_testDataPath, "settings", $"{filename}.json");
    File.Exists(filePath).Should().BeTrue();

    var savedContent = await File.ReadAllTextAsync(filePath);
    savedContent.Should().Be(content);
}
```

---

## When to Write Tests

### ✅ ALWAYS Write Tests For:

1. **New Services** - Any new backend service or frontend service
2. **New Context Providers** - Any new React context
3. **Critical Bug Fixes** - Write test that reproduces bug, then fix
4. **Data Persistence** - Anything that saves/loads data
5. **IPC Communication** - Message handlers and routing

### ⚠️ CONSIDER Writing Tests For:

1. **Complex UI Components** - Forms, wizards, multi-step processes
2. **Utility Functions** - Formatters, validators, parsers
3. **State Management** - Complex useState/useReducer logic

### ❌ SKIP Tests For:

1. **Simple Presentational Components** - Pure UI with no logic
2. **Third-party Library Wrappers** - Trust the library's tests
3. **One-off Scripts** - Build scripts, migration scripts

---

## Example: Testing the Settings System

Here's a complete example for the recently added settings file service:

### Backend Test (SettingsFileServiceTests.cs)

```csharp
// See template above - create comprehensive tests for:
// - SaveSettingsFileAsync
// - GetSettingsFileAsync
// - DeleteSettingsFileAsync
// - SettingsFileExistsAsync
// - ListSettingsFilesAsync
// - Path traversal protection
// - Invalid JSON handling
// - Concurrent access
```

### Frontend Test (settingsFileService.test.ts)

```typescript
import { settingsFileService } from '../settingsFileService';
import { photinoService } from '../../../../shared/services/photinoService';

jest.mock('../../../../shared/services/photinoService');

describe('settingsFileService', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('getSettingsFile', () => {
    it('should parse and return JSON data', async () => {
      (photinoService.sendMessage as jest.Mock).mockResolvedValue({
        success: true,
        content: '{"theme":"dark"}'
      });

      const result = await settingsFileService.getSettingsFile('myconfig');

      expect(result).toEqual({ theme: 'dark' });
      expect(photinoService.sendMessage).toHaveBeenCalledWith(
        'SETTINGS',
        'GET_FILE',
        { filename: 'myconfig' }
      );
    });

    it('should return null if file does not exist', async () => {
      (photinoService.sendMessage as jest.Mock).mockResolvedValue({
        success: false
      });

      const result = await settingsFileService.getSettingsFile('nonexistent');

      expect(result).toBeNull();
    });

    it('should handle errors gracefully', async () => {
      (photinoService.sendMessage as jest.Mock).mockRejectedValue(
        new Error('Network error')
      );

      const result = await settingsFileService.getSettingsFile('myconfig');

      expect(result).toBeNull();
    });
  });

  describe('saveSettingsFile', () => {
    it('should serialize and save JSON data', async () => {
      (photinoService.sendMessage as jest.Mock).mockResolvedValue({
        success: true
      });

      const result = await settingsFileService.saveSettingsFile('myconfig', {
        theme: 'dark'
      });

      expect(result).toBe(true);
      expect(photinoService.sendMessage).toHaveBeenCalledWith(
        'SETTINGS',
        'SAVE_FILE',
        {
          filename: 'myconfig',
          content: '{\n  "theme": "dark"\n}'
        }
      );
    });
  });
});
```

---

## Summary

**Remember:**
1. **Test critical paths FIRST** - Settings, IPC, contexts
2. **Write tests BEFORE fixing bugs** - Reproduce, then fix
3. **Run tests BEFORE committing** - `dotnet test && npm test`
4. **Mock external dependencies** - Don't rely on real backend in frontend tests
5. **Use proper async patterns** - await/async, waitFor()

**If you're not sure if something needs a test, ASK THE USER!**

---

*Last updated: 2026-02-18*
