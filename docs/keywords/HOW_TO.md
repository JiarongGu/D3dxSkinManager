# How-To Keywords Index

> **Purpose:** Task-based quick reference for common operations
> **Parent Index:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)

**Last Updated:** 2026-03-08

**Recent Additions:**
- IMemoryCache caching pattern → See "Adding Performance Caching"
- FileSystemWatcher pattern → See "Adding File Monitoring"

---

## Performance Optimization

### Adding IMemoryCache Caching

**"How do I add caching to expensive operations?"**
- **Full Pattern:** `docs/ai-assistant/WORKFLOWS.md` (Example 4) ⭐⭐⭐
- **Architecture:** `docs/AI_GUIDE.md` (Caching with IMemoryCache section)
- **Real Example:** ModQueryService.GetActiveModsAsync

**Quick Pattern:**
```csharp
public class YourService {
    private readonly IMemoryCache _cache;
    private readonly string _cacheKey;

    public YourService(IMemoryCache cache, IProfileContext profileContext, IProfileEventBus eventBus) {
        _cache = cache;
        _cacheKey = $"CacheName_{profileContext.ProfileId}";  // Profile-specific!

        // Subscribe to events for invalidation
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.SOME_CHANGED, _ => {
            _cache.Remove(_cacheKey);
            return Task.CompletedTask;
        });
    }

    public async Task<List<Data>> GetDataAsync() {
        return await _cache.GetOrCreateAsync(_cacheKey, async entry => {
            return await BuildExpensiveDataAsync();  // Slow path
        }) ?? new List<Data>();  // Fallback
    }
}
```

**When to Use:**
- Expensive file system scans
- Complex database queries
- Data accessed frequently but changes rarely
- Operations with FileSystemWatcher for auto-invalidation

### Adding File Monitoring (FileSystemWatcher)

**"How do I monitor directory changes and invalidate cache?"**
- **Full Pattern:** `docs/ai-assistant/WORKFLOWS.md` (Adding FileSystemWatcher) ⭐⭐⭐
- **Real Example:** ModCacheWatcher monitors cache/Mods folder

**Quick Pattern:**
```csharp
public class SomeWatcher : IDisposable {
    private readonly IProfileEventBus _eventBus;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();

    public void StartWatching() {
        lock (_lock) {
            _watcher = new FileSystemWatcher(path) {
                NotifyFilter = NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };
            _watcher.Deleted += OnDeleted;
        }
    }

    private void OnDeleted(object sender, FileSystemEventArgs e) {
        // Fire-and-forget to avoid blocking watcher thread
        _ = Task.Run(async () => {
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CACHE_CHANGED, new { });
        });
    }

    public void Dispose() {
        lock (_lock) {
            if (_watcher != null) {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
        }
    }
}
```

**Key Points:**
- Use `Task.Run` fire-and-forget pattern
- Be specific with NotifyFilter
- Emit events that other services subscribe to
- Always implement IDisposable

---

## Build and Deployment

### Building for Production

**"How do I build a production release?"**
- **Documentation:** `docs/how-to/BUILD_AND_DEPLOY.md` ⭐⭐⭐
- **Build Script:** `build-production.ps1` (automated build)
- **Output:** `dist/win-x64/D3dxSkinManager.exe` (single-file, ~14 MB)

**Quick Command:**
```powershell
.\build-production.ps1
```

**Manual Build:**
```bash
# 1. Build frontend
cd D3dxSkinManager.Client
npm install && npm run build

# 2. Build backend
cd ../D3dxSkinManager
dotnet publish -c Release -r win-x64 --no-self-contained
```

### Development Mode

**"How do I enable development mode features (DevTools, hot reload)?"**
- **DevTools/Context Menus:** Enabled only in development mode
- **Frontend:** Loads from `http://localhost:3000` (React dev server)
- **Detection:** `.dev` file or `ASPNETCORE_ENVIRONMENT=Development`

**Enable Development Mode:**
```bash
# Option 1: Create .dev file
touch .dev

# Option 2: Set environment variable
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

**Run Frontend Dev Server:**
```bash
cd D3dxSkinManager.Client
npm run dev  # Starts on http://localhost:3000
```

### Production vs Development

| Feature | Development | Production |
|---------|-------------|------------|
| **DevTools (F12)** | ✅ Enabled | ❌ Disabled |
| **Context Menus** | ✅ Enabled | ❌ Disabled |
| **Browser Shortcuts** | ✅ Enabled | ❌ Blocked |
| **Frontend Source** | `localhost:3000` | Embedded resources |
| **Console Window** | ✅ Visible (Exe) | ❌ Hidden (WinExe) |

---

## Internationalization (i18n)

### Adding i18n to Components

**"How do I add translations to a component?"**
- **Documentation:** `docs/how-to/ADD_I18N_TO_COMPONENT.md` ⭐⭐⭐
- **Feature Guide:** `docs/features/INTERNATIONALIZATION.md`
- **Translation Files:** `D3dxSkinManager/Languages/en.json`, `cn.json`
- **Current Status:** 16/35+ components completed (507 keys per language)

**Quick Steps:**
1. Import `useTranslation` hook
2. Add `const { t } = useTranslation();`
3. Replace hardcoded strings with `t('namespace.key')`
4. Add keys to both `en.json` and `cn.json`
5. Test language switching in Settings

**Example:**
```tsx
import { useTranslation } from 'react-i18next';

export const MyComponent = () => {
  const { t } = useTranslation();
  return <Button>{t('common.ok')}</Button>;
};
```

### Adding Translation Keys

**"How do I add new translation keys?"**
- **Translation Files:** `D3dxSkinManager/Languages/` (flat JSON structure)
- **Namespaces:** `common.*`, `mods.*`, `launch.*`, `plugins.*`, etc.
- **Guidelines:** `docs/features/INTERNATIONALIZATION.md#translation-guidelines`

**Steps:**
1. Add key to `en.json` in alphabetical order
2. Add matching key to `cn.json` with Chinese translation
3. Use in component: `t('namespace.key')`
4. Verify key parity with verification script

### Changing Language

**"How does language switching work?"**
- **User-facing:** Settings panel → Language selector
- **Programmatic:** `import { changeLanguage } from '../i18n/i18n'`
- **Backend:** Saved in `data/settings/global.json`
- **Persistence:** Loads automatically on app startup

---

## Backend Development

### Adding Services

**"How do I add a new backend service?"**
- **Documentation:** `docs/ai-assistant/WORKFLOWS.md#adding-services`
- **Example:** `Modules/Mods/Services/ModManagementService.cs`
- **DI Registration:** `Modules/Mods/ModsServiceExtensions.cs`
- **Pattern:** Repository, Domain Service, or Infrastructure Service
- **Reference:** `docs/core/ARCHITECTURE.md#service-layers`

**Steps:**
1. Create service class in `Modules/[Module]/Services/`
2. Create interface `I[ServiceName]`
3. Implement service methods
4. Register in `[Module]ServiceExtensions.cs`
5. Inject into facade or other services

---

### Dependency Injection

**"How do I use Dependency Injection?"**
- **Documentation:** `docs/core/ARCHITECTURE.md#dependency-injection`
- **Example:** `D3dxSkinManager/Program.cs:24-38`
- **DI Container:** Microsoft.Extensions.DependencyInjection
- **Module Extensions:** Each module has `[Module]ServiceExtensions.cs`

**Pattern:**
```csharp
// In ModsServiceExtensions.cs
public static IServiceCollection AddModsModule(this IServiceCollection services)
{
    services.AddSingleton<IModFacade, ModFacade>();
    services.AddSingleton<IModManagementService, ModManagementService>();
    return services;
}
```

---

### Facade Pattern

**"How do I use the Facade pattern?"**
- **Documentation:** `docs/core/ARCHITECTURE.md#facade-pattern`
- **Example:** `Modules/Mods/ModFacade.cs:14`
- **Base Class:** `Modules/Core/Facades/BaseFacade.cs`
- **Purpose:** IPC entry point for module operations

**Pattern:**
```csharp
public class ModFacade : BaseFacade, IModFacade
{
    private readonly IModManagementService _modManagement;

    public ModFacade(IModManagementService modManagement)
    {
        _modManagement = modManagement;
    }

    public async Task<ModInfo[]> GetAllModsAsync()
    {
        return await _modManagement.GetAllAsync();
    }
}
```

---

### Repository Pattern

**"How do I use the Repository pattern?"**
- **Documentation:** `docs/core/ARCHITECTURE.md#repository-pattern`
- **Example:** `Modules/Mods/Services/ModRepository.cs:32`
- **Purpose:** Data access abstraction

**Pattern:**
```csharp
public interface IModRepository
{
    Task<ModInfo[]> GetAllAsync();
    Task<ModInfo?> GetByIdAsync(string sha);
    Task InsertAsync(ModInfo mod);
}
```

---

### Database Changes

**"How do I update the database schema?"**
- **Documentation:** `docs/ai-assistant/WORKFLOWS.md#database-changes`
- **Schema Location:** `Modules/Mods/Services/ModRepository.cs:49-78`
- **Database:** SQLite
- **Connection:** `data/profiles/*/mods.db`

**Steps:**
1. Update schema in `InitializeDatabaseAsync()`
2. Update model class (`ModInfo.cs`)
3. Update repository methods (CRUD)
4. Test with existing data (migration if needed)

---

### IPC Messages

**"How do I add a new IPC message type?"**
- **Documentation:** `docs/ai-assistant/WORKFLOWS.md#ipc-messages`
- **Backend Handler:** Facade classes in `Modules/*/[Module]Facade.cs`
- **Frontend Types:** `src/shared/types/message.types.ts`
- **Frontend Service:** `src/shared/services/bridgeService.ts`

**Steps:**
1. Add message type to `MessageType` union (`message.types.ts`)
2. Add handler in facade's `HandleMessageAsync()`
3. Add frontend service method in `[module]Service.ts`
4. Call from component/hook

---

## Frontend Development

### Adding Components

**"How do I add a React component?"**
- **Documentation:** `docs/ai-assistant/WORKFLOWS.md#adding-components`
- **Location:** `src/components/` or `src/modules/[module]/components/`
- **Pattern:** Functional components with hooks

**Steps:**
1. Create `ComponentName.tsx` in appropriate folder
2. Use TypeScript for props interface
3. Import Compact components for consistency
4. Use custom hooks for logic
5. Export from `index.ts` if in module

---

### Custom Hooks

**"How do I create a custom hook?"**
- **Documentation:** `docs/core/ARCHITECTURE.md#custom-hooks-pattern`
- **Example:** `src/hooks/useModData.ts:8`
- **Location:** `src/hooks/` or `src/modules/[module]/hooks/`

**Pattern:**
```typescript
export const useModData = () => {
  const [mods, setMods] = useState<ModInfo[]>([]);
  const [loading, setLoading] = useState(false);

  const loadMods = async () => {
    setLoading(true);
    const data = await modService.getAllMods();
    setMods(data);
    setLoading(false);
  };

  return { mods, loading, loadMods };
};
```

---

### Delayed Loading (No Flicker Pattern)

**"How do I implement loading without flicker?"** ⭐ **NEW PATTERN**
- **Documentation:** `docs/features/DELAYED_LOADING_UX_PATTERN.md`
- **Hook:** `src/shared/hooks/useDelayedLoading.ts`
- **Examples:** `useClassificationData.ts`, `useModData.ts`, `ConfirmDialog.tsx`

**Use This Pattern Instead of Always-Show-Loading**

**Pattern:**
```typescript
import { useDelayedLoading } from '../../../shared/hooks/useDelayedLoading';

export const useMyData = () => {
  const [state, dispatch] = useReducer(myReducer, initialState);

  // 1. Create hook with 100ms threshold
  const { loading, execute } = useDelayedLoading(100);

  // 2. Sync loading state with reducer
  useEffect(() => {
    dispatch({ type: "SET_LOADING", payload: loading });
  }, [loading]);

  // 3. Wrap operations with execute()
  const loadData = useCallback(async (id: string) => {
    await execute(async () => {
      const data = await myService.getData(id);
      dispatch({ type: "SET_DATA", payload: data });
    });
  }, [execute]);

  return { state, loadData };
};
```

**Reducer (Important!):**
```typescript
// DON'T set loading: false in SET_DATA action
case "SET_DATA":
  return { ...state, data: action.payload }; // Let hook control loading
```

**When to use:**
- ✅ Data fetching (mods, classifications, profiles)
- ✅ UI interactions (dialogs, confirmations)
- ✅ Tree operations (drag-drop, reordering)

---

### Scroll Position Preservation

**"How do I preserve scroll position during reloads?"** ⭐ **NEW PATTERN (2026-03-07)**
- **Hook:** `src/shared/hooks/useScrollPosition.ts`
- **Use Case:** Prevent scroll position loss when content reloads
- **Examples:** ModListPanel, CategoryTree

**The Problem:**
When using a full-screen loader that replaces content, scroll position is lost after reload completes. This creates a poor UX where users lose their place in long lists.

**The Solution: Overlay Spinner + Scroll Position Persistence**

**Pattern:**
```typescript
import { useScrollPosition } from '@/shared/hooks/useScrollPosition';

export const MyPanel: React.FC = () => {
  // 1. Initialize scroll position hook with unique key
  const { scrollRef, saveScrollPosition, restoreScrollPosition, resetScrollPosition } =
    useScrollPosition('my-panel-scroll');

  // 2. Attach ref to scrollable container
  return (
    <div ref={scrollRef} className="scrollable-content">
      <MyList
        onBeforeReload={saveScrollPosition}
        onAfterReload={restoreScrollPosition}
      />
    </div>
  );
};

// In child component (MyList):
export const MyList: React.FC<Props> = ({ loading, onBeforeReload, onAfterReload }) => {
  // 3. Save/restore on loading state changes
  React.useEffect(() => {
    if (loading) {
      onBeforeReload?.();
    } else {
      onAfterReload?.();
    }
  }, [loading, onBeforeReload, onAfterReload]);

  // 4. Use overlay spinner instead of content replacement
  return (
    <div className="list-container">
      {loading && (
        <div className="loading-overlay">
          <Spin size="large" />
        </div>
      )}
      <div className={classNames('list-content', { 'loading': loading })}>
        {items.map(item => <Item key={item.id} {...item} />)}
      </div>
    </div>
  );
};
```

**CSS for Overlay Spinner:**
```css
.list-container {
  position: relative;
}

.loading-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  backdrop-filter: blur(1px);
  z-index: 10;
  pointer-events: all;
  cursor: auto;
}

.list-content {
  transition: opacity 0.2s;
}

.list-content.loading {
  opacity: 0.9;
  pointer-events: none;
}
```

**When to use:**
- ✅ Long scrollable lists (mod list, category tree)
- ✅ When reloading after operations (update, delete, refresh)
- ✅ When you want to maintain user's context

**When NOT to use:**
- ❌ Initial load (no scroll position to preserve)
- ❌ When changing filters/categories (use resetScrollPosition instead)
- ❌ Short lists where scroll position doesn't matter
- ❌ File uploads/downloads (always slow, use progress bar)
- ❌ Complex verification needed (use `useOptimisticUpdate` instead)

**Benefits:**
- Fast operations (<100ms): No spinner, feels instant ✨
- Slow operations (>100ms): Spinner appears, user gets feedback 🔄
- No flicker, smooth UX

---

### Context Providers

**"How do I create a Context Provider?"**
- **Documentation:** `docs/architecture/FRONTEND_CONTEXT_ARCHITECTURE.md`
- **Example:** `src/shared/context/ThemeContext.tsx`
- **Location:** `src/shared/context/`

**Pattern:**
```typescript
const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

export const ThemeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [theme, setTheme] = useState<ThemeMode>('auto');

  return (
    <ThemeContext.Provider value={{ theme, setTheme }}>
      {children}
    </ThemeContext.Provider>
  );
};

export const useTheme = () => {
  const context = useContext(ThemeContext);
  if (!context) throw new Error('useTheme must be used within ThemeProvider');
  return context;
};
```

---

### Compact Components

**"How do I use Compact components?"**
- **Location:** `src/shared/components/compact/`
- **Import:** `import { CompactButton, CompactCard } from 'shared/components/compact'`
- **Purpose:** Consistent sizing and styling

**Usage:**
```typescript
import { CompactButton, CompactCard } from 'shared/components/compact';

<CompactCard>
  <CompactButton type="primary" onClick={handleClick}>
    Save
  </CompactButton>
</CompactCard>
```

---

### Theme System

**"How do I use the theme system?"**
- **Documentation:** `docs/features/THEME_SYSTEM.md`
- **Hook:** `useTheme()` from `src/shared/context/ThemeContext.tsx`
- **CSS Variables:** `src/styles/theme-colors.css`

**Usage:**
```typescript
const { theme, effectiveTheme, setTheme } = useTheme();

<Button onClick={() => setTheme('dark')}>
  Dark Mode
</Button>
```

---

## Building & Running

### Build for Production

**"How do I build for production?"**
- **Documentation:** `docs/core/DEVELOPMENT.md#building`
- **Script:** `build-production.ps1`
- **Steps:**
  1. React build → `npm run build`
  2. Copy to wwwroot → PowerShell script
  3. .NET publish → `dotnet publish -c Release`

**Command:**
```powershell
.\build-production.ps1
```

---

### Run in Development

**"How do I run in development?"**
- **Documentation:** `docs/QUICKSTART.md`

**Backend:**
```bash
cd D3dxSkinManager
dotnet run
```

**Frontend (separate terminal):**
```bash
cd D3dxSkinManager.Client
npm start
```

---

### Run Tests

**"How do I run tests?"**
- **Documentation:** `docs/ai-assistant/TESTING_GUIDE.md`
- **Test Project:** `D3dxSkinManager.Tests/`

**Commands:**
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "ClassName=ModFacadeTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~GetAllModsAsync"
```

---

## Migration

### Understanding Migration

**"How does migration work?"**
- **Documentation:** `docs/architecture/MIGRATION_ARCHITECTURE.md`
- **Service:** `Modules/Migration/Services/MigrationService.cs`
- **Steps:** 6-step process
- **Pattern:** Step-based architecture with `IMigrationStep`

**6 Steps:**
1. Analyze Source
2. Migrate Configuration
3. Migrate Classifications
4. Migrate Classification Thumbnails
5. Migrate Mod Archives (NO extensions)
6. Migrate Mod Previews

---

### Archive Storage Format

**"Why are archives stored without extensions?"**
- **Reason:** Matches Python version format
- **Location:** `data/profiles/*/mods/{SHA}` (no extension)
- **Detection:** SharpCompress auto-detects format from file header
- **Update:** 2026-02-20
- **Files:** `ModFileService.cs`, `MigrationStep5MigrateModArchives.cs`

---

## Documentation

### Adding to CHANGELOG

**"How do I add to CHANGELOG?"**
- **Documentation:** `docs/maintenance/CHANGELOG_MANAGEMENT.md`
- **Rule:** Main CHANGELOG.md < 200 lines
- **Format:** Summary entries (3-5 lines)
- **Detailed:** Create file in `changelogs/YYYY-MM/`

**Check line count:**
```bash
wc -l docs/CHANGELOG.md
```

---

### Updating KEYWORDS_INDEX

**"How do I update KEYWORDS_INDEX?"**
- **Documentation:** `docs/maintenance/KEYWORDS_INDEX_MANAGEMENT.md`
- **Rule:** KEYWORDS_INDEX.md < 500 lines
- **Format:** One-line entries: `Name → path (purpose)`
- **Detailed:** Add to domain-specific file in `keywords/`

**Check line count:**
```bash
wc -l docs/KEYWORDS_INDEX.md
```

---

### Archiving Old Docs

**"When should I archive documentation?"**
- **Location:** `docs/archive/YYYY-MM-DD-topic/`
- **Criteria:**
  - Outdated session documents
  - Superseded by newer docs
  - Historical reference only
- **Keep:** Migration history, refactoring summaries

---

## Git Workflow

### Creating Feature Branch

**"How do I create a feature branch?"**
- **Documentation:** `docs/ai-assistant/WORKFLOWS.md#git-workflow`

**Commands:**
```bash
git checkout -b feature/feature-name
# Make changes
git add .
git commit -m "Description"
git push -u origin feature/feature-name
```

---

### Creating Commits

**"How do I create good commits?"**
- **Documentation:** AI_GUIDE.md Git Safety Protocol
- **Format:**
  ```
  Brief summary (50 chars max)

  Detailed description of changes

  🤖 Generated with Claude Code
  Co-Authored-By: Claude <noreply@anthropic.com>
  ```

**Steps:**
1. Check status and diff
2. Stage relevant files
3. Write clear commit message
4. Verify with `git log`

---

### Creating Pull Requests

**"How do I create a pull request?"**
- **Documentation:** AI_GUIDE.md Creating Pull Requests
- **Tool:** `gh` CLI

**Commands:**
```bash
# Push branch
git push -u origin feature/feature-name

# Create PR
gh pr create --title "PR Title" --body "Summary"
```

---

## Common Issues

### "File has not been read yet" Error

**"Why am I getting 'File has not been read yet' errors?"**
- **Cause:** Edit tool requires reading file first
- **Solution:** Use Read tool before Edit tool
- **Reference:** `docs/ai-assistant/TROUBLESHOOTING.md`

---

### Build Errors

**"How do I fix build errors?"**
- **Documentation:** `docs/ai-assistant/TROUBLESHOOTING.md`
- **Common Causes:**
  - Missing dependencies: `dotnet restore`, `npm install`
  - Version mismatches: Check .csproj, package.json
  - Cache issues: Clean and rebuild

---

### Test Failures

**"How do I debug test failures?"**
- **Documentation:** `docs/ai-assistant/TESTING_GUIDE.md`
- **Steps:**
  1. Read test output carefully
  2. Check test expectations vs actual
  3. Use `--logger "console;verbosity=detailed"`
  4. Add debug logging to code under test

---

## Project Structure

### "Where is..."

| What | Location |
|------|----------|
| **Main entry point (backend)?** | `D3dxSkinManager/Program.cs:11` |
| **Main UI (frontend)?** | `D3dxSkinManager.Client/src/App.tsx` |
| **IPC handler?** | `Infrastructure/IpcCommunicationHandler.cs` (backend), `bridgeService.ts` (frontend) |
| **Database schema?** | `Modules/Mods/Services/ModRepository.cs:49` |
| **Data directory?** | `data/` (created at runtime) |
| **Config files?** | `data/settings/`, `data/profiles/*/` |
| **Mod archives?** | `data/profiles/*/mods/{SHA}` (no extension) |
| **Extracted mods?** | `data/profiles/*/work/Mods/{SHA}/` |
| **Tests?** | `D3dxSkinManager.Tests/` |
| **Plugins?** | `Plugins/` (external), `Modules/Plugins/` (infrastructure) |

---

**Line Count:** ~370 lines
**Parent:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)
