# Frontend Context Architecture

**Version:** 2.1
**Last Updated:** 2026-02-23
**Status:** Current Implementation

## Overview

React Context-based architecture aligned with backend's stateless, profile-scoped design.

## Architecture Principles

1. **Single Source of Truth** - ProfileContext manages all profile state
2. **Stateless Alignment** - Each request includes profileId parameter
3. **Type Safety** - `string | undefined` for optional profile ID

## Component Hierarchy

```
App.tsx
└─ ThemeProvider
   └─ SlideInScreenProvider
      └─ AppWithProfileInit
         └─ ProfileProvider          ← Profile state
            └─ AppInitializer        ← Initialization
               └─ AppContent         ← Main UI
                  └─ ModsProvider
                     └─ [Feature Components]
```

## ProfileContext API

### Interface
```typescript
interface ProfileContextValue {
  // State (direct access)
  selectedProfile: Profile | undefined;
  selectedProfileId: string | undefined;  // ⚠️ undefined, not null
  profiles: Profile[];
  loading: boolean;
  error: string | undefined;

  // Actions
  actions: {
    setSelectedProfile: (profile: Profile) => void;
    loadProfiles: () => Promise<void>;
    selectProfile: (profileId: string) => Promise<void>;
    createProfile: (name: string, description?: string) => Promise<Profile>;
    updateProfile: (profileId: string, name: string, description?: string) => Promise<void>;
    deleteProfile: (profileId: string) => Promise<void>;
  };
}
```

### Usage Pattern
```typescript
const { selectedProfileId, actions } = useProfile();

// Service calls with profile
const mods = await modService.getAllMods(selectedProfileId);

// Profile actions
await actions.updateProfile(profileId, name, description);
```

## Module Context Pattern

Each feature module follows this pattern:

### Provider Structure
```typescript
export const ModsProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { selectedProfileId } = useProfile();
  const [state, dispatch] = useReducer(modsReducer, initialState);

  // Effect syncs with profile changes
  useEffect(() => {
    if (selectedProfileId) {
      loadMods(selectedProfileId);
    } else {
      dispatch({ type: 'RESET' });
    }
  }, [selectedProfileId]);

  // Actions pass profileId from context
  const actions = {
    loadMods: async () => {
      const mods = await modService.getAllMods(selectedProfileId);
      dispatch({ type: 'SET_MODS', payload: mods });
    }
  };

  return (
    <ModsContext.Provider value={{ state, actions }}>
      {children}
    </ModsContext.Provider>
  );
};
```

### Hook Usage
```typescript
// Custom hook encapsulates context
export const useMods = () => {
  const context = useContext(ModsContext);
  if (!context) throw new Error('useMods must be used within ModsProvider');
  return context;
};

// Component usage
const ModList = () => {
  const { state, actions } = useMods();
  const { selectedProfileId } = useProfile();

  useEffect(() => {
    actions.loadMods();
  }, [selectedProfileId]);

  return <div>{state.mods.map(...)}</div>;
};
```

## Service Integration

### BaseModuleService Pattern
```typescript
// Services receive profileId as parameter
class ModService extends BaseModuleService {
  constructor() { super('MOD'); }

  async getAllMods(profileId?: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_ALL', profileId);
  }

  async loadMod(profileId: string, id: string): Promise<boolean> {
    return this.sendBooleanMessage('LOAD', profileId, { id });
  }
}
```

### IPC Message Format
```typescript
{
  id: string;
  module: ModuleName;
  type: string;
  profileId?: string;  // Top-level, not in payload
  payload?: unknown;
}
```

## State Management Patterns

### Loading States
```typescript
// Three-stage loading for UX
const [loadingState, setLoadingState] = useState<
  'immediate' | 'delayed' | 'complete'
>('immediate');

useEffect(() => {
  const timer1 = setTimeout(() => setLoadingState('delayed'), 100);
  const timer2 = setTimeout(() => setLoadingState('complete'), 300);
  return () => { clearTimeout(timer1); clearTimeout(timer2); };
}, []);

// Progressive UI reveal
if (loadingState === 'immediate') return null;
if (loadingState === 'delayed') return <Spin />;
return <Content />;
```

### Error Handling
```typescript
catch (error: unknown) {
  const msg = error instanceof Error ? error.message : 'Unknown error';
  notification.error(msg);
}
```

### Data Conventions
- Use `undefined` for missing data (NOT `null`)
- Use `null` only for React render returns
- Always include type guards for error handling

## Module Contexts

### Available Contexts
- **ProfileContext** - Profile management
- **ModsContext** - Mod operations
- **SettingsContext** - Application settings
- **ThemeContext** - Theme/UI preferences
- **SlideInScreenContext** - Navigation state

### Context Files
```
src/shared/context/
├── ProfileContext.tsx
├── SettingsContext.tsx
├── ThemeContext.tsx
└── SlideInScreenContext.tsx

src/modules/{module}/context/
└── {Module}Context.tsx
```

## Best Practices

1. **Always use hooks** - Never access context directly
2. **Profile-aware contexts** - Reset state when profile changes
3. **Type safety** - Use TypeScript strict mode
4. **Error boundaries** - Wrap providers with error boundaries
5. **Memoization** - Use React.memo and useMemo for performance

## Related Documentation

- [CURRENT_ARCHITECTURE.md](CURRENT_ARCHITECTURE.md) - System overview
- [FRONTEND_SERVICE_ARCHITECTURE.md](FRONTEND_SERVICE_ARCHITECTURE.md) - Service patterns
- [REACT_CLOSURE_PATTERNS.md](../ai-assistant/REACT_CLOSURE_PATTERNS.md) - Hook patterns