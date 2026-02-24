# Classification System

**Last Updated:** 2026-02-23
**Status:** ✅ Active

## Overview

The Classification System provides a hierarchical tree structure for organizing mods into categories. It uses stable GUIDs for classification IDs while displaying human-readable names to users. The system supports drag-and-drop operations, smart refresh logic, and efficient category name mapping.

---

## Architecture

### Backend (C#)

#### ClassificationService
- **Location:** `D3dxSkinManager/Modules/Mods/Services/ClassificationService.cs`
- **Purpose:** Manages classification tree in SQLite database
- **Key Features:**
  - GUID-based node IDs for stability (no cascading updates on rename)
  - Name uniqueness enforced globally across entire database (case-insensitive)
  - Hierarchical parent-child relationships
  - Priority-based ordering

#### ModFacade Category Mapping
- **Location:** `D3dxSkinManager/Modules/Mods/ModFacade.cs`
- **Method:** `PopulateCategoryNamesBulkAsync()`
- **Purpose:** Maps classification IDs (GUIDs) to human-readable names
- **Process:**
  1. Extract unique category IDs from mods
  2. Fetch classification tree once
  3. Build flat ID→Name dictionary
  4. Populate CategoryName field on each mod
  5. Fallback to ID if name not found (legacy/deleted categories)

### Frontend (React/TypeScript)

#### Classification Tree Component
- **Location:** `D3dxSkinManager.Client/src/modules/mods/components/ClassificationPanel/`
- **Components:**
  - `ClassificationTree.tsx` - Main tree UI
  - `ClassificationTreeContext.tsx` - State management
  - `useClassificationTreeOperations.tsx` - Operations hook

#### Smart Refresh Logic
- **Function:** `shouldRefreshModsForNodeUpdate()`
- **Location:** `useClassificationTreeOperations.tsx`
- **Algorithm:**
  ```typescript
  // Refresh mod list ONLY if:
  // 1. The name actually changed AND
  // 2. One of these conditions:
  //    - Updated node IS the selected node
  //    - Updated node is DESCENDANT of selected node
  //
  // Does NOT refresh if:
  // - Updated node is ancestor (doesn't affect view)
  // - Only non-name properties changed
  ```

---

## Data Model

### Classification Node Structure
```typescript
interface ClassificationNode {
  id: string;           // GUID (stable, never changes)
  name: string;         // Display name (can be updated)
  parentId?: string;    // Parent node GUID
  priority: number;     // Sort order
  description?: string; // Optional description
  thumbnail?: string;   // Optional thumbnail path
  children: ClassificationNode[];
  modCount: number;     // Count of mods in this node
}
```

### Mod Category Fields
```typescript
interface ModInfo {
  category: string;      // Classification ID (GUID or legacy path)
  categoryName?: string; // Human-readable name for display
  // ... other fields
}
```

---

## Key Features

### 1. Stable GUID System
- **Problem Solved:** Renaming nodes no longer requires cascading updates
- **Implementation:**
  - Each node gets a UUID on creation
  - ID never changes, only name updates
  - Mods reference the stable GUID

### 2. Hierarchical Mod Display
- **Behavior:** When a parent node is selected, it shows:
  - Mods directly in that node
  - Mods from ALL descendant nodes
- **Impact on Refresh:** Must refresh if descendant changes

### 3. Smart Refresh Optimization
- **When to Refresh:**
  - Name of current node changed
  - Name of descendant node changed
- **When NOT to Refresh:**
  - Name of ancestor changed
  - Non-name properties changed
  - Unrelated nodes changed

### 4. Drag & Drop Support
- **Operations:**
  - Drag mods to classification nodes
  - Drag nodes to reorder/reparent
  - Drop on "Unclassified" to remove category
- **Hook:** `useDragDrop()` with declarative configuration

---

## User Experience

### Category Name Display
1. **Before:** Mods showed GUID in category tag (e.g., "a4b5c6d7-e8f9...")
2. **After:** Mods show readable name (e.g., "Character/Outfit/Summer")

### Performance Optimizations
- **Bulk Operations:** Single tree fetch for all category names
- **Delayed Loading:** Uses `useDelayedLoading` pattern (100ms threshold)
- **Selective Refresh:** Only refreshes affected mod views

---

## Migration from Path-Based System

### Legacy System (Path-Based)
- IDs were paths: "Character/Outfit/Summer"
- Renaming required updating all child paths
- Renaming required updating all mod references
- Complex cascading update logic

### New System (GUID-Based)
- IDs are GUIDs: "a4b5c6d7-e8f9-..."
- Renaming only updates the name field
- Mod references remain unchanged
- Simple, efficient updates

---

## Testing Considerations

### Unit Tests
- **Location:** `D3dxSkinManager.Tests/Modules/Mods/ClassificationServiceGuidTests.cs`
- **Coverage:**
  - GUID generation
  - Name uniqueness globally across database (case-insensitive)
  - Tree operations (create, update, delete)
  - No cascading updates on rename

### Manual Testing Checklist
- [ ] Create classification with name
- [ ] Rename classification - verify mods update
- [ ] Select parent - verify shows all descendant mods
- [ ] Rename child - verify parent view updates
- [ ] Rename ancestor - verify child view NOT updated
- [ ] Drag mod to classification
- [ ] Delete classification with mods

---

## Related Documentation

- [DELAYED_LOADING_UX_PATTERN.md](DELAYED_LOADING_UX_PATTERN.md) - Loading pattern used
- [architecture/DOMAIN_DESIGN.md](../architecture/DOMAIN_DESIGN.md) - Service architecture
- [changelogs/2026-02/](../changelogs/2026-02/) - Implementation history

---

## Code Examples

### Backend: Populating Category Names
```csharp
private async Task PopulateCategoryNamesBulkAsync(List<ModInfo> mods)
{
    // Get unique category IDs
    var categoryIds = mods
        .Where(m => !string.IsNullOrEmpty(m.Category))
        .Select(m => m.Category)
        .Distinct()
        .ToList();

    // Get classification tree once
    var tree = await _classificationService.GetClassificationTreeAsync();

    // Build lookup dictionary
    var nodeMap = new Dictionary<string, string>();
    BuildNodeMap(tree, nodeMap);

    // Populate names
    foreach (var mod in mods)
    {
        if (nodeMap.TryGetValue(mod.Category, out var name))
            mod.CategoryName = name;
        else
            mod.CategoryName = mod.Category; // Fallback
    }
}
```

### Frontend: Smart Refresh Check
```typescript
const nameChanged = data.name !== node.name;
if (onModsRefresh &&
    nameChanged &&
    shouldRefreshModsForNodeUpdate(tree, nodeId, selectedId)) {
    await onModsRefresh();
}
```

---

## Future Enhancements

1. **Bulk Operations:** Batch rename multiple classifications
2. **Import/Export:** Save/load classification structures
3. **Templates:** Pre-defined classification hierarchies
4. **Search:** Find mods across all classifications
5. **Permissions:** Read-only classifications for shared profiles