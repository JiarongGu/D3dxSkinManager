---
name: post-feature
description: Use after completing a feature or bug fix to audit what changed and evolve docs/skills. Detects new IPC messages, store state, components, and patterns, then suggests or runs the right doc-update skills. Proactively use this skill after any non-trivial feature work.
---

# Post-Feature Audit

Audit recent changes and evolve the documentation/skill system so future sessions benefit from what was built.

**Purpose**: Code generation sessions add new IPC endpoints, store state, components, and patterns — but docs never get updated. This skill closes the loop by detecting what changed and triggering the right doc-update skills.

## When to Use

**Mandatory** after completing any non-trivial feature (per CLAUDE.md Step 6).
Skip only for trivial fixes (typo, single-line CSS tweak, config change).

## Arguments

**Format**: `/post-feature`

No arguments needed — the skill analyzes the current git diff automatically.

## What This Skill Does

### Phase 1: Detect Changes

Analyze `git diff` (staged + unstaged, or since last merge-base) to detect:

| Change Type | Detection Method |
|---|---|
| **New IPC messages** | New entries in `*Facade.cs` `RouteMessageAsync` switch |
| **New backend service methods** | New methods in `I*Service.cs` interfaces |
| **New frontend IPC methods** | New methods in `*Service.ts` (extends BaseModuleService) |
| **New store state** | New fields in `*Store.ts` state interfaces |
| **New React components** | New `.tsx` files or new `export const` components |
| **New CSS classes** | New `.css` files |
| **New i18n keys** | New entries in `Languages/en.json` or `Languages/cn.json` |
| **New hooks** | New `use*.ts` files or new exported hook functions |
| **New patterns** | Repeated code structures not covered by existing skills |

### Phase 2: Classify Impact

Categorize each detected change:

- **Reference update needed** — New IPC messages, constants, file paths
- **Guide update needed** — New workflow steps, new skill candidates
- **Technical doc update needed** — Non-obvious patterns, architecture decisions
- **No update needed** — Internal refactors, test changes, CSS tweaks

### Phase 3: Generate Report

Output a structured report:

```markdown
## Post-Feature Audit Report

### Changes Detected
- [x] New IPC message: `BATCH_MOVE_CATEGORIES` in CategoryFacade
- [x] New service method: `BatchUpdateParentAsync` in ICategoryService
- [x] New frontend IPC method: `batchMoveCategories` in categoryService.ts
- [x] New store state: `selectedCategoryIds` in modsStore.ts
- [ ] New component: (none)
- [ ] New pattern candidate: (none)

### Recommended Doc Updates

1. **KEYWORDS_INDEX / REFERENCE** (run `/doc-update-reference`):
   - Add `BATCH_MOVE_CATEGORIES` IPC message under Category module
   - Add `selectedCategoryIds` store field reference

2. **AI_GUIDE** (run `/doc-update-guide`):
   - (no workflow changes needed)

3. **ADVANCED_PATTERNS / DESIGN_DECISIONS** (run `/doc-update-technical`):
   - (no new non-obvious patterns)

### Skill Candidates
- (none detected — multi-select is a one-off feature pattern)

### Actions
- [ ] Run suggested doc-update commands
- [ ] Review if any pattern should become a new skill
```

### Phase 4: Execute (with confirmation)

After showing the report, ask:
> "Want me to run the suggested doc updates?"

If confirmed, execute the relevant `/doc-update-*` skills with the detected changes.

## Detection Rules

### New IPC Messages

Search for new lines matching:
```csharp
"MESSAGE_TYPE" => await HandlerAsync(request),
```
in `*Facade.cs` files within the diff.

### New Service Methods

Search for new lines in `I*Service.cs` interfaces:
```csharp
Task<ReturnType> MethodNameAsync(params);
```

### New Frontend IPC Methods

Search for new methods in files extending `BaseModuleService`:
```typescript
async methodName(profileId: string, ...): Promise<T> {
  return this.sendMessage<T>("MESSAGE_TYPE", ...);
}
```

### New Store State

Search for new fields in Zustand store state interfaces:
```typescript
export interface *State {
  newField: Type;  // <-- new line in diff
}
```

### New Components

Search for new `.tsx` files or new `export const ComponentName: React.FC` declarations.

### Pattern Candidates

Flag when the same structural pattern appears 3+ times in the diff without a corresponding skill. Example: if you see 3 new `useCallback` + `useStableRef` combos, suggest creating a skill for it.

## Important Rules

- Only audit changes in the current working tree (not historical commits)
- Don't auto-run doc updates without user confirmation
- Skip trivial changes (formatting, comments, import reordering)
- If no updates are needed, say so — don't force unnecessary doc changes
- Focus on what future sessions would need to know, not what's obvious from code

## Integration with Workflow

This skill is the bridge between code generation and documentation evolution:

```
Feature work complete
       |
       v
/post-feature  <-- THIS SKILL
       |
       v
Audit report generated
       |
       v
User confirms  ──> /doc-update-reference (IPC, constants, paths)
                ──> /doc-update-guide (workflows, skills table)
                ──> /doc-update-technical (patterns, decisions)
                ──> New skill creation (if pattern detected)
```

## Evolution Note

**Version History**:
- v1.0 (2026-04-12): Initial post-feature audit skill
