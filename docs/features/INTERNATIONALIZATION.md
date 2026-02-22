# Internationalization (i18n) System

**Created:** 2026-02-21
**Status:** ✅ Fully Implemented
**Languages:** English (en), Chinese (cn)
**Coverage:** 507 translation keys per language

---

## Overview

D3dxSkinManager features a complete bilingual (English + Chinese) internationalization system using `react-i18next`. All user-facing text in the application has been internationalized.

### Key Features
- **Bilingual Support:** English and Chinese (Simplified)
- **Dynamic Language Switching:** Change language without restart
- **Backend-Stored Translations:** Translation files stored on backend for easy updates
- **Flat JSON Structure:** Simple, searchable translation keys
- **Complete Coverage:** 507+ translation keys covering entire UI

---

## Architecture

### Technology Stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| **Frontend Library** | react-i18next | React integration for i18next |
| **Core Library** | i18next | Translation engine |
| **Backend Storage** | .NET Settings Service | Stores language files |
| **IPC** | WebView2 bridge | Loads translations from backend |

### Data Flow

```
[Frontend Component]
    ↓ useTranslation()
[i18next Instance]
    ↓ Custom Backend
[Language Service]
    ↓ bridgeService
[Backend Settings]
    ↓ JSON files
[Translation Data]
```

---

## Implementation Details

### File Structure

```
D3dxSkinManager/
├── Data/
│   └── Languages/           # Backend language files
│       ├── en.json         # English translations
│       └── cn.json         # Chinese translations
│
└── D3dxSkinManager.Client/
    └── src/
        └── i18n/
            ├── i18n.ts     # i18next configuration
            └── I18nInitializer.tsx  # React initialization
```

### Translation Structure

**Flat JSON structure** (easier to search and maintain):

```json
{
  "common.save": "Save",
  "common.cancel": "Cancel",
  "mods.title": "Mods",
  "mods.actions.load": "Load",
  "mods.actions.unload": "Unload",
  "profiles.manage": "Manage Profiles",
  // ... 500+ more keys
}
```

### Key Categories

| Category | Prefix | Example Keys |
|----------|--------|--------------|
| Common UI | `common.*` | save, cancel, delete, confirm |
| Mods | `mods.*` | title, actions, status, filters |
| Profiles | `profiles.*` | manage, create, switch |
| Settings | `settings.*` | general, advanced, appearance |
| Migration | `migration.*` | wizard, steps, progress |
| Operations | `operations.*` | loading, success, error |
| Warehouse | `warehouse.*` | browse, download, categories |
| Tools | `tools.*` | cache, validation, cleanup |
| Launch | `launch.*` | game, custom, arguments |

---

## Usage Guide

### Basic Component Usage

```tsx
import { useTranslation } from 'react-i18next';

export const MyComponent: React.FC = () => {
  const { t } = useTranslation();

  return (
    <div>
      <h1>{t('mods.title')}</h1>
      <Button>{t('common.save')}</Button>
    </div>
  );
};
```

### With Interpolation

```tsx
// Translation: "Loaded {{count}} mods"
<span>{t('mods.status.loaded', { count: 5 })}</span>
// Output: "Loaded 5 mods"
```

### With Pluralization

```tsx
// en.json:
// "mods.count_one": "{{count}} mod",
// "mods.count_other": "{{count}} mods"

<span>{t('mods.count', { count: modCount })}</span>
```

### Changing Language

```tsx
import { changeLanguage } from '../i18n/i18n';

const handleLanguageChange = async (lang: string) => {
  await changeLanguage(lang); // Changes and saves to backend
};
```

---

## Backend Implementation

### Settings Facade Handler

```csharp
// D3dxSkinManager/Modules/Settings/SettingsFacade.cs

case "GET_LANGUAGE":
    var languageCode = payload?.GetProperty("languageCode").GetString() ?? "en";
    var languageFile = Path.Combine(_dataPath, "Languages", $"{languageCode}.json");

    if (File.Exists(languageFile))
    {
        var content = await File.ReadAllTextAsync(languageFile);
        var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(content);

        return new {
            success = true,
            language = new {
                code = languageCode,
                name = languageCode == "en" ? "English" : "中文",
                translations
            }
        };
    }
    break;
```

### Language Files Location

```
D3dxSkinManager/Data/Languages/
├── en.json  (507 keys)
└── cn.json  (507 keys)
```

---

## Custom i18n Backend

The system uses a custom backend to load translations from the .NET backend:

```typescript
// D3dxSkinManager.Client/src/i18n/i18n.ts

const customBackend = {
  type: 'backend',
  read: async (language: string, namespace: string, callback) => {
    try {
      const languageSettings = await languageService.getLanguage(language);

      if (languageSettings?.translations) {
        callback(null, languageSettings.translations);
      } else {
        callback(new Error(`Language ${language} not found`));
      }
    } catch (error) {
      callback(error as Error);
    }
  },
};
```

### Language Service API

**File:** `D3dxSkinManager.Client/src/shared/services/languageService.ts`

```typescript
import { bridgeService } from './bridgeService';

export const languageService = {
  async getLanguage(languageCode: string): Promise<LanguageSettings | null> {
    const response = await bridgeService.sendMessage({
      module: 'SETTINGS',
      type: 'GET_LANGUAGE',
      payload: { languageCode },
    });
    return response.language || null;
  },

  async getAvailableLanguages(): Promise<string[]> {
    const response = await bridgeService.sendMessage({
      module: 'SETTINGS',
      type: 'GET_AVAILABLE_LANGUAGES',
    });
    return response.languages || [];
  },
};
```

### Initialization

```tsx
// D3dxSkinManager.Client/src/i18n/I18nInitializer.tsx

export const I18nInitializer: React.FC = ({ children }) => {
  const [isReady, setIsReady] = useState(false);

  useEffect(() => {
    const initLanguage = async () => {
      await loadLanguageFromSettings(); // Load saved language
      setIsReady(true);
    };

    initLanguage();
  }, []);

  if (!isReady) {
    return <LoadingScreen />;
  }

  return <>{children}</>;
};
```

---

## Adding New Translations

### Step 1: Add Keys to Both Language Files

**en.json:**
```json
{
  "myfeature.title": "My New Feature",
  "myfeature.description": "This is a new feature"
}
```

**cn.json:**
```json
{
  "myfeature.title": "我的新功能",
  "myfeature.description": "这是一个新功能"
}
```

### Step 2: Use in Component

```tsx
const { t } = useTranslation();

return (
  <div>
    <h2>{t('myfeature.title')}</h2>
    <p>{t('myfeature.description')}</p>
  </div>
);
```

---

## Component Coverage Status

### ✅ Fully Internationalized (16 components)

- App.tsx
- AppHeader.tsx
- ModList.tsx
- ModPreviewPanel.tsx
- ProfileManager.tsx
- ProfileSelector.tsx
- SettingsView.tsx
- MigrationWizard.tsx
- ClassificationPanel.tsx
- WarehouseView.tsx
- ToolsView.tsx
- LaunchTab.tsx
- PluginsView.tsx
- AppStatusBar.tsx
- OperationMonitor.tsx
- ErrorBoundary.tsx

### ⚠️ Partial/Pending (19+ components)

See [how-to/ADD_I18N_TO_COMPONENT.md](../how-to/ADD_I18N_TO_COMPONENT.md) for migration guide.

---

## Language Settings Integration

Language preference is saved to global settings:

```typescript
// Changing language
await changeLanguage('cn'); // Saves to backend

// Settings stored in:
// D3dxSkinManager/Data/Settings/global.json
{
  "language": "cn",
  "theme": "dark",
  // ...
}
```

---

## Testing

### Manual Testing
1. Switch language in settings
2. Verify all UI elements update
3. Check for missing translations (shows key if missing)
4. Test interpolation and pluralization

### Automated Testing
```typescript
// Test translation loading
describe('i18n', () => {
  it('should load English translations', async () => {
    await i18n.changeLanguage('en');
    expect(i18n.t('common.save')).toBe('Save');
  });

  it('should load Chinese translations', async () => {
    await i18n.changeLanguage('cn');
    expect(i18n.t('common.save')).toBe('保存');
  });
});
```

---

## Performance Considerations

- **Lazy Loading:** Translations loaded on-demand from backend
- **Caching:** i18next caches loaded translations in memory
- **Bundle Size:** No translation files in frontend bundle (loaded from backend)
- **Fast Switching:** Language change without page reload

---

## Future Enhancements

1. **More Languages:** Japanese (ja), Korean (ko), Spanish (es)
2. **RTL Support:** Arabic, Hebrew support
3. **Translation Management:** Admin UI for editing translations
4. **Namespace Splitting:** Split large translation file into namespaces
5. **Fallback Chain:** Multi-level fallback (cn → en → key)

---

## Related Documentation

- [How to Add i18n to a Component](../how-to/ADD_I18N_TO_COMPONENT.md)
- [Language Service API](../api/LANGUAGE_SERVICE.md)
- [Settings Module](../architecture/MODULE_STRUCTURE.md#settings-module)

---

**Last Updated:** 2026-02-22