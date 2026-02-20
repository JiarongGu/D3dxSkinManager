# How to Add i18n to a Component

**Difficulty:** Easy
**Time:** 10-20 minutes per component
**Prerequisites:** Basic React & TypeScript knowledge

---

## Step-by-Step Guide

### Step 1: Read the Component

```bash
# Read the component file to identify hardcoded strings
cat D3dxSkinManager.Client/src/modules/mods/components/YourComponent.tsx
```

### Step 2: Identify Hardcoded Strings

Look for:
- `"Text in quotes"`
- `` `Template ${literals}` ``
- `placeholder="..."`, `title="..."`, `label="..."`
- `notification.success("...")`
- JSX text content

**Example:**
```tsx
// Hardcoded strings to replace
<Button>Save Changes</Button>
<Input placeholder="Enter name..." />
notification.success("Operation completed!");
```

### Step 3: Add Translation Imports

Add these imports at the top of the file:

```tsx
import { useTranslation } from 'react-i18next';
import './ComponentName.css'; // If creating CSS file
```

### Step 4: Add useTranslation Hook

Inside the component, add:

```tsx
export const YourComponent: React.FC = () => {
  const { t } = useTranslation();

  // ... rest of component
};
```

### Step 5: Replace Hardcoded Strings

Replace each hardcoded string with a translation call:

```tsx
// Before
<Button>Save Changes</Button>
<Input placeholder="Enter name..." />
notification.success("Operation completed!");

// After
<Button>{t('namespace.saveChanges')}</Button>
<Input placeholder={t('namespace.enterName')} />
notification.success(t('namespace.operationCompleted'));
```

### Step 6: Add Translation Keys

Add keys to **both** `en.json` and `cn.json`:

**en.json:**
```json
{
  "translations": {
    "namespace.saveChanges": "Save Changes",
    "namespace.enterName": "Enter name...",
    "namespace.operationCompleted": "Operation completed!"
  }
}
```

**cn.json:**
```json
{
  "translations": {
    "namespace.saveChanges": "保存更改",
    "namespace.enterName": "输入名称...",
    "namespace.operationCompleted": "操作完成！"
  }
}
```

### Step 7: Handle Dynamic Content

For strings with dynamic values, use parameter interpolation:

```tsx
// Before
notification.success(`Loaded ${count} mods`);

// After
notification.success(t('mods.loadedCount', { count }));
```

**Translation files:**
```json
// en.json
"mods.loadedCount": "Loaded {{count}} mods"

// cn.json
"mods.loadedCount": "已加载 {{count}} 个模组"
```

### Step 8: Convert Inline Styles to CSS (If Needed)

If the component has inline styles, create a CSS file:

**YourComponent.css:**
```css
.your-component-container {
  padding: 16px;
  background: var(--color-bg-elevated);
}

.your-component-title {
  font-size: 18px;
  font-weight: 600;
}
```

**Component file:**
```tsx
// Before
<div style={{ padding: '16px', background: 'var(--color-bg-elevated)' }}>
  <h1 style={{ fontSize: '18px', fontWeight: 600 }}>Title</h1>
</div>

// After
<div className="your-component-container">
  <h1 className="your-component-title">{t('namespace.title')}</h1>
</div>
```

### Step 9: Verify Translation Key Parity

Run this check to ensure both language files have the same keys:

```bash
cd D3dxSkinManager/Languages
node -e "
const en = require('./en.json');
const cn = require('./cn.json');
const enKeys = Object.keys(en.translations);
const cnKeys = new Set(Object.keys(cn.translations));
const missing = enKeys.filter(k => !cnKeys.has(k));
if (missing.length > 0) {
  console.log('Missing in cn.json:', missing);
} else {
  console.log('✓ All keys synchronized!');
}
"
```

### Step 10: Test the Component

```bash
# Build frontend
cd D3dxSkinManager.Client
npm run build

# If successful, test in the application:
# 1. Run the application
# 2. Switch language in Settings > Language
# 3. Navigate to the updated component
# 4. Verify all text changes to Chinese
# 5. Switch back to English and verify
```

---

## Complete Example

### Before (Hardcoded):

```tsx
import React from 'react';
import { Button, Input } from 'antd';

export const ModEditForm: React.FC = () => {
  const handleSave = () => {
    notification.success('Mod updated successfully!');
  };

  return (
    <div style={{ padding: '20px' }}>
      <h2>Edit Mod</h2>
      <Input placeholder="Enter mod name..." />
      <Input placeholder="Enter author..." />
      <Button onClick={handleSave}>Save Changes</Button>
      <Button>Cancel</Button>
    </div>
  );
};
```

### After (i18n):

```tsx
import React from 'react';
import { Button, Input } from 'antd';
import { useTranslation } from 'react-i18next';
import './ModEditForm.css';

export const ModEditForm: React.FC = () => {
  const { t } = useTranslation();

  const handleSave = () => {
    notification.success(t('mods.edit.updateSuccess'));
  };

  return (
    <div className="mod-edit-form-container">
      <h2>{t('mods.edit.title')}</h2>
      <Input placeholder={t('mods.edit.namePlaceholder')} />
      <Input placeholder={t('mods.edit.authorPlaceholder')} />
      <Button onClick={handleSave}>{t('mods.edit.saveChanges')}</Button>
      <Button>{t('common.cancel')}</Button>
    </div>
  );
};
```

**ModEditForm.css:**
```css
.mod-edit-form-container {
  padding: 20px;
}
```

**en.json additions:**
```json
{
  "mods.edit.title": "Edit Mod",
  "mods.edit.namePlaceholder": "Enter mod name...",
  "mods.edit.authorPlaceholder": "Enter author...",
  "mods.edit.saveChanges": "Save Changes",
  "mods.edit.updateSuccess": "Mod updated successfully!"
}
```

**cn.json additions:**
```json
{
  "mods.edit.title": "编辑模组",
  "mods.edit.namePlaceholder": "输入模组名称...",
  "mods.edit.authorPlaceholder": "输入作者...",
  "mods.edit.saveChanges": "保存更改",
  "mods.edit.updateSuccess": "模组更新成功！"
}
```

---

## Naming Conventions

### Translation Keys

Use this hierarchy:
```
module.category.specificKey
```

Examples:
- `common.ok` - Common UI elements
- `mods.list.loaded` - Mod list view
- `launch.game.title` - Game launch screen
- `plugins.status.active` - Plugin status
- `dialogs.confirm.message` - Confirmation dialog

### CSS Classes

Use BEM-like naming:
```
component-name-element-modifier
```

Examples:
- `mod-list-container`
- `mod-list-item`
- `mod-list-item-selected`
- `launch-tab-button`
- `launch-tab-button-active`

---

## Common Patterns

### Modal Dialogs

```tsx
const { t } = useTranslation();

Modal.confirm({
  title: t('dialogs.confirmDelete.title'),
  content: t('dialogs.confirmDelete.message', { name: mod.name }),
  okText: t('dialogs.confirmDelete.confirm'),
  cancelText: t('common.cancel'),
  onOk: handleDelete,
});
```

### Form Labels

```tsx
<Form.Item
  label={t('mods.edit.name')}
  name="name"
  rules={[{ required: true, message: t('validation.required') }]}
>
  <Input placeholder={t('mods.edit.namePlaceholder')} />
</Form.Item>
```

### Tooltips

```tsx
<Button
  title={t('mods.actions.loadTooltip')}
  onClick={handleLoad}
>
  {t('mods.actions.load')}
</Button>
```

### Empty States

```tsx
<Empty
  description={t('mods.list.noMods')}
  image={Empty.PRESENTED_IMAGE_SIMPLE}
/>
```

---

## Troubleshooting

### Issue: Key not found warning

**Symptom:** Console warning: "i18next: key not found"

**Solution:**
1. Check key exists in `en.json` and `cn.json`
2. Verify key spelling matches exactly
3. Restart dev server after adding new keys

### Issue: Parameters not showing

**Symptom:** `{{count}}` appears literally in UI

**Solution:**
1. Ensure translation string contains `{{paramName}}`
2. Pass correct parameter object: `t('key', { paramName: value })`
3. Check parameter name matches exactly

### Issue: Language not updating

**Symptom:** Component still shows English after switching to Chinese

**Solution:**
1. Verify component uses `useTranslation()` hook
2. Check translation key exists in `cn.json`
3. Force re-render by toggling language twice
4. Clear browser cache and rebuild

---

## Checklist

Use this checklist when updating a component:

- [ ] Read component file and identify all hardcoded strings
- [ ] Add `import { useTranslation } from 'react-i18next';`
- [ ] Add `const { t } = useTranslation();` hook
- [ ] Replace all hardcoded strings with `t()` calls
- [ ] Add translation keys to `en.json` (alphabetically)
- [ ] Add matching keys to `cn.json` with Chinese translations
- [ ] Convert inline styles to CSS classes (if applicable)
- [ ] Create CSS file if needed
- [ ] Run key parity verification script
- [ ] Build and test in application
- [ ] Test language switching (EN ↔ CN)
- [ ] Verify dynamic parameters work correctly
- [ ] Check all notifications show correct text
- [ ] Commit changes with descriptive message

---

## Quick Reference

### Translation Function

```tsx
const { t } = useTranslation();

// Simple translation
t('namespace.key')

// With parameters
t('namespace.key', { param: value })

// Multiple parameters
t('namespace.key', { name: 'John', count: 5 })
```

### Common Namespaces

- `common.*` - Buttons, labels (OK, Cancel, Save, etc.)
- `mods.*` - Everything mod-related
- `launch.*` - Launch module
- `dialogs.*` - Dialog boxes
- `contextMenu.*` - Context menu items
- `validation.*` - Form validation messages
- `errors.*` - Error messages

### Files to Edit

1. **Component:** Add `useTranslation()`, replace strings
2. **en.json:** Add English translations
3. **cn.json:** Add Chinese translations
4. **CSS file (optional):** Replace inline styles

---

**Next Steps:**
- See [INTERNATIONALIZATION.md](../features/INTERNATIONALIZATION.md) for full documentation
- See [Component Status](../features/INTERNATIONALIZATION.md#components-updated) for progress
- See [Translation Guidelines](../features/INTERNATIONALIZATION.md#translation-guidelines) for style guide
