# Advanced Patterns

**Version:** 1.0
**Last Updated:** 2026-04-11

This document contains advanced patterns that are **too complex or context-specific to automate with skills**. These require understanding, judgment, and adaptation to specific use cases.

**Note**: For code generation patterns (services, components, IPC, etc.), use the **Skills System** instead:
- See [.claude/skills/README.md](../../.claude/skills/README.md) for available skills
- See [CODE_GENERATION.md](../CODE_GENERATION.md) for how to use them

---

## 📚 Table of Contents

- [IMemoryCache Strategy](#imemorycache-strategy)
- [Database Migrations](#database-migrations)
- [Performance Optimization](#performance-optimization)
- [Advanced Event Patterns](#advanced-event-patterns)

---

## IMemoryCache Strategy

### When to Use Caching

**✅ USE caching when**:
- Operation is expensive (file system scans, complex queries)
- Data changes infrequently but is accessed frequently
- You can detect when data becomes stale (events, file watcher)

**❌ DON'T cache when**:
- Data changes constantly
- Cache invalidation is complex/unreliable
- Memory footprint would be too large
- Operation is already fast (< 10ms)

### Profile-Specific Cache Keys

**CRITICAL**: `IMemoryCache` is a **singleton** shared across all profiles. You MUST use profile-specific keys!

```csharp
public class ModQueryService
{
    private readonly IMemoryCache _cache;
    private readonly string _cacheKey;

    public ModQueryService(
        IMemoryCache cache,
        IProfileContext profileContext)
    {
        _cache = cache;

        // ✅ CORRECT - Profile-specific key
        _cacheKey = $"ActiveMods_{profileContext.ProfileId}";

        // ❌ WRONG - Shared across profiles (data leakage!)
        // _cacheKey = "ActiveMods";
    }
}
```

**Why**: Without profile-specific keys, Profile A might see Profile B's data!

### Task.Yield() Pattern

**CRITICAL**: Long-running cache factories block the IPC thread, preventing UI updates.

```csharp
public async Task<List<ModInfo>> GetActiveModsAsync()
{
    return await _cache.GetOrCreateAsync(_cacheKey, async entry =>
    {
        // ✅ CRITICAL - Yield immediately to prevent UI blocking
        await Task.Yield();

        // Now run expensive operation asynchronously
        return await ScanCacheDirectory();  // 500ms+
    }) ?? new List<ModInfo>();
}
```

**What Task.Yield() does**:
- Forces continuation to run asynchronously
- Allows UI thread to process pending state updates
- Prevents loading spinners from appearing "stuck"

**Where to use it**:
- Cache factories (`GetOrCreateAsync`)
- Migration operations
- Workflow execution steps
- Category tree building
- Any operation > 100ms called from IPC

### Event-Driven Cache Invalidation

**Best Practice**: Invalidate cache automatically when data changes.

```csharp
public ModQueryService(
    IMemoryCache cache,
    IProfileContext profileContext,
    IProfileEventBus eventBus)
{
    _cache = cache;
    _cacheKey = $"ActiveMods_{profileContext.ProfileId}";

    // Subscribe to events that invalidate cache
    eventBus.Subscribe(ModuleNames.MOD, ModEvents.CACHE_CHANGED, _ =>
    {
        _cache.Remove(_cacheKey);  // Invalidate on change
        return Task.CompletedTask;
    });

    // Can subscribe to multiple events
    eventBus.Subscribe(ModuleNames.MOD, ModEvents.DELETED, _ =>
    {
        _cache.Remove(_cacheKey);
        return Task.CompletedTask;
    });
}
```

**Use FileSystemWatcher** for cache directory monitoring:
```csharp
// ModCacheWatcher monitors cache/Mods directory
// Emits CACHE_CHANGED event on file/folder changes
// ModQueryService subscribes to CACHE_CHANGED → invalidates cache
```

See: `/file-watcher` skill for generating watcher services.

### Cache Expiration (Use Sparingly)

**Prefer**: Event-driven invalidation
**Fallback**: Time-based expiration (when events unreliable)

```csharp
await _cache.GetOrCreateAsync(_cacheKey, async entry =>
{
    // Set absolute expiration (cache cleared after this time)
    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

    // Or sliding expiration (reset on access)
    entry.SlidingExpiration = TimeSpan.FromMinutes(2);

    await Task.Yield();
    return await LoadData();
});
```

**Guidelines**:
- Event-driven invalidation > Time-based expiration
- Use absolute expiration for truly time-sensitive data
- Use sliding expiration for "keep warm" scenarios

---

## Database Migrations

### Philosophy

**Database-First Approach**:
1. Define entity model changes
2. Generate migration
3. Run migration
4. Verify with tests

**Not Automated**: Migrations require understanding schema changes and data transformations.

### Migration Workflow

```bash
# 1. Modify entity (e.g., add property to ModEntity)
# Add: public string? NewField { get; set; }

# 2. Generate migration
dotnet ef migrations add AddNewFieldToMods -p D3dxSkinManager.csproj

# 3. Review generated migration file
# Check: Modules/Fluent/Migrations/Migration_YYYYMMDDHHMMSS_AddNewFieldToMods.cs

# 4. Run migration
dotnet ef database update -p D3dxSkinManager.csproj

# 5. Verify with test
# Create integration test extending InMemoryDatabaseTestBase
```

### Migration Best Practices

**1. Nullable vs Required Fields**:
```csharp
// Migration file
.WithColumn("Author").AsText().Nullable()      // Can be null
.WithColumn("Name").AsText().NotNullable()     // Cannot be null

// Entity must match
public string Name { get; set; } = string.Empty;  // Required (not nullable)
public string? Author { get; set; }                // Nullable
```

**2. Default Values for New Columns**:
```csharp
// Add column with default value for existing rows
.WithColumn("IsActive")
    .AsBoolean()
    .NotNullable()
    .WithDefaultValue(true);  // Existing rows get true
```

**3. Data Transformation Migrations**:
```csharp
// Complex migration with data transformation
public override void Up()
{
    // 1. Add new column
    Alter.Table("Mods")
        .AddColumn("NewColumn").AsString().Nullable();

    // 2. Transform existing data
    Execute.Sql(@"
        UPDATE Mods
        SET NewColumn = OldColumn + '_suffix'
        WHERE OldColumn IS NOT NULL
    ");

    // 3. Make column required after populating
    Alter.Column("NewColumn").OnTable("Mods")
        .AsString().NotNullable();
}
```

### Testing Migrations

**Use InMemoryDatabaseTestBase**:
```csharp
public class ModRepositoryIntegrationTests : InMemoryDatabaseTestBase
{
    [Fact]
    public async Task NewField_ShouldPersist()
    {
        // Arrange
        var repository = new ModRepository(Connection, Logger);
        var mod = new ModEntity { Name = "Test", NewField = "Value" };

        // Act
        await repository.AddAsync(mod);
        var retrieved = await repository.GetByNameAsync("Test");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.NewField.Should().Be("Value");
    }
}
```

**InMemoryDatabaseTestBase**:
- Runs real FluentMigrator migrations on in-memory SQLite
- Tests actual database schema
- Verifies nullable/required matches entity model

---

## Performance Optimization

### React.memo vs useMemo

**React.memo** - Prevents component re-render:
```typescript
// ✅ Use when component is expensive to render
export const ExpensiveComponent = React.memo(({ data }: Props) => {
  // Component only re-renders if `data` prop changes
  return <ComplexVisualization data={data} />;
});

// ❌ Don't use for simple components
export const SimpleDiv = React.memo(({ text }: Props) => {
  return <div>{text}</div>;  // Too simple to benefit
});
```

**useMemo** - Memoizes expensive calculations:
```typescript
const ExpensiveComponent = ({ items }: Props) => {
  // ✅ Memoize expensive calculation
  const sortedItems = useMemo(() => {
    return items.sort((a, b) => complexCompare(a, b));  // Expensive
  }, [items]);

  // ❌ Don't memoize cheap calculations
  const count = useMemo(() => items.length, [items]);  // Too cheap
};
```

**Guidelines**:
- `React.memo`: Expensive components (> 16ms render time)
- `useMemo`: Expensive calculations (> 10ms)
- Profile first, optimize second (don't guess)

### Virtual Scrolling

**When to use**: Lists with > 100 items

```typescript
import { FixedSizeList } from 'react-window';

const VirtualizedModList = ({ mods }: Props) => {
  return (
    <FixedSizeList
      height={600}
      itemCount={mods.length}
      itemSize={50}
      width="100%"
    >
      {({ index, style }) => (
        <div style={style}>
          <ModCard mod={mods[index]} />
        </div>
      )}
    </FixedSizeList>
  );
};
```

**Benefits**:
- Only renders visible items
- 1000s of items with smooth scrolling
- Memory usage stays constant

### Debouncing vs Throttling

**Debounce** - Wait for pause in events:
```typescript
// ✅ Use for search input (wait until user stops typing)
const debouncedSearch = useCallback(
  debounce((query: string) => {
    performSearch(query);
  }, 300),  // Wait 300ms after last keystroke
  []
);
```

**Throttle** - Limit event rate:
```typescript
// ✅ Use for scroll events (limit update rate)
const throttledScroll = useCallback(
  throttle(() => {
    updateScrollPosition();
  }, 100),  // Max once per 100ms
  []
);
```

**Guidelines**:
- **Debounce**: Search, input validation, resize
- **Throttle**: Scroll, mouse move, resize (when need continuous feedback)

**See also**: [memoizeDebounce pattern](../ai-assistant/WORKFLOWS.md#debouncing-with-parameters) for per-parameter debouncing.

---

## Advanced Event Patterns

### Event Consolidation with Debouncing

**Problem**: Multiple related events fire rapidly (event storm).

**Solution**: Consolidate events + debounce frontend handler.

```typescript
// Backend: Event handler consolidates 8 events → 1
public class ModListEventHandler
{
    public ModListEventHandler(IProfileEventBus eventBus)
    {
        // 8 different events all emit MOD_LIST_UPDATED
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.LOADED,
            async _ => await EmitConsolidated());
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.UNLOADED,
            async _ => await EmitConsolidated());
        // ... 6 more subscriptions
    }

    private async Task EmitConsolidated()
    {
        await _eventBus.EmitAsync(
            ModuleNames.MOD,
            ModEvents.MOD_LIST_UPDATED,  // Single event
            new { }
        );
    }
}
```

```typescript
// Frontend: Subscribe to consolidated event with debouncing
const handleModListUpdate = useCallback(
  debounce(() => {
    if (!selectedProfileId) return;
    void modOps.refreshMods(selectedProfileId);  // Refresh once
  }, 20),  // 20ms debounce (rapid-fire events handled once)
  [selectedProfileId]
);

useEffect(() => {
  const unsubscribe = eventBus.subscribe(
    Module.MOD,
    ModEventType.MOD_LIST_UPDATED,  // Single subscription
    handleModListUpdate
  );

  return () => {
    handleModListUpdate.cancel();  // Cancel pending debounce
    unsubscribe();
  };
}, [selectedProfileId, handleModListUpdate]);
```

**Benefits**:
- 8+ event subscriptions → 1 subscription
- Rapid-fire events (e.g., load 5 mods) → 1 refresh
- Backend consolidates → Frontend debounces → Optimal performance

See: `/event-handler` skill for generating consolidation handlers.

### Per-Parameter Debouncing

**Problem**: Standard debounce only keeps last call's parameters.

**Solution**: Use `memoizeDebounce` for independent timers per parameter.

```typescript
import { memoizeDebounce } from '@/shared/utils/memoizeDebounce';

// ❌ WRONG - Regular debounce loses parameters
const handleEvent = useCallback(
  debounce(async (id: string) => {
    await refreshMod(id);  // Only LAST id processed!
  }, 20),
  []
);
// Problem: LOADED(mod1), UNLOADED(mod2), LOADED(mod3)
// → Only mod3 refreshed (mod1, mod2 lost)

// ✅ CORRECT - memoizeDebounce creates separate timer per ID
const handleEvent = useCallback(
  memoizeDebounce(
    async (id: string) => {
      await refreshMod(id);
    },
    (id) => id,  // Cache key resolver (each ID independent)
    20
  ),
  []
);
// Result: mod1, mod2, mod3 all refresh (independent timers)
```

**When to use**:
- Different entities (different mod IDs): `memoizeDebounce`
- Same operation batching (save settings): Regular `debounce`

---

## 📖 Related Documentation

- **Code Generation**: [.claude/skills/README.md](../../.claude/skills/README.md) - Skills for automatable patterns
- **Architecture**: [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md) - Architecture constraints
- **Testing**: [../ai-assistant/TESTING_GUIDE.md](../ai-assistant/TESTING_GUIDE.md) - Testing infrastructure
- **Troubleshooting**: [../ai-assistant/TROUBLESHOOTING.md](../ai-assistant/TROUBLESHOOTING.md) - Common issues

---

**Remember**: This doc is for patterns that **require judgment**. For repetitive code generation, use **skills** instead!
