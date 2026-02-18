# Frontend Context Refactoring Plan

## Current Problems

### 1. Excessive Prop Drilling
- ModHierarchicalView has 20+ useState hooks
- Props passed through multiple component layers
- No centralized state management
- Difficult to share state between sibling components

### 2. Scattered State Logic
- State update logic duplicated across components
- No single source of truth for module state
- Hard to track state changes and side effects

### 3. Poor Event Flow
- Callbacks passed through multiple levels
- No clear event system
- Difficult to coordinate actions across components

## Solution: Module Context Pattern

Each module will have its own React Context providing:
1. **State**: Centralized module state
2. **Actions**: Dispatched actions to update state
3. **Selectors**: Derived/computed state
4. **Events**: Event emitter for cross-component communication

### Architecture Pattern

```
Module/
├── context/
│   ├── ModuleContext.tsx       # Context definition & provider
│   ├── ModuleState.ts          # State type definitions
│   └── ModuleActions.ts        # Action creators
├── components/
│   └── (all components use context instead of props)
└── services/
    └── moduleService.ts        # Backend API calls
```

## Modules to Refactor

### 1. Mods Module (Priority 1)
**Current Issues:**
- 20+ useState hooks in ModHierarchicalView
- Import state scattered across multiple components
- No shared state between table and preview panel

**Context Structure:**
```typescript
ModsContext {
  state: {
    // Data
    mods: ModInfo[]
    selectedMod: ModInfo | null
    selectedMods: ModInfo[]

    // UI State
    selectedObject: string
    searchQuery: string
    expandedKeys: React.Key[]

    // Import State
    importTasks: ImportTask[]
    importProcessing: boolean

    // Dialog State
    editDialogVisible: boolean
    tagDialogVisible: boolean
    batchEditDialogVisible: boolean
  }

  actions: {
    // Data Actions
    loadMods: () => Promise<void>
    selectMod: (mod: ModInfo | null) => void
    selectMods: (mods: ModInfo[]) => void

    // Mod Operations
    importMod: (filePath: string) => Promise<void>
    updateMod: (sha: string, data: Partial<ModInfo>) => Promise<void>
    deleteMod: (sha: string) => Promise<void>
    loadMod: (sha: string) => Promise<void>
    unloadMod: (sha: string) => Promise<void>

    // UI Actions
    setSelectedObject: (object: string) => void
    setSearchQuery: (query: string) => void
    toggleExpanded: (key: React.Key) => void

    // Dialog Actions
    openEditDialog: (mod: ModInfo) => void
    closeEditDialog: () => void
    openTagDialog: () => void
    closeTagDialog: () => void
  }

  selectors: {
    filteredMods: ModInfo[]
    modsGroupedByObject: Record<string, ModInfo[]>
    loadedModsCount: number
    objectNames: string[]
  }
}
```

### 2. Profiles Module (Priority 2)
**Current Issues:**
- Profile switching doesn't refresh all dependent data
- Active profile state not shared globally
- Config updates scattered

**Context Structure:**
```typescript
ProfilesContext {
  state: {
    profiles: Profile[]
    activeProfile: Profile | null
    profileConfig: ProfileConfiguration | null
    loading: boolean
  }

  actions: {
    loadProfiles: () => Promise<void>
    switchProfile: (profileId: string) => Promise<void>
    createProfile: (request: CreateProfileRequest) => Promise<void>
    updateProfile: (profileId: string, data: UpdateProfileRequest) => Promise<void>
    deleteProfile: (profileId: string) => Promise<void>
    updateConfig: (field: string, value: any) => Promise<void>
  }

  events: {
    onProfileSwitch: EventEmitter
    onConfigChange: EventEmitter
  }
}
```

### 3. Migration Module (Priority 3)
**Context Structure:**
```typescript
MigrationContext {
  state: {
    pythonPath: string
    analysis: MigrationAnalysis | null
    migrationResult: MigrationResult | null
    currentStep: number
    loading: boolean
  }

  actions: {
    autoDetect: () => Promise<void>
    analyzePath: (path: string) => Promise<void>
    startMigration: (options: MigrationOptions) => Promise<void>
    reset: () => void
  }
}
```

### 4. Tools Module (Priority 4)
**Context Structure:**
```typescript
ToolsContext {
  state: {
    cacheItems: CacheItem[]
    cacheStats: CacheStatistics | null
    validationReport: StartupValidationReport | null
    d3dVersions: D3DMigotoVersion[]
  }

  actions: {
    loadCache: () => Promise<void>
    clearCache: (category?: CacheCategory) => Promise<void>
    runValidation: () => Promise<void>
    loadD3DVersions: () => Promise<void>
    deployD3DVersion: (version: string) => Promise<void>
  }
}
```

## Implementation Strategy

### Phase 1: Create Context Infrastructure
1. Create `context/` directory in each module
2. Define state types and interfaces
3. Implement context provider with useReducer
4. Create custom hooks (useModsContext, useProfilesContext, etc.)

### Phase 2: Refactor Components
1. Replace useState with context
2. Remove prop drilling
3. Use context hooks instead of props
4. Simplify component hierarchy

### Phase 3: Add Event System
1. Implement EventEmitter for cross-module communication
2. Connect profile switches to mod reloads
3. Add loading states coordination

### Phase 4: Testing & Optimization
1. Test state updates
2. Verify event propagation
3. Optimize re-renders with selectors
4. Add error boundaries

## Benefits

### Before (Current)
```tsx
// App.tsx
const { mods, loadMods } = useModData();

<ModHierarchicalView
  mods={mods}
  onRefresh={loadMods}
  onLoad={handleLoadMod}
  onUnload={handleUnloadMod}
  onDelete={handleDeleteMod}
/>

// ModHierarchicalView.tsx (20+ useState hooks)
const [selectedMod, setSelectedMod] = useState(null);
const [editDialogVisible, setEditDialogVisible] = useState(false);
const [importTasks, setImportTasks] = useState([]);
// ... 17 more useState hooks
```

### After (With Context)
```tsx
// App.tsx
<ModsProvider>
  <ModHierarchicalView />
</ModsProvider>

// ModHierarchicalView.tsx
const { state, actions } = useModsContext();
// Clean, simple, no prop drilling!
```

## Code Examples

### Example 1: ModsContext Implementation

```typescript
// modules/mods/context/ModsContext.tsx
import React, { createContext, useContext, useReducer, useCallback } from 'react';
import { ModInfo } from '../../../shared/types/mod.types';
import { modService } from '../services/modService';

// State
interface ModsState {
  mods: ModInfo[];
  selectedMod: ModInfo | null;
  selectedMods: ModInfo[];
  loading: boolean;
  error: string | null;
}

// Actions
type ModsAction =
  | { type: 'SET_MODS'; payload: ModInfo[] }
  | { type: 'SELECT_MOD'; payload: ModInfo | null }
  | { type: 'SELECT_MODS'; payload: ModInfo[] }
  | { type: 'SET_LOADING'; payload: boolean }
  | { type: 'SET_ERROR'; payload: string | null }
  | { type: 'UPDATE_MOD'; payload: { sha: string; data: Partial<ModInfo> } };

// Reducer
function modsReducer(state: ModsState, action: ModsAction): ModsState {
  switch (action.type) {
    case 'SET_MODS':
      return { ...state, mods: action.payload, loading: false };
    case 'SELECT_MOD':
      return { ...state, selectedMod: action.payload };
    case 'SELECT_MODS':
      return { ...state, selectedMods: action.payload };
    case 'SET_LOADING':
      return { ...state, loading: action.payload };
    case 'SET_ERROR':
      return { ...state, error: action.payload };
    case 'UPDATE_MOD':
      return {
        ...state,
        mods: state.mods.map(mod =>
          mod.sha === action.payload.sha
            ? { ...mod, ...action.payload.data }
            : mod
        ),
      };
    default:
      return state;
  }
}

// Context
interface ModsContextValue {
  state: ModsState;
  actions: {
    loadMods: () => Promise<void>;
    selectMod: (mod: ModInfo | null) => void;
    selectMods: (mods: ModInfo[]) => void;
    importMod: (filePath: string) => Promise<void>;
    updateMod: (sha: string, data: Partial<ModInfo>) => Promise<void>;
    deleteMod: (sha: string) => Promise<void>;
    loadModInGame: (sha: string) => Promise<void>;
    unloadModFromGame: (sha: string) => Promise<void>;
  };
}

const ModsContext = createContext<ModsContextValue | undefined>(undefined);

// Provider
export const ModsProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [state, dispatch] = useReducer(modsReducer, {
    mods: [],
    selectedMod: null,
    selectedMods: [],
    loading: false,
    error: null,
  });

  const loadMods = useCallback(async () => {
    dispatch({ type: 'SET_LOADING', payload: true });
    try {
      const mods = await modService.getAllMods();
      dispatch({ type: 'SET_MODS', payload: mods });
    } catch (error) {
      dispatch({ type: 'SET_ERROR', payload: error.message });
    }
  }, []);

  const selectMod = useCallback((mod: ModInfo | null) => {
    dispatch({ type: 'SELECT_MOD', payload: mod });
  }, []);

  const importMod = useCallback(async (filePath: string) => {
    try {
      const imported = await modService.importMod(filePath);
      await loadMods(); // Refresh list
    } catch (error) {
      dispatch({ type: 'SET_ERROR', payload: error.message });
    }
  }, [loadMods]);

  // ... other actions

  const value = {
    state,
    actions: {
      loadMods,
      selectMod,
      selectMods: (mods) => dispatch({ type: 'SELECT_MODS', payload: mods }),
      importMod,
      updateMod: async (sha, data) => {
        await modService.updateMetadata(sha, data);
        dispatch({ type: 'UPDATE_MOD', payload: { sha, data } });
      },
      deleteMod: async (sha) => {
        await modService.deleteMod(sha);
        await loadMods();
      },
      loadModInGame: async (sha) => {
        await modService.loadMod(sha);
        await loadMods();
      },
      unloadModFromGame: async (sha) => {
        await modService.unloadMod(sha);
        await loadMods();
      },
    },
  };

  return <ModsContext.Provider value={value}>{children}</ModsContext.Provider>;
};

// Hook
export const useModsContext = () => {
  const context = useContext(ModsContext);
  if (!context) {
    throw new Error('useModsContext must be used within ModsProvider');
  }
  return context;
};
```

### Example 2: Using Context in Components

```tsx
// Before
const ModHierarchicalView: React.FC<{
  mods: ModInfo[];
  loading: boolean;
  onLoad: (sha: string) => void;
  onUnload: (sha: string) => void;
  onDelete: (sha: string) => void;
  onRefresh: () => void;
}> = ({ mods, loading, onLoad, onUnload, onDelete, onRefresh }) => {
  const [selectedMod, setSelectedMod] = useState<ModInfo | null>(null);
  // ... 19 more useState hooks

  return (
    <ModTable
      mods={mods}
      selectedMod={selectedMod}
      onSelectMod={setSelectedMod}
      onLoad={onLoad}
      onUnload={onUnload}
      onDelete={onDelete}
    />
  );
};

// After
const ModHierarchicalView: React.FC = () => {
  const { state, actions } = useModsContext();

  return (
    <ModTable
      mods={state.mods}
      selectedMod={state.selectedMod}
      onSelectMod={actions.selectMod}
      onLoad={actions.loadModInGame}
      onUnload={actions.unloadModFromGame}
      onDelete={actions.deleteMod}
    />
  );
};
```

## Migration Checklist

### Mods Module
- [ ] Create ModsContext.tsx
- [ ] Define ModsState interface
- [ ] Implement modsReducer
- [ ] Create ModsProvider
- [ ] Create useModsContext hook
- [ ] Refactor ModHierarchicalView
- [ ] Refactor ModTable
- [ ] Refactor ModPreviewPanel
- [ ] Update App.tsx to use provider

### Profiles Module
- [ ] Create ProfilesContext.tsx
- [ ] Implement ProfilesProvider
- [ ] Connect to global state
- [ ] Add profile switch events
- [ ] Refactor ProfileManager

### Migration Module
- [ ] Create MigrationContext.tsx
- [ ] Refactor MigrationWizard
- [ ] Add step navigation

### Tools Module
- [ ] Create ToolsContext.tsx
- [ ] Refactor ToolsView
- [ ] Separate cache/validation/d3dmigoto contexts

## Success Criteria

1. ✅ Zero prop drilling (no props passed > 1 level)
2. ✅ Centralized state per module
3. ✅ Clear action creators for all operations
4. ✅ Event-driven communication between modules
5. ✅ < 5 useState hooks per component
6. ✅ All state updates go through context
7. ✅ Components are presentational, contexts handle logic
8. ✅ Easy to test (can mock context values)

---

**Next Steps:** Implement ModsContext as proof of concept, then apply pattern to other modules.
