# Mod Module Redundancy Analysis

**Date:** 2026-02-20
**Analyst:** AI Assistant
**Status:** Analysis Complete - Awaiting Refactoring Approval

## Executive Summary

The mod module contains significant duplication between `useModData` and `ModsContext`. The main issue is **duplicate load/unload operations** where `ModsContext` wraps `useModData` functions but adds classification filtered list updates.

**Estimated Code Reduction:** ~100 lines
**Bundle Size Impact:** ~500-800 bytes
**Complexity Reduction:** High

---

## 1. Duplicate Load/Unload Functions

### Issue

Both `useModData.ts` and `ModsContext.tsx` implement `loadModInGame` and `unloadModFromGame` with nearly identical logic.

#### useModData.ts (Lines 107-167)
```typescript
const loadModInGame = async (profileId: string, sha: string) => {
  // Apply optimistic update to mods
  dispatch({ type: "UPDATE_MOD_LOCAL", payload: { sha, data: { isLoaded: true } } });

  try {
    await modService.loadMod(profileId, sha);
    notification.success("Mod loaded successfully");
    await loadMods(profileId);  // Refresh
  } catch (error) {
    dispatch({ type: "UPDATE_MOD_LOCAL", payload: { sha, data: { isLoaded: false } } });
    notification.error("Failed to load mod");
  }
};
```

#### ModsContext.tsx (Lines 265-303)
```typescript
const loadModInGame = async (sha: string) => {
  // Apply optimistic update to mods AND filtered list
  modData.dispatch({ type: "UPDATE_MOD_LOCAL", payload: { sha, data: { isLoaded: true } } });
  classificationData.dispatch({ type: "UPDATE_FILTERED_MOD", payload: { sha, data: { isLoaded: true } } });

  try {
    await modService.loadMod(selectedProfileId, sha);
    notification.success("Mod loaded successfully");
    await modData.loadMods(selectedProfileId);  // Refresh
  } catch (error) {
    modData.dispatch({ type: "UPDATE_MOD_LOCAL", payload: { sha, data: { isLoaded: false } } });
    classificationData.dispatch({ type: "UPDATE_FILTERED_MOD", payload: { sha, data: { isLoaded: false } } });
    notification.error("Failed to load mod");
  }
};
```

### Key Differences

| Aspect | useModData | ModsContext |
|--------|-----------|-------------|
| **ProfileId** | Passed as parameter | Uses `selectedProfileId` from context |
| **Optimistic Updates** | Updates `mods` only | Updates `mods` + `classificationFilteredMods` |
| **Error Recovery** | Reverts `mods` only | Reverts both `mods` + `classificationFilteredMods` |

### Recommendation

**Option A: Keep ModsContext, Remove from useModData (Recommended)**
- `ModsContext` is the single source of truth for all mod operations
- `useModData` should only handle low-level data management
- **Benefit:** Clear separation - UI operations in Context, data in hook
- **Impact:** Remove ~60 lines from `useModData.ts`

**Option B: Keep Both, Add Documentation**
- Document that `useModData` is for direct use, `ModsContext` adds classification sync
- **Benefit:** Flexibility for components that don't need classification sync
- **Impact:** Add comments only

---

## 2. Reducer Actions Analysis

### modsDataReducer.ts

```typescript
export type ModsDataAction =
  | { type: "SET_MODS"; payload: ModInfo[] }
  | { type: "SET_LOADING"; payload: boolean }
  | { type: "SET_ERROR"; payload: string | null }
  | { type: "SELECT_MOD"; payload: ModInfo | null }
  | { type: "SELECT_MODS"; payload: ModInfo[] }
  | { type: "UPDATE_MOD_LOCAL"; payload: { sha: string; data: Partial<ModInfo> } };
```

**Analysis:** All actions are used. No redundancy detected.

### classificationReducer.ts

```typescript
export type ClassificationAction =
  | { type: "SET_CLASSIFICATION_TREE"; payload: ClassificationNode[] }
  | { type: "SET_CLASSIFICATION_LOADING"; payload: boolean }
  | { type: "SELECT_CLASSIFICATION"; payload: ClassificationNode | null }
  | { type: "SET_CLASSIFICATION_FILTERED_MODS"; payload: ModInfo[] | null }
  | { type: "SET_CLASSIFICATION_SEARCH"; payload: string }
  | { type: "UPDATE_FILTERED_MOD"; payload: { sha: string; data: Partial<ModInfo>; newCategory?: string } };
```

**Analysis:** All actions are used. No redundancy detected.

---

## 3. Context Structure Analysis

### Current Structure

```
ModsContext (top-level)
├── useModData (mods state)
├── useClassificationData (tree + filtered mods state)
├── useImportOperations (import state)
└── useModsUIState (UI state)
```

### Issues

1. **ModsContext re-implements operations from hooks**
   - `loadModInGame` / `unloadModFromGame` duplicate `useModData`
   - `updateModCategory` could potentially live in `useModData`

2. **Unclear responsibility boundaries**
   - Is `ModsContext` an orchestrator or a data provider?
   - Should UI operations live in Context or in hooks?

### Recommendation

**Establish Clear Layering:**

```
Layer 1: Data Hooks (useModData, useClassificationData)
↓
Layer 2: Context (ModsContext) - Orchestrates multi-domain operations
↓
Layer 3: Components - UI logic only
```

**Rules:**
- **Data Hooks:** Single-domain CRUD operations (mods OR classifications)
- **Context:** Cross-domain operations (mods + classifications together)
- **Components:** UI rendering, event handling

---

## 4. Proposed Refactoring

### Phase 1: Remove Duplicate Load/Unload from useModData

**Files to Modify:**
- `useModData.ts` - Remove `loadModInGame` and `unloadModFromGame`
- `ModsContext.tsx` - Keep as is (already uses classification sync)

**Impact:**
- ~60 lines removed
- Clearer separation of concerns
- No breaking changes (ModsContext is the public API)

### Phase 2: Consolidate Category Update Logic

**Current:** `ModsContext` handles category updates with complex tree count logic

**Proposal:** Move to dedicated hook `useModCategoryOperations`
- Already exists: `useModCategoryUpdate.ts` (but only used in ClassificationPanel)
- Expand to handle all category operations
- Remove from ModsContext

**Impact:**
- ~80 lines removed from ModsContext
- Reusable category update logic
- Easier to test

### Phase 3: Document Layering

Add JSDoc comments to clarify:
- Which operations belong in which layer
- When to use hooks directly vs Context

---

## 5. Action Items

- [ ] **Decision:** Choose Option A or Option B for load/unload duplication
- [ ] **Refactor:** Remove duplicate functions from useModData (if Option A)
- [ ] **Extract:** Move category update logic to dedicated hook
- [ ] **Document:** Add layer boundaries to AI_GUIDE.md
- [ ] **Update:** CHANGELOG.md with refactoring work
- [ ] **Test:** Ensure all mod operations still work

---

## 6. Questions for User

1. **Should we remove `loadModInGame`/`unloadModFromGame` from `useModData`?**
   - Pro: Cleaner separation, ModsContext is single source
   - Con: Less flexibility for direct usage

2. **Should we extract category update logic to `useModCategoryOperations`?**
   - Pro: Reusable, testable, cleaner ModsContext
   - Con: One more hook to maintain

3. **Priority:** What should we tackle first?
   - Option 1: Remove duplicates (quick win)
   - Option 2: Full refactoring (more time)
   - Option 3: Just document (no code changes)

