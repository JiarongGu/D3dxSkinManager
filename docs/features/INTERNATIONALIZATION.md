# Internationalization (i18n) System

**Status:** ✅ Production Ready
**Version:** 1.0
**Last Updated:** 2026-02-21
**Languages:** English (en), Chinese Simplified (cn)

---

## Overview

D3dxSkinManager uses **react-i18next** with a custom backend that loads translations from the C# backend via IPC. This architecture allows for easy language file management in a desktop application.

## Architecture

```
┌─────────────────────┐
│  React Frontend     │
│  react-i18next      │
│  useTranslation()   │
└──────────┬──────────┘
           │ IPC (Photino)
           ▼
┌─────────────────────┐      ┌────────────────────────┐
│  C# Backend         │◄────►│  Language Files        │
│  LanguageService    │      │  Languages/en.json     │
│  GetLanguageAsync() │      │  Languages/cn.json     │
└─────────────────────┘      └────────────────────────┘
           │                            │
           │                            │ Build: Auto-copy
           ▼                            ▼
┌─────────────────────┐      ┌────────────────────────┐
│  Global Settings    │      │  Output Directory      │
│  global.json        │      │  data/languages/       │
│  { language: "en" } │      │  en.json, cn.json      │
└─────────────────────┘      └────────────────────────┘
```

## Quick Start

### Using Translations in Components

```tsx
import { useTranslation } from 'react-i18next';

export const MyComponent: React.FC = () => {
  const { t } = useTranslation();

  return (
    <div>
      <h1>{t('common.title')}</h1>
      <p>{t('mods.notifications.loadSuccess', { name: modName })}</p>
      <Button>{t('common.ok')}</Button>
    </div>
  );
};
```

### Changing Language

```tsx
import { changeLanguage } from '../i18n/i18n';

// Change language and persist to backend
await changeLanguage('cn');
```

## Translation File Structure

### Flat JSON Format (Recommended)

Translation files use **flat keys with dot notation** for easy searching:

```json
{
  "code": "en",
  "name": "English",
  "translations": {
    "common.ok": "OK",
    "common.cancel": "Cancel",
    "mods.list.loaded": "LOADED",
    "mods.notifications.loadSuccess": "Mod loaded successfully",
    "launch.game.launchButton": "Launch Game",
    "plugins.status.active": "Active"
  }
}
```

**Benefits:**
- ✅ Easy to search: `grep "mods.list.loaded"`
- ✅ Clear hierarchy: namespace.category.key
- ✅ Simple maintenance: No nested navigation
- ✅ Quick lookups: Find any key instantly

### Translation Namespaces (507 Keys Total)

| Namespace | Keys | Purpose |
|-----------|------|---------|
| `common.*` | ~50 | Common UI elements (OK, Cancel, Save, etc.) |
| `header.*` | ~10 | Header navigation and profile selector |
| `statusBar.*` | ~6 | Status bar indicators |
| `mods.*` | ~150 | Mod management system |
| `├─ mods.list.*` | ~20 | Mod list view |
| `├─ mods.panel.*` | ~5 | Mod panel states |
| `├─ mods.preview.*` | ~35 | Preview panel operations |
| `├─ mods.edit.*` | ~20 | Edit dialog forms |
| `└─ mods.notifications.*` | ~20 | Mod-related notifications |
| `launch.*` | ~80 | Launch module |
| `├─ launch.game.*` | ~40 | Game launch configuration |
| `├─ launch.d3dmigoto.*` | ~35 | 3DMigoto configuration |
| `└─ launch.tabs.*` | ~2 | Tab labels |
| `tools.*` | ~15 | Tools module |
| `plugins.*` | ~20 | Plugins module |
| `dialogs.*` | ~50 | Dialog boxes |
| `shortcuts.*` | ~12 | Keyboard shortcuts |
| `about.*` | ~30 | About dialog |
| `settings.*` | ~50 | Settings panel |
| `contextMenu.*` | ~10 | Context menus |
| `grading.*` | ~6 | Rating system (S/A/B/C/D) |
| `errors.*` | ~12 | Error messages |
| `validation.*` | ~6 | Form validation |

## File Locations

### Backend Files

```
D3dxSkinManager/
├── Languages/                    # Source language files
│   ├── en.json                   # English (507 keys)
│   └── cn.json                   # Chinese (507 keys)
├── Modules/Settings/
│   ├── Models/
│   │   ├── LanguageSettings.cs   # Language model
│   │   └── GlobalSettings.cs     # Added: Language field
│   ├── Services/
│   │   ├── LanguageService.cs    # Language operations
│   │   └── GlobalSettingsService.cs  # Language persistence
│   ├── SettingsFacade.cs         # IPC handlers
│   └── SettingsServiceExtensions.cs  # DI registration
└── D3dxSkinManager.csproj        # Auto-copy configuration
```

### Frontend Files

```
D3dxSkinManager.Client/src/
├── i18n/
│   ├── i18n.ts                   # i18next config with custom backend
│   └── I18nInitializer.tsx       # Initialization component
├── shared/
│   ├── types/language.types.ts   # Language interfaces
│   └── services/languageService.ts  # Frontend API
└── modules/
    └── settings/components/
        └── SettingsView.tsx      # Language selector UI
```

## Backend Implementation

### Language Service

**File:** `D3dxSkinManager/Modules/Settings/Services/LanguageService.cs`

```csharp
public interface ILanguageService
{
    Task<LanguageSettings?> GetLanguageAsync(string languageCode);
    Task<List<string>> GetAvailableLanguagesAsync();
    Task<bool> LanguageExistsAsync(string languageCode);
}

public class LanguageService : ILanguageService
{
    private readonly string _languagesDirectory;

    public async Task<LanguageSettings?> GetLanguageAsync(string languageCode)
    {
        var filePath = Path.Combine(_languagesDirectory, $"{languageCode}.json");
        if (!File.Exists(filePath)) return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<LanguageSettings>(json);
    }
}
```

### IPC Handlers

**File:** `D3dxSkinManager/Modules/Settings/SettingsFacade.cs`

```csharp
private async Task<object> GetLanguageHandlerAsync(IpcRequest request)
{
    var languageCode = request.Payload?.GetProperty("languageCode").GetString();
    var language = await _languageService.GetLanguageAsync(languageCode);
    return new { success = true, language };
}
```

### Auto-Copy Configuration

**File:** `D3dxSkinManager/D3dxSkinManager.csproj`

```xml
<!-- Copy language files to output directory -->
<ItemGroup>
  <None Include="Languages\**\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>data\languages\%(RecursiveDir)%(Filename)%(Extension)</Link>
  </None>
</ItemGroup>
```

## Frontend Implementation

### Custom i18next Backend

**File:** `D3dxSkinManager.Client/src/i18n/i18n.ts`

```typescript
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { languageService } from '../shared/services/languageService';

const customBackend = {
  type: 'backend' as const,
  read: async (language: string, namespace: string, callback) => {
    try {
      const languageSettings = await languageService.getLanguage(language);
      if (languageSettings && languageSettings.translations) {
        callback(null, languageSettings.translations);
      } else {
        callback(new Error('Language not found'), null);
      }
    } catch (error) {
      callback(error, null);
    }
  },
};

i18n
  .use(customBackend)
  .use(initReactI18next)
  .init({
    lng: DEFAULT_LANGUAGE,
    fallbackLng: DEFAULT_LANGUAGE,
    interpolation: { escapeValue: false },
  });
```

### Language Service API

**File:** `D3dxSkinManager.Client/src/shared/services/languageService.ts`

```typescript
import { photinoService } from './photinoService';

export const languageService = {
  async getLanguage(languageCode: string): Promise<LanguageSettings | null> {
    const response = await photinoService.sendMessage({
      module: 'SETTINGS',
      type: 'GET_LANGUAGE',
      payload: { languageCode },
    });
    return response.language || null;
  },

  async getAvailableLanguages(): Promise<string[]> {
    const response = await photinoService.sendMessage({
      module: 'SETTINGS',
      type: 'GET_AVAILABLE_LANGUAGES',
    });
    return response.languages || [];
  },
};
```

### Initialization

**File:** `D3dxSkinManager.Client/src/i18n/I18nInitializer.tsx`

```typescript
export const I18nInitializer: React.FC<I18nInitializerProps> = ({ children }) => {
  const [isInitialized, setIsInitialized] = useState(false);

  useEffect(() => {
    const initialize = async () => {
      await loadLanguageFromSettings(); // Load saved language
      setIsInitialized(true);
    };
    initialize();
  }, []);

  if (!isInitialized) {
    return <div>Loading...</div>;
  }

  return <I18nextProvider i18n={i18n}>{children}</I18nextProvider>;
};
```

## Adding New Translation Keys

### 1. Add to Translation Files

Add the key to **both** `en.json` and `cn.json` in alphabetical order:

**en.json:**
```json
{
  "translations": {
    "myModule.myAction": "My Action",
    "myModule.myMessage": "Hello {{name}}!"
  }
}
```

**cn.json:**
```json
{
  "translations": {
    "myModule.myAction": "我的操作",
    "myModule.myMessage": "你好 {{name}}！"
  }
}
```

### 2. Use in Component

```tsx
import { useTranslation } from 'react-i18next';

export const MyComponent = () => {
  const { t } = useTranslation();

  return (
    <div>
      <button>{t('myModule.myAction')}</button>
      <p>{t('myModule.myMessage', { name: 'User' })}</p>
    </div>
  );
};
```

### 3. Verify Key Parity

```bash
cd D3dxSkinManager/Languages
node -e "
const en = require('./en.json');
const cn = require('./cn.json');
console.log('EN keys:', Object.keys(en.translations).length);
console.log('CN keys:', Object.keys(cn.translations).length);
console.log('Match:', Object.keys(en.translations).length === Object.keys(cn.translations).length ? '✓' : '✗');
"
```

## Component Update Pattern

### Standard Pattern

```tsx
// 1. Import dependencies
import { useTranslation } from 'react-i18next';
import './ComponentName.css';

// 2. Add hook
export const ComponentName: React.FC = () => {
  const { t } = useTranslation();

  // 3. Replace all hardcoded strings
  return (
    <div className="component-container">
      <h1>{t('namespace.title')}</h1>
      <button onClick={handleClick}>
        {t('namespace.action')}
      </button>
      <p>{t('namespace.message', { count: items.length })}</p>
    </div>
  );
};
```

### Notification Messages

```tsx
import { notification } from '../shared/utils/notification';

// Before
notification.success('Operation completed successfully');
notification.error('Operation failed');

// After
notification.success(t('namespace.successMessage'));
notification.error(t('namespace.errorMessage'));
```

### Dynamic Messages with Parameters

```tsx
// Multiple parameters
notification.success(
  t('mods.notifications.exportSuccess', {
    name: mod.name,
    size: mod.size
  })
);

// Pluralization
<span>{t('statusBar.modsLoaded', { count: 5, total: 10 })}</span>
```

## Components Updated (16/35+)

### ✅ Completed (16 components)

1. AppHeader.tsx
2. AppStatusBar.tsx
3. SettingsView.tsx
4. ModList.tsx
5. ModListPanel.tsx
6. ModPreviewPanel.tsx
7. BasicInfoSection.tsx
8. MetadataSection.tsx
9. TagsSection.tsx
10. LaunchView.tsx
11. GameLaunchTab.tsx
12. D3DMigotoTab.tsx
13. ToolsView.tsx
14. PluginsView.tsx
15. KeyboardShortcutsDialog.tsx
16. AboutDialog.tsx

### ⏳ High Priority (Remaining - 6 components)

1. **AddModWindow** (~60 strings) - Import/add mod workflow
2. **BatchEditDialog** (~50 strings) - Batch operations on mods
3. **ClassificationScreen** (~30 strings) - Classification management
4. **ProfileManager** (~70 strings) - Profile CRUD operations
5. **ProfileSwitcher** (~15 strings) - Profile selection
6. **HelpWindow** (~200 strings) - Extensive help documentation

### 📋 Medium Priority (7 components + 4 steps)

7. **MigrationWizard** + 4 step components (~100 strings)
8. **CacheManagementTool** (~50 strings)
9. **UnityArgsDialog** (~30 strings)
10. **TagManagementTool** (~15 strings)
11. **StartupValidationTool** (~20 strings)
12. **TagSelectDialog** (~30 strings)
13. **ImportTagSelectorDialog** (~15 strings)

### 📊 Low Priority (6+ components)

14. AppSider, ModActionButtons, OperationMonitorScreen
15. PythonMigrationTool, UtilitiesTool
16. Shared components (GradingTag, ConfirmDialog, etc.)

## CSS Class Migration

When updating components, also convert inline styles to CSS classes:

**Before:**
```tsx
<div style={{ padding: '16px', background: '#fff' }}>
  Content
</div>
```

**After:**
```tsx
// Component.css
.component-container {
  padding: 16px;
  background: #fff;
}

// Component.tsx
<div className="component-container">
  Content
</div>
```

## Translation Guidelines

### English (en)

- Use clear, concise language
- Maintain professional tone
- Use sentence case for UI labels
- Use Title Case for dialog titles

### Chinese (cn)

- Use Simplified Chinese (简体中文)
- Maintain consistent terminology:
  - "Mod" → "模组"
  - "Plugin" → "插件"
  - "Profile" → "配置"
  - "Load" → "加载"
  - "Unload" → "卸载"
  - "Deploy" → "部署"
- Keep brand names in English (3DMigoto, Unity, etc.)
- Keep technical abbreviations (SHA, ZIP, JSON)
- Use natural Chinese sentence structure

### Parameter Interpolation

Always preserve parameter placeholders:

```json
{
  "message": "Loaded {{count}} mods",
  "greeting": "Hello, {{name}}!",
  "status": "{{current}} of {{total}} completed"
}
```

## Testing

### Manual Testing

1. **Switch languages** in Settings panel
2. **Navigate all screens** to verify translations
3. **Trigger notifications** to check dynamic messages
4. **Test parameter interpolation** with different values

### Automated Key Verification

```bash
# Check key count
node -e "console.log(Object.keys(require('./Languages/en.json').translations).length)"

# Find missing keys in cn.json
node scripts/check-translations.js
```

### Build Verification

```bash
# Frontend build
cd D3dxSkinManager.Client
npm run build

# Backend build (copies language files)
cd D3dxSkinManager
dotnet build
```

## Common Issues

### Issue: Translation Not Showing

**Cause:** Key doesn't exist in translation file
**Solution:** Add key to both `en.json` and `cn.json`

### Issue: Parameters Not Interpolating

**Cause:** Wrong parameter syntax
**Solution:** Use `{{paramName}}` syntax in translation string

### Issue: Language Not Persisting

**Cause:** Setting not saved to backend
**Solution:** Ensure `changeLanguage()` calls both i18n and backend service

### Issue: Translation Key Mismatch

**Cause:** Different keys in en.json and cn.json
**Solution:** Run key parity check script (see verification above)

## Performance

- **Initial Load:** ~50ms (loads selected language)
- **Language Switch:** ~100ms (loads new language + saves setting)
- **Translation Lookup:** ~0.1ms (in-memory hash map)
- **Bundle Size Impact:** +7.95 KB CSS (gzipped)

## Future Enhancements

- [ ] Add more languages (Japanese, Korean, etc.)
- [ ] Implement lazy loading for large translation files
- [ ] Add translation management UI for non-technical users
- [ ] Support RTL languages (Arabic, Hebrew)
- [ ] Add translation validation in CI/CD pipeline

## References

- [react-i18next Documentation](https://react.i18next.com/)
- [i18next Documentation](https://www.i18next.com/)
- Translation Files: `D3dxSkinManager/Languages/`
- Backend Service: `D3dxSkinManager/Modules/Settings/Services/LanguageService.cs`
- Frontend Config: `D3dxSkinManager.Client/src/i18n/i18n.ts`

---

**For more information:**
- See [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md) for file locations
- See [DEVELOPMENT.md](../core/DEVELOPMENT.md) for build setup
- See [ARCHITECTURE.md](../architecture/CURRENT_ARCHITECTURE.md) for system design
