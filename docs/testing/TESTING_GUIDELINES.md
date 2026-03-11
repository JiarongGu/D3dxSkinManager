# Testing Guidelines

**Last Updated:** 2026-02-27

## Test Suite Status

**Total Tests**: 127 tests
**Pass Rate**: 100%
**Execution Time**: ~350ms
**External Dependencies**: None

### Test Breakdown
- **Pure Business Logic**: 61 tests (EventBus, ProfileEventBus, PathHelper, CategoryService)
- **SQLite Repositories**: 37 tests (Category, Mod, Tag - using in-memory SQLite)
- **In-Memory Repositories**: 33 tests (TaskChain, TaskInfo - using Dictionary)

All tests follow the principles outlined in this guide: no file system access, no WinForms dependencies, and no external resources.

---

## Unit Testing Rules

### ✅ DO: Test Pure Business Logic

Unit tests should focus on **pure business logic** that can be tested in isolation:

```csharp
✅ GOOD - Pure business logic
public class ModFacade
{
    private readonly IModRepository _repository;  // Interface - mockable
    private readonly IProfileEventBus _eventBus;  // Interface - mockable

    public async Task<Mod> GetModAsync(string id)
    {
        var mod = await _repository.GetByIdAsync(id);
        if (mod == null) throw new NotFoundException();
        return mod;
    }
}
```

**Unit tests can mock interfaces:**
- ✅ `IModRepository`
- ✅ `IProfileEventBus`
- ✅ `ILogHelper`
- ✅ All domain services with interfaces

### ❌ DON'T: Test Components with External Dependencies

**NEVER unit test these directly - DELETE the test files instead:**
- ❌ `WebView2` controls and WinForms components
- ❌ `Form` / `Control` classes
- ❌ Any `System.Windows.Forms.*` types
- ❌ UI event handlers
- ❌ File system operations (File, Directory, Stream)
- ❌ Network operations
- ❌ Any code that requires external resources

**EXCEPTION: Repository/Database Tests**
- ✅ SQLite repository tests CAN be tested using **in-memory databases**
- ✅ In-memory SQLite doesn't touch the file system and is fast
- ✅ See "Testing Repositories with In-Memory SQLite" section below

```csharp
❌ BAD - Testing WinForms directly
public class EventBusIpcBridgeTests
{
    // This will fail - can't mock WebView2!
    private readonly Mock<WebView2> _mockWebView;

    [Fact]
    public void Test_Something()
    {
        // WebView2 requires WinForms initialization
        // Tests will hang or crash
    }
}
```

## Making WinForms Code Testable

### Pattern: Interface + Implementation

To test code that uses WinForms components, **extract an interface**:

#### Before (Not Testable)
```csharp
public class IpcCommunicationHandler
{
    private readonly WebView2 _webView;  // ❌ Concrete WinForms type

    public IpcCommunicationHandler(WebView2 webView)
    {
        _webView = webView;
    }

    public void SendNotification(string module, string type, object? payload)
    {
        _webView.CoreWebView2.PostWebMessageAsJson(...);
    }
}
```

#### After (Testable)
```csharp
// 1. Create interface
public interface IIpcCommunicationHandler
{
    void SendNotification(string module, string type, object? payload);
    event EventHandler<IpcMessageReceivedEventArgs>? MessageReceived;
}

// 2. Implementation stays the same
public class IpcCommunicationHandler : IIpcCommunicationHandler
{
    private readonly WebView2 _webView;  // Still uses WebView2 internally

    public IpcCommunicationHandler(WebView2 webView)
    {
        _webView = webView;
    }

    public void SendNotification(string module, string type, object? payload)
    {
        _webView.CoreWebView2.PostWebMessageAsJson(...);
    }
}

// 3. Consumers depend on interface
public class EventBusIpcBridge
{
    private readonly IIpcCommunicationHandler _ipcHandler;  // ✅ Interface

    public EventBusIpcBridge(IEventBus eventBus, IIpcCommunicationHandler ipcHandler)
    {
        _ipcHandler = ipcHandler;
    }
}
```

#### Now Testable
```csharp
✅ GOOD - Can mock the interface
public class EventBusIpcBridgeTests
{
    [Fact]
    public void Test_EventForwarding()
    {
        // Arrange
        var mockIpc = new Mock<IIpcCommunicationHandler>();
        var bridge = new EventBusIpcBridge(mockEventBus.Object, mockIpc.Object);

        // Act
        await bridge.ForwardEventAsync(eventMessage);

        // Assert
        mockIpc.Verify(x => x.SendNotification("MOD", "LOADED", payload), Times.Once);
    }
}
```

## Test Organization

### Folder Structure

Mirror the production code structure:

```
D3dxSkinManager/
  ├── Modules/
  │   ├── Core/
  │   │   └── Event/
  │   │       └── EventBus.cs
  │   └── Mod/
  │       └── Services/
  │           └── ModFacade.cs

D3dxSkinManager.Tests/
  ├── Modules/
  │   ├── Core/
  │   │   └── Event/
  │   │       └── EventBusTests.cs
  │   └── Mod/
  │       └── Services/
  │           └── ModFacadeTests.cs
```

### Test Naming Convention

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange

    // Act

    // Assert
}
```

**Examples:**
- `GetModAsync_WhenModExists_ShouldReturnMod()`
- `EmitAsync_WithProfileId_ShouldFilterByProfile()`
- `RegisterHandler_WithInvalidModule_ShouldThrowArgumentException()`

## What to Do with Tests That Have External Dependencies

**DO NOT skip or keep these tests** - **DELETE them entirely:**

1. **WinForms/WebView2 Tests** - Delete the entire test file
   - Example: `EventBusIpcBridgeTests.cs` was deleted
   - These tests will never run reliably in unit test environment

2. **File System Tests** - Delete the entire test file
   - Example: `GlobalSettingServiceTests.cs`, `SettingFileServiceTests.cs`, `ImageServiceTests.cs`, `FileServiceTests.cs` were deleted
   - File operations should be tested manually or in integration tests

3. **Database/Network Tests** - Delete the entire test file
   - These require external resources and don't belong in unit tests

**Philosophy:** Unit tests should only test pure business logic with mockable dependencies. If you can't mock it easily, don't test it in unit tests. Keeping untestable tests just creates maintenance burden and CI/CD issues.

## Testing Tools

### Required NuGet Packages

```xml
<PackageReference Include="xUnit" Version="..." />
<PackageReference Include="FluentAssertions" Version="..." />
<PackageReference Include="Moq" Version="..." />
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="..." />
```

### Assertion Style

**Use FluentAssertions** for readable assertions:

```csharp
✅ GOOD - Fluent and readable
result.Should().NotBeNull();
result.Theme.Should().Be("dark");
result.Mods.Should().HaveCount(3);
result.Mods.Should().Contain(m => m.Sha == "abc123");

❌ BAD - Old xUnit style
Assert.NotNull(result);
Assert.Equal("dark", result.Theme);
Assert.Equal(3, result.Mods.Count);
```

## Testing Repositories with In-Memory SQLite

For testing SQLite repositories, use **shared in-memory databases** instead of file-based databases:

```csharp
✅ GOOD - In-memory SQLite with shared cache
public class CategoryRepositoryTests
{
    private readonly CategoryRepository _repository;
    private readonly Mock<IProfilePathService> _mockProfilePathService;

    public CategoryRepositoryTests()
    {
        // Use shared in-memory SQLite database - no file system access!
        // Using URI filename with cache=shared allows multiple connections to share the same in-memory database
        // This is required because CategoryRepository opens/closes connections for each operation
        var dbName = $"testdb_{Guid.NewGuid():N}";
        _mockProfilePathService = new Mock<IProfilePathService>();

        // CategoryRepository will create connection string as:
        // Data Source=file:testdb_xxx?mode=memory&cache=shared
        _mockProfilePathService
            .Setup(p => p.ProfileDatabasePath)
            .Returns($"file:{dbName}?mode=memory&cache=shared");

        _repository = new CategoryRepository(_mockProfilePathService.Object);
    }

    [Fact]
    public async Task InsertAsync_WithValidCategory_ShouldInsert()
    {
        // Arrange
        var category = new CategoryInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Category",
            Priority = 100
        };

        // Act
        var result = await _repository.InsertAsync(category);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(category.Id);
    }
}
```

**Why this works:**
- `mode=memory` creates an in-memory database (no file system)
- `cache=shared` allows multiple connections to share the same database
- Each test class gets a unique database name (GUID-based)
- Fast, isolated, and tests real SQL queries

**When to use:**
- ✅ Testing repository CRUD operations
- ✅ Testing SQL queries and joins
- ✅ Testing database schema/migrations

**When NOT to use:**
- ❌ File-based services (GlobalSettingService, SettingFileService, ImageService)
- ❌ Services that directly use File.* or Directory.* operations
- ❌ Use full mocking or delete the tests instead

## Common Testing Patterns

### Pattern 1: Testing Async/Await

```csharp
[Fact]
public async Task EmitAsync_ShouldInvokeHandler()
{
    // Arrange
    var invoked = false;
    eventBus.RegisterHandler("MOD", "LOADED", async (msg) => {
        invoked = true;
        await Task.CompletedTask;
    });

    // Act
    await eventBus.EmitAsync("MOD", "LOADED");

    // Assert
    invoked.Should().BeTrue();
}
```

### Pattern 2: Testing Events

```csharp
[Fact]
public async Task Service_WhenEventOccurs_ShouldRaiseEvent()
{
    // Arrange
    EventMessage? capturedEvent = null;
    eventBus.RegisterHandler("MOD", "LOADED", async (msg) => {
        capturedEvent = msg;
        await Task.CompletedTask;
    });

    // Act
    await service.LoadModAsync("abc123");

    // Assert
    capturedEvent.Should().NotBeNull();
    capturedEvent!.Module.Should().Be("MOD");
    capturedEvent.Type.Should().Be("LOADED");
}
```

## Summary Checklist

Before writing a test, ask:

- [ ] Is this testing pure business logic?
- [ ] Can I mock all dependencies?
- [ ] Does this require external dependencies?
  - WinForms/WebView2 → **DELETE the test file**
  - File System (File.*, Directory.*) → **DELETE the test file**
  - Database → Use **in-memory SQLite** if it's a repository test
  - Network → **DELETE the test file**
- [ ] Are all assertions using FluentAssertions?
- [ ] Does the test follow AAA (Arrange-Act-Assert)?
- [ ] Is the test name descriptive?

## Need Help?

- **WinForms testing issues**: Extract interfaces (see examples above)
- **Async/await problems**: Use `async Task` return type
- **Mock setup**: Check [Moq documentation](https://github.com/moq/moq4)
- **FluentAssertions**: Check [documentation](https://fluentassertions.com/)
