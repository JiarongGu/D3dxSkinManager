# AI Assistant Guidelines

**Last Updated:** 2026-02-23
**Purpose:** Essential patterns and anti-patterns for code generation

---

## State Management

### ✅ DO: Use Simple State

```typescript
const [mods, setMods] = useState<ModInfo[]>([]);
const modCount = mods.length;  // Derived, not stored
```

### ❌ DON'T: Duplicate State

```typescript
// BAD - Redundant state
const [mods, setMods] = useState<ModInfo[]>([]);
const [modCount, setModCount] = useState(0);  // Duplicates mods.length
```

---

## Error Handling

### ✅ DO: Type-Safe Error Handling

```typescript
try {
  await modService.loadMod(sha);
  message.success('Mod loaded');
} catch (error: unknown) {
  // Type guard for safety
  const errorMessage = error instanceof Error ? error.message : 'Unknown error';
  message.error(`Failed: ${errorMessage}`);
  console.error('Load mod error:', error);
}
```

### ❌ DON'T: Use any or assume Error type

```typescript
// BAD - All of these are wrong
catch (error: any) { }        // Never use any
catch (error) { }             // Implicit any
catch (error: Error) { }      // Unsafe assumption
```

---

## Async Operations

### ✅ DO: async/await Pattern

```typescript
const loadMods = async () => {
  setLoading(true);
  try {
    const data = await modService.getAllMods();
    setMods(data || []);  // Handle undefined
  } catch (error: unknown) {
    message.error('Failed to load mods');
  } finally {
    setLoading(false);
  }
};
```

### ❌ DON'T: Mix Patterns

```typescript
// BAD - Mixing promises with async/await
const loadMods = async () => {
  modService.getAllMods().then(data => {  // Don't mix!
    setMods(data);
  });
};
```

---

## Component Patterns

### ✅ DO: Functional Components

```typescript
export const ModList: FC<ModListProps> = ({ mods, onSelect }) => {
  const { t } = useTranslation();

  return (
    <div className="mod-list">
      {mods.map(mod => (
        <ModItem key={mod.sha} mod={mod} onSelect={onSelect} />
      ))}
    </div>
  );
};
```

### ❌ DON'T: Class Components

```typescript
// BAD - Never use class components
class ModList extends React.Component { }
```

---

## Hook Dependencies

### ✅ DO: Stable Callbacks

```typescript
// Use useStableRef for callbacks
const itemsRef = useStableRef(items);
const handleClick = useCallback(() => {
  console.log(itemsRef.current.length);  // Always current
}, []);  // No deps needed
```

### ❌ DON'T: Stale Closures

```typescript
// BAD - Stale closure
const handleClick = useCallback(() => {
  console.log(items.length);  // May be stale!
}, [items]);  // Recreates on every change
```

---

## IPC Communication

### ✅ DO: Type-Safe Messages

```typescript
interface LoadModRequest {
  sha: string;
  profileId: string;
}

const loadMod = async (request: LoadModRequest) => {
  return await modService.loadMod(request);
};
```

### ❌ DON'T: Untyped Payloads

```typescript
// BAD - No type safety
const loadMod = async (payload: any) => {
  return await sendMessage('LOAD_MOD', payload);
};
```

---

## File Paths

### ✅ DO: Use Path Service

```typescript
// Backend - Always use path service
const absolutePath = _pathService.GetAbsolutePath(relativePath);
```

### ❌ DON'T: Hardcode Paths

```typescript
// BAD - Hardcoded path
const path = "C:\\Games\\D3DX\\mods\\" + modName;
```

---

## CSS and Theming

### ✅ DO: CSS Variables

```typescript
<div style={{
  background: 'var(--color-bg-container)',
  color: 'var(--color-text-base)'
}} />
```

### ❌ DON'T: Hardcode Colors

```typescript
// BAD - Breaks themes
<div style={{ background: '#ffffff', color: '#000000' }} />
```

---

## Internationalization

### ✅ DO: Translation Keys

```typescript
const { t } = useTranslation();
<Button>{t('mods.actions.load')}</Button>
```

### ❌ DON'T: Hardcode Text

```typescript
// BAD - Not translatable
<Button>Load Mod</Button>
```

---

## React Patterns

### ✅ DO: Early Returns

```typescript
const ModDetails: FC<{ mod?: ModInfo }> = ({ mod }) => {
  if (!mod) return null;  // Early return

  return <div>{mod.name}</div>;
};
```

### ❌ DON'T: Nested Conditionals

```typescript
// BAD - Hard to read
return (
  <div>
    {mod ? (
      <div>{mod.name}</div>
    ) : (
      <div>No mod selected</div>
    )}
  </div>
);
```

---

## useEffect Best Practices

### ✅ DO: Cleanup Functions

```typescript
useEffect(() => {
  const subscription = service.subscribe(handler);
  return () => subscription.unsubscribe();  // Cleanup
}, []);
```

### ❌ DON'T: Memory Leaks

```typescript
// BAD - No cleanup
useEffect(() => {
  service.subscribe(handler);  // Leak!
}, []);
```

---

## Type Safety

### ✅ DO: Strict Types

```typescript
interface ModInfo {
  sha: string;
  name: string;
  enabled: boolean;
}

const [mods, setMods] = useState<ModInfo[]>([]);
```

### ❌ DON'T: any or loose types

```typescript
// BAD - All of these
const [mods, setMods] = useState<any[]>([]);
const [mods, setMods] = useState([]);  // Implicit any[]
```

---

## Null vs Undefined

### ✅ DO: undefined for missing data

```typescript
const [selectedMod, setSelectedMod] = useState<ModInfo>();  // undefined
if (!selectedMod) return null;  // React render requires null
```

### ❌ DON'T: null for data

```typescript
// BAD - Use undefined for data
const [selectedMod, setSelectedMod] = useState<ModInfo | null>(null);
```

---

## Service Calls

### ✅ DO: Handle undefined

```typescript
const mods = await modService.getAllMods();
setMods(mods || []);  // Handle undefined
```

### ❌ DON'T: Assume values

```typescript
// BAD - Assumes always returns array
const mods = await modService.getAllMods();
setMods(mods);  // Crashes if undefined
```

---

## Modal Patterns

### ✅ DO: Declarative Modals

```typescript
<Modal
  open={visible}
  transitionName=""  // No animation
  maskTransitionName=""
  centered
>
  {content}
</Modal>
```

### ❌ DON'T: Imperative API

```typescript
// BAD - Causes flashing
Modal.confirm({ title: 'Confirm' });
```

---

## Performance

### ✅ DO: Memoize Expensive Operations

```typescript
const sortedMods = useMemo(
  () => mods.sort((a, b) => a.name.localeCompare(b.name)),
  [mods]
);
```

### ❌ DON'T: Recalculate Every Render

```typescript
// BAD - Sorts on every render
const sortedMods = mods.sort((a, b) => a.name.localeCompare(b.name));
```

---

## Quick Reference

| Pattern | Use | Avoid |
|---------|-----|-------|
| State | Simple, derived values | Redundant state |
| Errors | `error: unknown` + guard | `any` or `Error` |
| Components | Functional + hooks | Class components |
| Callbacks | useStableRef | Stale closures |
| Data | `undefined` | `null` for data |
| Modals | Declarative | Imperative |
| Colors | CSS variables | Hardcoded |
| Text | i18n keys | Hardcoded strings |

---

**Remember:** These patterns prevent bugs. Follow them consistently.