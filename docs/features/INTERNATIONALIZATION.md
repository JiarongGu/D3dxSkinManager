# Internationalization (i18n)

**Last Updated:** 2026-02-23
**Status:** Implemented
**Languages:** English (en), Chinese (cn)
**Coverage:** 507+ translation keys

## Architecture

```
Frontend Component
    ↓ useTranslation()
i18next Instance
    ↓ Custom Backend
Language Service → Backend Settings
    ↓
Translation JSON Files
```

### Storage
- Backend: `Data/Languages/{en,cn}.json`
- Frontend: `src/i18n/i18n.ts` (config)

## Translation Structure

**Flat JSON format:**
```json
{
  "common.save": "Save",
  "mods.title": "Mods",
  "profiles.manage": "Manage Profiles"
}
```

### Key Prefixes
- `common.*` - UI elements (save, cancel, delete)
- `mods.*` - Mod operations
- `profiles.*` - Profile management
- `settings.*` - Application settings
- `migration.*` - Migration wizard
- `operations.*` - Progress/status
- `warehouse.*` - Mod discovery
- `tools.*` - Utilities
- `launch.*` - Game launching

## Usage Patterns

### Component Translation
```typescript
import { useTranslation } from 'react-i18next';

const MyComponent = () => {
  const { t } = useTranslation();
  return <Button>{t('common.save')}</Button>;
};
```

### Nested Keys
```typescript
// With interpolation
t('mods.status.loaded', { count: 5 })  // "5 mods loaded"

// Pluralization
t('items', { count: 1 })  // "1 item"
t('items', { count: 5 })  // "5 items"
```

### Language Switching
```typescript
const { i18n } = useTranslation();

// Change language
await i18n.changeLanguage('cn');

// Get current
const current = i18n.language;  // 'en' or 'cn'
```

## Adding Translations

### 1. Add to JSON files
```json
// Data/Languages/en.json
"feature.newKey": "New Feature"

// Data/Languages/cn.json
"feature.newKey": "新功能"
```

### 2. Use in component
```typescript
const label = t('feature.newKey');
```

### 3. TypeScript types (auto-generated)
```typescript
// src/i18n/types.ts
interface TranslationKeys {
  'feature.newKey': string;
}
```

## Best Practices

1. **Always translate** - No hardcoded strings
2. **Use flat keys** - Easier to search/maintain
3. **Descriptive keys** - `mods.actions.load` not `load`
4. **Keep bilingual** - Add both languages together
5. **Test both languages** - Check UI overflow in Chinese

## Backend Integration

### Loading Translations
```csharp
// SettingsFacade.cs
case "GET_LANGUAGE_FILE":
    var lang = request.GetPayload<string>();
    var json = File.ReadAllText($"Data/Languages/{lang}.json");
    return MessageResponse.CreateSuccess(request.Id, json);
```

### Storing Preference
```typescript
// Settings saved to backend
await settingsService.updateGlobalSettings({
  language: 'cn'
});
```

## Common Translations

| Key | English | Chinese |
|-----|---------|---------|
| common.save | Save | 保存 |
| common.cancel | Cancel | 取消 |
| common.delete | Delete | 删除 |
| common.confirm | Confirm | 确认 |
| mods.title | Mods | 模组 |
| profiles.title | Profiles | 配置文件 |
| settings.title | Settings | 设置 |

## Related Documentation

- [ADD_I18N_TO_COMPONENT.md](../how-to/ADD_I18N_TO_COMPONENT.md) - Step-by-step guide
- [DESIGN_DECISIONS.md](../core/DESIGN_DECISIONS.md) - i18n architecture decision