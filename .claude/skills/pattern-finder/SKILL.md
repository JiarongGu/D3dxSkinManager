---
name: pattern-finder
description: Use FIRST before implementing any new feature, service, or component. Searches codebase for existing similar code patterns to reuse and follow.
disable-model-invocation: false
---

# Pattern Finder (Code Discovery)

Find existing implementations of patterns in the codebase to use as reference examples.

**Purpose**: Replace manual grep/search with intelligent pattern discovery that finds the best reference examples for your task.

## Arguments

**Format**: `/pattern-finder <PatternType> <Module?>`

**Example**:
```
/pattern-finder service Mod
/pattern-finder event-handler
/pattern-finder batch-operation
```

**Parameters**:
- `PatternType` - Type of pattern to find (see Pattern Types below)
- `Module` - Optional: Limit search to specific module (Mod, Profile, Workflow, etc.)

## Supported Pattern Types

### Backend Patterns
- `service` - Find service implementations (Layer 1, 2, 3)
- `repository` - Find repository implementations
- `facade` - Find facade implementations
- `event-handler` - Find event consolidation handlers
- `file-watcher` - Find FileSystemWatcher implementations
- `cache-service` - Find IMemoryCache usage patterns
- `batch-operation` - Find batch operations (SQL + facade + frontend)

### Frontend Patterns
- `component` - Find React components
- `ipc-service` - Find frontend IPC service implementations
- `context` - Find React context providers
- `hook` - Find custom React hooks
- `ag-grid` - Find AG Grid usage examples

### Cross-Cutting Patterns
- `error-handling` - Find OperationException usage
- `i18n` - Find translation patterns
- `events` - Find event emission/subscription
- `testing` - Find test patterns (unit, integration)
- `migration` - Find database migration examples

## What This Skill Does

1. **Searches codebase** for existing implementations
2. **Ranks by quality** (best examples first)
3. **Shows code snippets** from top examples
4. **Provides file paths** for deep dive
5. **Highlights key patterns** used

## Output Format

```markdown
## Pattern: {PatternType} {Module?}

### Found {N} Implementations

### 🌟 Best Examples (Highest Quality)

**1. ModLifecycleService** ⭐⭐⭐⭐⭐
- **File**: `Modules/Mod/Services/ModLifecycleService.cs`
- **Why**: Complete example with events, error handling, DI, Layer 2 service
- **Key Features**:
  - ✅ Event emission after operations
  - ✅ Business logic (category conflict resolution)
  - ✅ Service coordination (cacheService + archiveService)
  - ✅ Proper error handling with OperationException
- **Pattern Used**: Layer 2 Service (Business Logic + Events)
- **Code Snippet**:
  ```csharp
  public async Task<ModLoadResult> LoadAsync(string id) {
      var mod = await _repository.GetByIdAsync(id);
      await HandleCategoryConflicts(mod);  // Business logic
      var success = await _cacheService.EnableCacheAsync(id);

      if (success) {
          await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new { Id = id });
      }
      return new ModLoadResult { Success = success };
  }
  ```

**2. ModQueryService** ⭐⭐⭐⭐
- **File**: `Modules/Mod/Services/ModQueryService.cs`
- **Why**: Good example of caching + FileSystemWatcher
- **Key Features**:
  - ✅ IMemoryCache with profile-specific keys
  - ✅ FileSystemWatcher for cache invalidation
  - ✅ GetOrCreateAsync pattern with Task.Yield()
- **Pattern Used**: Layer 1 Service (Pure Operations) + Caching
- **Code Snippet**:
  ```csharp
  public async Task<List<ModInfo>> GetActiveModsAsync() {
      return await _cache.GetOrCreateAsync(_cacheKey, async entry => {
          await Task.Yield();  // Prevent UI blocking
          return await ScanCacheDirectory();
      }) ?? new List<ModInfo>();
  }
  ```

### 📋 Other Examples

3. **TextureValidationService** - `Modules/Mod/Services/TextureValidationService.cs`
   - Validation service pattern
   - Multiple validation rules

4. **ModArchiveService** - `Modules/Mod/Services/ModArchiveService.cs`
   - File operations with error handling
   - Progress reporting

### Pattern Summary

**Common Elements Across All Examples**:
- ✅ Interface + Implementation separation
- ✅ Constructor DI injection (IRepository, IEventBus, ILogHelper)
- ✅ Async/await throughout
- ✅ OperationException for errors
- ✅ Logging at appropriate levels

**Variations**:
- Layer 1 vs Layer 2 vs Layer 3 responsibilities
- With/without IMemoryCache
- With/without event emission
- With/without FileSystemWatcher

### Suggested Skills

For creating similar code:
- `/backend-service` - Generate service structure
- `/error-with-i18n` - Add error handling
- `/service-registration` - Register in DI

### Related Patterns

If implementing this pattern, you might also need:
- **Event Handler** - If service emits events
- **Facade** - For IPC integration
- **IPC Service** - Frontend integration
```

## Pattern Search Strategies

### Strategy 1: Find by File Pattern
```
Pattern: service
→ Search: Modules/*/Services/*.cs
→ Filter: Classes implementing I{Name}Service
→ Rank: By complexity and completeness
```

### Strategy 2: Find by Interface
```
Pattern: repository
→ Search: Classes implementing IRepository
→ Filter: In Modules/*/Repositories/
→ Rank: By usage count
```

### Strategy 3: Find by Feature
```
Pattern: batch-operation
→ Search: Methods with "Batch" in name
→ Filter: SQL IN clause patterns
→ Rank: By completeness (backend + frontend)
```

## Ranking Criteria

Examples are ranked (⭐⭐⭐⭐⭐ = best) based on:

**5 Stars** - Perfect Example:
- ✅ Complete implementation (no TODOs)
- ✅ All project patterns followed (DI, events, errors, logging)
- ✅ Well-documented with XML comments
- ✅ Used in production (not test code)
- ✅ Recently updated (follows current patterns)

**4 Stars** - Good Example:
- ✅ Complete implementation
- ✅ Most patterns followed
- ✅ Some documentation
- ⚠️ May be missing one best practice

**3 Stars** - Acceptable Example:
- ✅ Working implementation
- ⚠️ Missing some patterns (e.g., no events)
- ⚠️ Minimal documentation

**2 Stars** - Basic Example:
- ✅ Functional but minimal
- ⚠️ Missing multiple patterns
- ⚠️ May be outdated

**1 Star** - Avoid:
- ⚠️ Incomplete or deprecated
- ⚠️ Poor example to follow
- ⚠️ Listed only for completeness

## Common Pattern Types Detail

### Backend Service Pattern
**Searches for**:
- Classes in `Modules/*/Services/`
- Implementing `I{Name}Service` interface
- With constructor DI injection

**Best examples include**:
- IProfileEventBus injection
- Event emission after operations
- OperationException error handling
- Proper logging levels

### Event Handler Pattern
**Searches for**:
- Classes in `Modules/*/EventHandlers/`
- Implementing `I{Name}EventHandler`
- With event subscriptions in constructor

**Best examples include**:
- Multiple event subscriptions
- Consolidated event emission
- Proper cleanup/disposal

### IPC Service Pattern
**Searches for**:
- TypeScript classes extending `BaseModuleService`
- In `shared/services/ipc/*.ts`
- With sendMessage/sendArrayMessage calls

**Best examples include**:
- Type-safe generics
- JSDoc comments referencing backend
- Singleton export pattern

### Batch Operation Pattern
**Searches for**:
- Backend: Methods with parameterized SQL IN clause
- Facade: Methods with result aggregation loops
- Frontend: batchDelete/batchUpdate methods

**Best examples include**:
- Complete triple (backend SQL + facade + frontend)
- Proper transaction handling
- Result aggregation with partial failure handling

## Example Outputs

### Example 1: Finding Services
```
/pattern-finder service Mod
```

**Output**:
- Lists all Mod module services
- Ranks by quality (Layer 2 > Layer 1 > Layer 3)
- Shows code snippets from best examples
- Suggests related patterns

### Example 2: Finding Event Handlers
```
/pattern-finder event-handler
```

**Output**:
- Lists all event handler implementations
- Shows consolidation patterns
- Highlights event subscription patterns
- Shows proper cleanup examples

### Example 3: Finding React Components
```
/pattern-finder component
```

**Output**:
- Lists major React components
- Highlights hook usage patterns
- Shows BEM CSS examples
- Demonstrates error handling with handleError

## Integration with Other Skills

**Typical Workflow**:
```
1. /pattern-finder service Mod
   → Find existing service examples

2. /doc-loader "creating mod service" backend
   → Load pattern documentation

3. /backend-service MyService Mod IDep1,IDep2 MethodAsync
   → Generate new service using pattern

4. Manually review against reference examples
   → Ensure all patterns followed
```

## Output Customization

### Detailed Mode (Default)
Shows:
- Full file paths
- Code snippets (10-20 lines)
- Key features list
- Pattern classification

### Summary Mode
Shows:
- File paths only
- Brief description
- Star rating

### Code-Only Mode
Shows:
- Minimal text
- Maximum code snippets
- For copy-paste reference

## Smart Filtering

**By Module**:
```
/pattern-finder service Mod      # Only Mod module
/pattern-finder service Profile  # Only Profile module
```

**By Layer** (services only):
```
/pattern-finder service --layer1  # Pure operations only
/pattern-finder service --layer2  # Business logic
```

**By Feature**:
```
/pattern-finder service --caching    # Services using IMemoryCache
/pattern-finder service --events     # Services emitting events
/pattern-finder service --file-ops   # Services with file operations
```

## Important Rules

- ✅ Always show file paths for reference
- ✅ Include code snippets from best examples
- ✅ Rank examples by quality (best first)
- ✅ Highlight pattern variations
- ✅ Suggest related skills and patterns
- ✅ Filter out test code unless specifically requested
- ❌ Don't show deprecated or outdated examples
- ❌ Don't overwhelm with too many examples (top 5 max)
- ❌ Don't show incomplete implementations (unless noted)

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial pattern finder skill

**How to update this skill**:
1. Add new pattern types as they emerge in codebase
2. Update ranking criteria based on current best practices
3. Add new filtering options as needed
4. Refine search strategies for better matches
5. Update reference to skills as new skills are added
6. Adjust star ratings as patterns evolve (old 5-star may become 4-star)

**When patterns change**:
- Re-rank existing examples based on new criteria
- Mark old patterns as "legacy" (lower star rating)
- Promote new pattern examples to top of list
- Update "Pattern Summary" section with new requirements
