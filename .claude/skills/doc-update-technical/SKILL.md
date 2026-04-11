---
name: doc-update-technical
description: Update technical documentation (ADVANCED_PATTERNS.md, DESIGN_DECISIONS.md) with new patterns, architectural decisions, or performance optimizations
---

# Technical Documentation Updater

Update technical documentation that explains non-automatable patterns, architectural decisions, and deep technical concepts.

**Purpose**: Technical documents capture the "WHY" behind architecture decisions and document complex patterns that can't be automated into skills. These documents evolve as new patterns emerge and architecture decisions are made.

## Arguments

**Format**: `/doc-update-technical <DocumentName> <UpdateType> <Details>`

**Example**:
```
/doc-update-technical ADVANCED_PATTERNS new-pattern "Async validation with FluentValidation - prevents UI blocking"
/doc-update-technical DESIGN_DECISIONS new-decision "Switched to FluentValidation for consistent validation across modules"
/doc-update-technical ADVANCED_PATTERNS update-pattern "Updated IMemoryCache pattern to include distributed caching"
```

**Parameters**:
- `DocumentName` - Technical document to update (ADVANCED_PATTERNS, DESIGN_DECISIONS)
- `UpdateType` - Type of update (new-pattern, new-decision, update-pattern, deprecate-pattern)
- `Details` - Technical description of pattern/decision

## What This Skill Does

1. **Identifies target document**:
   - ADVANCED_PATTERNS.md - Non-automatable technical patterns
   - DESIGN_DECISIONS.md - Architecture decisions and rationale

2. **Adds new technical content**:
   - New patterns with code examples
   - Architecture decisions with trade-offs
   - Performance optimizations with benchmarks
   - Migration notes for pattern changes

3. **Updates existing content**:
   - Evolves patterns as better approaches emerge
   - Adds context to decisions
   - Marks deprecated patterns
   - Adds version history

4. **Maintains technical accuracy**:
   - Includes code examples
   - Documents WHY, not just WHAT
   - Explains trade-offs
   - References related patterns

## Target Documents

### ADVANCED_PATTERNS.md

**Purpose**: Document complex patterns that can't be automated into skills.

**Structure**:
```markdown
# Advanced Patterns

**Last Updated:** 2026-04-11

Non-automatable patterns requiring understanding and judgment.

## Pattern Categories

1. **Caching Strategy** - IMemoryCache patterns
2. **Database Patterns** - Migrations, transactions
3. **Performance Optimization** - React optimization, async patterns
4. **Event Patterns** - Advanced event handling
5. **Validation Patterns** - NEW: Complex validation scenarios

## 1. Caching Strategy

### Profile-Specific Cache Keys

**Pattern**:
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
        _cacheKey = $"ActiveMods_{profileContext.ProfileId}";  // Profile-specific
    }

    public async Task<List<ModInfo>> GetActiveModsAsync()
    {
        return await _cache.GetOrCreateAsync(_cacheKey, async entry =>
        {
            await Task.Yield();  // CRITICAL - Prevent UI blocking
            return await ScanCacheDirectory();
        }) ?? new List<ModInfo>();
    }
}
```

**Why**: Cache keys must be profile-specific to prevent data leakage between profiles.

**Trade-offs**:
- ✅ Prevents cross-profile data leakage
- ✅ Allows per-profile cache invalidation
- ❌ Increases memory usage (cache per profile)

## 5. Validation Patterns (NEW)

### Async Validation without UI Blocking

**Pattern**:
```csharp
public class ModValidator : AbstractValidator<ModCreateRequest>
{
    public ModValidator(IModRepository repository)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MustAsync(async (name, cancellation) =>
            {
                await Task.Yield();  // CRITICAL - Prevent UI blocking
                return await repository.IsNameUniqueAsync(name);
            })
            .WithMessage("Mod name must be unique");
    }
}
```

**Why**: FluentValidation async rules run synchronously by default, blocking UI. `Task.Yield()` ensures async execution.

**Trade-offs**:
- ✅ Prevents UI freezing during validation
- ✅ Allows database queries in validation
- ❌ Slightly slower validation (async overhead)

**Related Patterns**: See IMemoryCache pattern for similar Task.Yield usage.
```

**Update Patterns for ADVANCED_PATTERNS.md**:

1. **New Pattern**:
   - Add to pattern categories list
   - Create new section with heading
   - Include code example
   - Document WHY and trade-offs
   - Add related patterns references

2. **Update Pattern**:
   - Update code example
   - Add version note (v1.0 → v2.0)
   - Document migration if breaking change
   - Preserve old pattern as "Legacy" if needed

3. **Deprecate Pattern**:
   - Add "DEPRECATED" marker to heading
   - Explain why deprecated
   - Provide replacement pattern
   - Keep for reference (don't delete)

### DESIGN_DECISIONS.md

**Purpose**: Document architecture decisions and their rationale.

**Structure**:
```markdown
# Design Decisions

**Last Updated:** 2026-04-11

Architectural decisions and their rationale.

## Decision Log

### DD-001: Modular Architecture (2026-01-15)

**Decision**: Use module-based architecture with DI containers per module.

**Context**: Need to support plugins and extensions without tight coupling.

**Alternatives Considered**:
1. Monolithic architecture - Simpler but inflexible
2. Microservices - Too complex for desktop app

**Trade-offs**:
- ✅ Extensible via plugins
- ✅ Clear boundaries between features
- ❌ More complex DI setup

**Status**: Active

---

### DD-005: FluentValidation for Validation (2026-04-11) (NEW)

**Decision**: Use FluentValidation library for all entity validation.

**Context**: Need consistent validation across backend and consistent error messages.

**Alternatives Considered**:
1. Data annotations - Limited expressiveness
2. Manual validation - Inconsistent patterns

**Trade-offs**:
- ✅ Expressive validation rules
- ✅ Reusable validators
- ✅ Async validation support
- ❌ Additional dependency

**Impact**:
- New skill: `/backend-validator`
- Pattern: See ADVANCED_PATTERNS.md (Async Validation section)

**Status**: Active
```

**Update Patterns for DESIGN_DECISIONS.md**:

1. **New Decision**:
   - Add to decision log with DD-XXX number
   - Include date
   - Document context, alternatives, trade-offs
   - Mark as "Active"

2. **Update Decision**:
   - Add update note to existing decision
   - Document what changed and why
   - Update status if needed

3. **Deprecate Decision**:
   - Change status to "Deprecated"
   - Document replacement decision
   - Add migration timeline

## Update Patterns

### Pattern 1: New Technical Pattern

**Steps**:
1. Update "Last Updated" date
2. Add to pattern categories (if new category)
3. Create new section with heading
4. Include complete code example
5. Document WHY (rationale)
6. Document trade-offs (pros/cons)
7. Add related patterns cross-references
8. Mark as NEW for visibility

**Example**:
```markdown
## 5. Validation Patterns (NEW)

### Async Validation without UI Blocking

**Pattern**: [code example]

**Why**: [rationale]

**Trade-offs**:
- ✅ Benefit 1
- ✅ Benefit 2
- ❌ Cost 1

**Related Patterns**: See X, Y
```

### Pattern 2: Update Existing Pattern

**Steps**:
1. Update "Last Updated" date
2. Update code example
3. Add version note if significant change
4. Document what changed and why
5. Add migration notes if breaking
6. Preserve old pattern as "Legacy" if still used

**Example**:
```markdown
### IMemoryCache Strategy

**Pattern (v2.0 - Updated 2026-04-11)**:
```csharp
// NEW: Added distributed caching fallback
public async Task<T> GetOrCreateAsync<T>(...)
{
    // Try local cache first
    var result = await _memoryCache.GetOrCreateAsync(...);
    if (result != null) return result;

    // Fallback to distributed cache
    return await _distributedCache.GetOrCreateAsync(...);
}
```

**Migration from v1.0**:
- Add IDistributedCache injection
- Update cache retrieval to check both caches
```

### Pattern 3: New Architecture Decision

**Steps**:
1. Update "Last Updated" date
2. Assign DD-XXX number (next in sequence)
3. Add decision entry with date
4. Document context (why decision needed)
5. List alternatives considered
6. Document trade-offs
7. Note impact (new skills, patterns, files)
8. Set status to "Active"

**Example**:
```markdown
### DD-006: Event Sourcing for Workflows (2026-04-15) (NEW)

**Decision**: Use event sourcing for workflow state management.

**Context**: Need to track workflow history and support undo/redo.

**Alternatives Considered**:
1. State snapshots - Loses history
2. Command pattern - No automatic history

**Trade-offs**:
- ✅ Full audit trail
- ✅ Time-travel debugging
- ❌ More storage required

**Impact**:
- New pattern: ADVANCED_PATTERNS.md (Event Sourcing section)
- New files: `Modules/Workflow/Events/`, `Modules/Workflow/EventStore/`

**Status**: Active
```

### Pattern 4: Deprecate Pattern

**Steps**:
1. Update "Last Updated" date
2. Add "DEPRECATED" to pattern heading
3. Document why deprecated
4. Provide replacement pattern
5. Add migration guide
6. Keep pattern for reference (don't delete)

**Example**:
```markdown
## 3. Performance Optimization

### ~~setState() Batching~~ (DEPRECATED)

**Status**: DEPRECATED as of 2026-04-11 (React 18+ auto-batches)

**Replacement**: Use standard setState() - React 18 handles batching automatically.

**Migration**: Remove manual batching code:
```typescript
// OLD (v1.0 - DEPRECATED)
ReactDOM.unstable_batchedUpdates(() => {
    setState1(value1);
    setState2(value2);
});

// NEW (v2.0)
setState1(value1);  // Auto-batched by React 18
setState2(value2);
```

**Why Deprecated**: React 18+ provides automatic batching, making manual batching unnecessary and error-prone.
```

## Important Rules

- ✅ Always update "Last Updated" date
- ✅ Include complete code examples
- ✅ Document WHY, not just WHAT
- ✅ Explain trade-offs (pros/cons)
- ✅ Add cross-references to related patterns
- ✅ Use version numbers for significant updates
- ✅ Add migration notes for breaking changes
- ✅ Keep deprecated patterns (don't delete)
- ❌ Don't add patterns without rationale
- ❌ Don't skip trade-offs analysis
- ❌ Don't delete old patterns (mark deprecated instead)
- ❌ Don't add automatable patterns (use skills instead)

## Integration with Other Skills

**After creating skill from pattern**:
```bash
# 1. Document pattern in ADVANCED_PATTERNS.md (this skill)
/doc-update-technical ADVANCED_PATTERNS new-pattern "Async validation pattern with Task.Yield"

# 2. Create skill for automatable parts
/backend-validator ModValidator Name,Path required

# 3. Update guide to reference both
/doc-update-guide AI_GUIDE new-skill "Added backend-validator (see ADVANCED_PATTERNS.md for async patterns)"
```

**After architecture decision**:
```bash
# 1. Document decision (this skill)
/doc-update-technical DESIGN_DECISIONS new-decision "Use FluentValidation for all validation"

# 2. Document patterns (this skill)
/doc-update-technical ADVANCED_PATTERNS new-pattern "FluentValidation async rules pattern"

# 3. Update guide
/doc-update-guide AI_GUIDE new-feature "Added validation system with FluentValidation"
```

## When to Use ADVANCED_PATTERNS vs Skills

**Use ADVANCED_PATTERNS for**:
- Patterns requiring judgment (when to use X vs Y)
- Performance optimizations (case-by-case basis)
- Complex patterns with many variations
- Non-automatable deep technical patterns

**Use Skills for**:
- Repetitive boilerplate code
- Consistent patterns with clear structure
- Multi-file code generation
- Pattern enforcement (DI, events, i18n)

**Example**:
- **Skill**: `/backend-validator` - Generates validator boilerplate
- **ADVANCED_PATTERNS**: Async validation pattern - Documents WHEN and WHY to use Task.Yield

## Validation Checklist

After updating, verify:

- [ ] "Last Updated" date changed
- [ ] Code examples are complete and runnable
- [ ] WHY is documented (not just WHAT)
- [ ] Trade-offs are analyzed
- [ ] Related patterns are cross-referenced
- [ ] Version noted if significant update
- [ ] Migration guide added if breaking change
- [ ] NEW marker added for new patterns
- [ ] Deprecated patterns kept (not deleted)

## Reference Examples

**Good Pattern Documentation**:
```markdown
### Pattern Name

**Pattern**:
```csharp
// Complete, runnable code example
public class Example { ... }
```

**Why**: Clear rationale for why this pattern exists.

**Trade-offs**:
- ✅ Benefit with explanation
- ❌ Cost with explanation

**Related Patterns**: Cross-reference to X, Y
```

**Good Architecture Decision**:
```markdown
### DD-XXX: Decision Title (YYYY-MM-DD)

**Decision**: Clear statement of what was decided.

**Context**: Why this decision was needed.

**Alternatives Considered**:
1. Alternative 1 - Why rejected
2. Alternative 2 - Why rejected

**Trade-offs**:
- ✅ Benefit
- ❌ Cost

**Impact**: What changed (files, patterns, skills)

**Status**: Active/Deprecated
```

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial doc-update-technical skill

**How to update this skill**:
1. If ADVANCED_PATTERNS.md structure changes, update section patterns
2. If DESIGN_DECISIONS.md format changes, update decision template
3. If new pattern categories emerge, add to update patterns
4. Update examples as documentation evolves
