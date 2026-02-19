# Frontend Context Architecture

**Version:** 2.0
**Last Updated:** 2026-02-19
**Status:** ✅ Implemented

## Overview

The D3dxSkinManager frontend uses a **React Context-based architecture** that aligns with the backend's stateless, profile-scoped design. This document describes the current implementation after the major refactoring completed on 2026-02-19.

---

## Key Changes from Previous Implementation

### What Changed
- ✅ Removed `window.__selectedProfileId` global pattern
- ✅ ProfileContext now exposes `selectedProfileId` directly
- ✅ Changed `selectedProfileId` type from `string | null` to `string | undefined`
- ✅ Simplified component hierarchy (no profile prop drilling)
- ✅ Fixed IPC message format (profileId at top level, not in payload)
- ✅ Updated all module contexts to use new ProfileContext API

### Why These Changes
- **Type Safety:** `string | undefined` matches IPC message optional properties
- **Clean API:** Direct property access instead of nested state object
- **Consistency:** All components follow same pattern for accessing profile
- **Maintainability:** Single source of truth for profile state

---

## Architecture Principles

### 1. **Single Source of Truth**
- **ProfileContext** is the only place managing profile state
- No prop drilling - all components use `useProfile()` hook
- No global variables or window properties

### 2. **Stateless Alignment**
- Frontend matches backend's stateless profile architecture
- Each request includes `profileId` parameter
- No backend-side "active" profile state

### 3. **Type Safety**
- `selectedProfileId` is `string | undefined` to match IPC message types
- All services properly typed for TypeScript strict mode
- Optional chaining used consistently

---

## Component Hierarchy

```
┌─────────────────────────────────────────────────────────────┐
│ App.tsx (Root)                                              │
│  └─ ThemeProvider                                           │
│      └─ SlideInScreenProvider                               │
│          └─ AppWithProfileInit                              │
│              └─ ProfileProvider          ← Profile state    │
│                  └─ AppInitializer      ← Initialization    │
│                      └─ AppContent       ← Main UI          │
│                           └─ ModsProvider                    │
│                               └─ ModHierarchicalView         │
│                                                              │
│ All child components use useProfile() hook to:              │
│  - Access selectedProfile                                   │
│  - Call profile actions (switch, create, update, delete)    │
│  - Get profileId for service calls                          │
└─────────────────────────────────────────────────────────────┘
```

---

## ProfileContext API

### Location
`D3dxSkinManager.Client/src/shared/context/ProfileContext.tsx`

### Interface

```typescript
interface ProfileContextValue {
  // State (direct access, no nesting)
  selectedProfile: Profile | null;
  selectedProfileId: string | undefined;  // ⚠️ undefined, not null
  profiles: Profile[];
  loading: boolean;
  error: string | null;

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

### Usage Examples

**✅ Correct:**
```typescript
const { selectedProfile, selectedProfileId, profiles, actions } = useProfile();

// Access profile ID for service calls
const mods = await modService.getAllMods(selectedProfileId);

// Update profile
await actions.updateProfile(profileId, newName, newDescription);

// Check if profile is selected
if (!selectedProfileId) {
  return <div>No profile selected</div>;
}
```

**❌ Incorrect (Old Pattern - DO NOT USE):**
```typescript
// Old nested state pattern
const { state: profileState } = useProfile();
const profileId = profileState.selectedProfile?.id;

// Old window global pattern (REMOVED!)
const profileId = window.__selectedProfileId;

// Old helper function (REMOVED!)
const profileId = getCurrentProfileId();
```

---

## Initialization Flow

### AppInitializer Sequence

The initialization happens in 4 stages:

#### Stage 1: Load Global Settings
```typescript
const settings = await settingsService.getGlobalSettings();
setState({ stage: 'loading-global', globalSettings: settings });
```

#### Stage 2: Load All Profiles
```typescript
setState({ stage: 'loading-profiles' });
await actions.loadProfiles();  // Uses ProfileContext
```

#### Stage 3: Select Initial Profile
```typescript
const profileToSelect = profiles.find(p => p.isActive) || profiles[0];
await actions.selectProfile(profileToSelect.id);
```

#### Stage 4: Ready
```typescript
if (selectedProfile && stage !== 'ready') {
  setState({ stage: 'ready' });
}
// Render main UI
```

### Initialization Stages

| Stage | Description | UI Display |
|-------|-------------|------------|
| `loading-global` | Loading settings | "Loading settings..." |
| `loading-profiles` | Loading profiles | "Loading profiles..." |
| `selecting-profile` | No profiles exist, waiting for user to create one | "Please create your first profile" |
| `ready` | Initialization complete, render main UI | Main application |
| `error` | Initialization failed | Error message |

### Debug Information

The loading screen shows debug info during development:
```typescript
Stage: {state.stage}, Profiles: {profiles.length}, Selected: {selectedProfile ? 'Yes' : 'No'}
```

This helps diagnose initialization issues quickly.

---

## Service Integration

### Module Context Pattern

All module contexts (ModsContext, SettingsContext, etc.) should follow this pattern:

```typescript
export const ModsProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [state, dispatch] = useReducer(modsReducer, initialState);
  const { selectedProfile, selectedProfileId } = useProfile();  // ✅ Correct API

  // Load mods when profile changes
  useEffect(() => {
    if (selectedProfile && selectedProfileId) {
      console.log('[ModsContext] Profile changed, loading mods for:', selectedProfile.name);
      loadMods(selectedProfileId);
      loadClassificationTree(selectedProfileId);
    } else {
      // Clear data if no profile selected
      console.log('[ModsContext] No profile selected, clearing data');
      dispatch({ type: "SET_MODS", payload: [] });
      dispatch({ type: "SET_CLASSIFICATION_TREE", payload: [] });
    }
  }, [selectedProfileId]);  // ✅ React to profileId changes

  const loadMods = useCallback(async (profileId?: string) => {
    const currentProfileId = profileId || selectedProfileId;
    if (!currentProfileId) return;

    dispatch({ type: "SET_LOADING", payload: true });
    try {
      const mods = await modService.getAllMods(currentProfileId);
      dispatch({ type: "SET_MODS", payload: mods });
    } catch (error) {
      dispatch({ type: "SET_ERROR", payload: error.message });
    }
  }, [selectedProfileId]);  // ✅ Include in dependencies

  // ... other operations
};
```

### Key Points

1. **Destructure directly:** `const { selectedProfileId } = useProfile()`
2. **React to changes:** Include `selectedProfileId` in useEffect dependencies
3. **Guard against null:** Always check `if (!selectedProfileId) return;`
4. **Pass explicitly:** Pass profileId to service methods explicitly

---

## IPC Message Format

### ⚠️ CRITICAL: profileId Goes at Top Level, NOT in Payload

```typescript
// ✅ Correct: profileId at top level
await photinoService.sendMessage({
  module: 'MOD',
  type: 'GET_ALL',
  profileId: selectedProfileId,  // Top-level property
});

// ❌ WRONG: profileId in payload (causes "Profile ID is required" error)
await photinoService.sendMessage({
  module: 'MOD',
  type: 'GET_ALL',
  payload: { profileId }  // Wrong location!
});
```

### Message Type Definition

```typescript
export interface PhotinoMessage {
  id: string;
  module: ModuleName;
  type: MessageType;
  profileId?: string;  // Optional, top-level
  payload?: any;       // Additional data
}
```

### Example Service Call

```typescript
// classificationService.ts
async getClassificationTree(profileId: string): Promise<ClassificationNode[]> {
  return await photinoService.sendMessage<ClassificationNode[]>({
    module: 'MOD',
    type: 'GET_CLASSIFICATION_TREE',
    profileId  // ✅ Top level, not in payload
  });
}
```

---

## Common Patterns

### 1. Component with Profile Data

```typescript
const MyComponent: React.FC = () => {
  const { selectedProfile, selectedProfileId } = useProfile();
  const [data, setData] = useState(null);

  useEffect(() => {
    if (selectedProfileId) {
      loadData();
    }
  }, [selectedProfileId]);

  const loadData = async () => {
    if (!selectedProfileId) return;

    const result = await someService.getData(selectedProfileId);
    setData(result);
  };

  if (!selectedProfile) {
    return <div>No profile selected</div>;
  }

  return (
    <div>
      <h2>Profile: {selectedProfile.name}</h2>
      {/* ... */}
    </div>
  );
};
```

### 2. Profile Selection UI

```typescript
const ProfileSelector: React.FC = () => {
  const { selectedProfile, profiles, actions } = useProfile();

  const handleProfileChange = async (profileId: string) => {
    try {
      await actions.selectProfile(profileId);
      message.success('Profile switched');
    } catch (error) {
      message.error('Failed to switch profile');
    }
  };

  return (
    <Select
      value={selectedProfile?.id}
      onChange={handleProfileChange}
      style={{ width: 200 }}
    >
      {profiles.map(p => (
        <Option key={p.id} value={p.id}>
          {p.name}
        </Option>
      ))}
    </Select>
  );
};
```

### 3. Creating New Profile

```typescript
const CreateProfileButton: React.FC = () => {
  const { actions } = useProfile();
  const [loading, setLoading] = useState(false);

  const handleCreate = async () => {
    setLoading(true);
    try {
      const profile = await actions.createProfile('New Profile', 'Description');
      await actions.selectProfile(profile.id);  // Auto-select
      message.success('Profile created');
    } catch (error) {
      message.error('Failed to create profile');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Button
      type="primary"
      onClick={handleCreate}
      loading={loading}
    >
      Create Profile
    </Button>
  );
};
```

---

## Migration Guide (Old → New)

### ProfileContext Usage

| Old Pattern | New Pattern | Why |
|------------|-------------|-----|
| `const { state: profileState } = useProfile()` | `const { selectedProfile, selectedProfileId } = useProfile()` | Cleaner API, direct access |
| `profileState.selectedProfile?.id` | `selectedProfileId` | No optional chaining needed |
| `profileState.selectedProfile` | `selectedProfile` | Simpler |
| `profileState.profiles` | `profiles` | Consistent |
| `window.__selectedProfileId` | `selectedProfileId` from `useProfile()` | No globals |
| `getCurrentProfileId()` | `const { selectedProfileId } = useProfile()` | Hook-based |

### Service Calls

| Old Pattern | New Pattern |
|------------|-------------|
| `await service.method(profileState.selectedProfile?.id)` | `await service.method(selectedProfileId)` |
| `const pid = getCurrentProfileId(); await service.method(pid)` | `const { selectedProfileId } = useProfile(); await service.method(selectedProfileId)` |

### Dependency Arrays

| Old Pattern | New Pattern |
|------------|-------------|
| `[profileState.selectedProfile?.id]` | `[selectedProfileId]` |
| `[profileState.selectedProfile]` | `[selectedProfile]` or `[selectedProfileId]` depending on use |

### IPC Messages

| Old Pattern | New Pattern |
|------------|-------------|
| `sendMessage({ ..., payload: { profileId } })` | `sendMessage({ ..., profileId })` |

---

## Type Safety Notes

### Why `string | undefined` instead of `string | null`?

**Reason:** TypeScript optional properties use `undefined`, not `null`

```typescript
// IPC message type
interface PhotinoMessage {
  profileId?: string;  // Optional = string | undefined (not null!)
}

// ProfileContext must match
selectedProfileId: string | undefined;  // ✅ Correct
selectedProfileId: string | null;       // ❌ Type mismatch
```

**TypeScript Semantics:**
- `undefined` = property doesn't exist or not set
- `null` = property exists but intentionally has no value

Since our IPC messages use optional properties (`profileId?:`), we use `undefined` for type consistency.

---

## Troubleshooting

### Issue: "Profile ID is required for module: MOD"

**Symptoms:**
- Backend logs show: `[IPC] Error: Profile ID is required for module: MOD`
- Request shows: `ProfileId: ` (empty)

**Cause:** Service call missing `profileId` or passing it in wrong location

**Fix:**
```typescript
// ✅ Correct - profileId at top level
await photinoService.sendMessage({
  module: 'MOD',
  type: 'GET_CLASSIFICATION_TREE',
  profileId: selectedProfileId  // Top level!
});

// ❌ Wrong - profileId in payload
await photinoService.sendMessage({
  module: 'MOD',
  type: 'GET_CLASSIFICATION_TREE',
  payload: { profileId }  // Should be top level!
});
```

**How to Verify:**
Check backend logs:
```
[IPC] Request: GET_CLASSIFICATION_TREE (Module: MOD, ProfileId: default)  ✅ Good
[IPC] Request: GET_CLASSIFICATION_TREE (Module: MOD, ProfileId: )         ❌ Bad
```

---

### Issue: App Stuck on "Loading profiles..."

**Symptoms:**
- Spinner shows indefinitely
- Debug info shows: `Stage: loading-profiles, Profiles: 0, Selected: No`

**Cause:** ProfileContext not loading profiles or AppInitializer not triggering selection

**Debug Steps:**
1. Check browser console for errors
2. Verify backend is running and responding
3. Check network tab for IPC messages
4. Add console.log in ProfileContext.loadProfiles()
5. Add console.log in AppInitializer.selectInitialProfile()

**Common Fixes:**
- Backend not running: Start with `dotnet run`
- ProfileContext loadProfiles() failing: Check error in console
- No profiles exist: Click "Create Default Profile" button

---

### Issue: Component Not Re-rendering on Profile Change

**Symptoms:**
- Profile switches but UI doesn't update
- Stale data shown after profile change

**Cause:** Not using `selectedProfileId` in dependency array

**Fix:**
```typescript
// ✅ Correct - includes selectedProfileId in deps
useEffect(() => {
  if (selectedProfileId) {
    loadData(selectedProfileId);
  }
}, [selectedProfileId]);  // Reacts to profile changes

// ❌ Wrong - missing dependency
useEffect(() => {
  if (selectedProfileId) {
    loadData(selectedProfileId);
  }
}, []);  // Only runs once!
```

---

### Issue: Type Error with selectedProfileId

**Symptoms:**
```
Type 'string | null' is not assignable to type 'string | undefined'
```

**Cause:** Using old type `string | null` instead of `string | undefined`

**Fix:**
```typescript
// ✅ Correct
const { selectedProfileId } = useProfile();  // string | undefined

// ❌ Wrong - don't override the type
const profileId: string | null = selectedProfileId;  // Type mismatch!
```

---

## Testing

### Manual Testing Checklist

- [ ] **Initialization**
  - [ ] App loads and shows loading screen
  - [ ] Global settings load successfully
  - [ ] Profiles load successfully
  - [ ] Initial profile selected automatically
  - [ ] UI renders after initialization

- [ ] **Profile Operations**
  - [ ] Profile selector shows all profiles
  - [ ] Switching profiles updates UI
  - [ ] Creating new profile works
  - [ ] Deleting profile works (except active)
  - [ ] Profile config updates save correctly

- [ ] **Data Loading**
  - [ ] Mods load for selected profile
  - [ ] Classification tree loads for selected profile
  - [ ] Settings load for selected profile
  - [ ] Switching profiles reloads data

- [ ] **Error Handling**
  - [ ] No console errors about missing profileId
  - [ ] Error messages display properly
  - [ ] Failed operations show user feedback

### Backend Logs to Check

**✅ Good logs:**
```
[IPC] Request: GET_ALL (Module: PROFILE, ProfileId: )
[IPC] Request: SWITCH (Module: PROFILE, ProfileId: )
[IPC] Request: GET_ALL (Module: MOD, ProfileId: default)            ← Has profileId!
[IPC] Request: GET_CLASSIFICATION_TREE (Module: MOD, ProfileId: default)  ← Has profileId!
[IPC] Response: Success
```

**❌ Bad logs:**
```
[IPC] Request: GET_ALL (Module: MOD, ProfileId: )                   ← Missing profileId!
[IPC] Error: Profile ID is required for module: MOD
```

If you see the bad pattern, check your service calls - you're not passing profileId correctly.

---

## Related Documentation

- [CURRENT_ARCHITECTURE.md](CURRENT_ARCHITECTURE.md) - Full system architecture
- [BACKEND_SERVICE_ARCHITECTURE.md](BACKEND_SERVICE_ARCHITECTURE.md) - Backend design
- [IPC_ARCHITECTURE.md](IPC_ARCHITECTURE.md) - IPC message format details
- [../AI_GUIDE.md](../AI_GUIDE.md) - AI assistant guidelines
- [../RECENT_CHANGES.md](../RECENT_CHANGES.md) - Recent changes log

---

## Changelog

### 2.0 (2026-02-19) - ✅ Implemented
- ✅ Removed `window.__selectedProfileId` global pattern
- ✅ Updated ProfileContext API (direct property access)
- ✅ Changed `selectedProfileId` type to `string | undefined`
- ✅ Fixed AppInitializer initialization flow
- ✅ Fixed classification service profileId passing (top-level, not payload)
- ✅ Updated ModsContext to use new ProfileContext API
- ✅ Updated SettingsView to use new ProfileContext API
- ✅ Updated ClassificationTreeOperations to use new ProfileContext API
- ✅ Added debug information to loading screen
- ✅ Added comprehensive error handling
- ✅ Build succeeds with no TypeScript errors

### 1.0 (2026-02-18) - Planning
- Initial documentation for context refactoring plan
- Designed module context pattern
- Identified refactoring priorities
