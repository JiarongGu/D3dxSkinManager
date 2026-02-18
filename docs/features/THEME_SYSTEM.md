# Theme System

**Status:** ✅ Implemented (2026-02-18)
**Type:** Core Feature - UI/UX
**Priority:** High

---

## Overview

Comprehensive theme system providing light/dark/auto modes with centralized color management using CSS custom properties and React Context API.

## Architecture

### Components

```
ThemeProvider (Context)
    ↓
App (ConfigProvider with Ant Design theme)
    ↓
All Components (use CSS variables)
```

### Files Structure

```
src/
├── shared/
│   └── context/
│       └── ThemeContext.tsx          # Theme state management
├── styles/
│   ├── theme-colors.css              # CSS custom properties (50+ variables)
│   ├── App.css                       # Global styles
│   └── visual-enhancements.css       # UI polish
└── App.tsx                           # ConfigProvider integration
```

## Theme Modes

### 1. Light Mode
- Clean, bright design
- Subtle shadows
- High contrast text

### 2. Dark Mode
- Dark backgrounds (#141414, #1f1f1f, #262626)
- Muted colors for reduced eye strain
- Enhanced shadows for depth
- Adjusted primary colors (#177ddc)

### 3. Auto Mode
- Follows system preference
- Automatically detects changes
- Real-time switching without reload

## CSS Custom Properties

### Color Categories (50+ variables)

#### Background Colors
```css
--color-bg-base          /* Main background */
--color-bg-container     /* Card/container background */
--color-bg-elevated      /* Modal/dropdown background */
--color-bg-layout        /* Page layout background */
--color-bg-spotlight     /* Highlighted section background */
--color-bg-mask          /* Overlay mask */
```

#### Border Colors
```css
--color-border-base      /* Primary borders */
--color-border-secondary /* Subtle borders */
```

#### Text Colors
```css
--color-text-base        /* Primary text */
--color-text-secondary   /* Secondary text */
--color-text-tertiary    /* Muted text */
--color-text-quaternary  /* Very muted text */
--color-text-inverse     /* Inverse text (for dark backgrounds) */
```

#### Status Colors
Each status color has 3 variants:
```css
--color-{status}         /* Base color */
--color-{status}-bg      /* Background */
--color-{status}-border  /* Border */
```

Status types: `primary`, `success`, `warning`, `error`, `info`

#### Component-Specific
```css
--color-card-bg
--color-card-header-bg
--color-input-bg
--color-input-border
--color-select-bg
--color-table-header-bg
--color-table-row-hover
--color-sider-bg
--color-header-bg
--color-header-text
```

#### Shadows
```css
--shadow-base       /* Default shadow */
--shadow-elevated   /* Modal/dropdown shadow */
--shadow-card       /* Card shadow */
```

## Implementation Guide

### For New Components

#### 1. Use CSS Variables for Colors

**❌ Bad:**
```tsx
<div style={{ background: '#ffffff', color: '#000000' }}>
```

**✅ Good:**
```tsx
<div style={{ background: 'var(--color-bg-container)', color: 'var(--color-text-base)' }}>
```

#### 2. Use Theme Hook for Programmatic Access

```tsx
import { useTheme } from '../shared/context/ThemeContext';

function MyComponent() {
  const { theme, effectiveTheme, setTheme } = useTheme();

  // theme: 'light' | 'dark' | 'auto'
  // effectiveTheme: 'light' | 'dark' (resolved)

  return <div>Current theme: {effectiveTheme}</div>;
}
```

#### 3. Color Selection Guide

| Use Case | CSS Variable |
|----------|-------------|
| Card background | `--color-card-bg` |
| Text (primary) | `--color-text-base` |
| Text (secondary) | `--color-text-secondary` |
| Borders | `--color-border-secondary` |
| Success message | `--color-success` |
| Error message | `--color-error` |
| Warning message | `--color-warning` |
| Info box background | `--color-info-bg` |
| Sidebar background | `--color-sider-bg` |

### For Settings Integration

Theme selector is already integrated in `SettingsView.tsx`:

```tsx
<Form.Item label="Theme" name="theme">
  <Select onChange={handleThemeChange}>
    <Option value="light">Light</Option>
    <Option value="dark">Dark</Option>
    <Option value="auto">Auto (System)</Option>
  </Select>
</Form.Item>
```

## Automatic Component Styling

All Ant Design components are automatically styled via CSS selectors in `theme-colors.css`:

- ✅ Layout components
- ✅ Cards
- ✅ Inputs, Selects, Pickers
- ✅ Tables
- ✅ Modals, Dropdowns
- ✅ Menus, Trees
- ✅ Forms, Tags, Alerts
- ✅ Statistics, Descriptions
- ✅ Empty states
- ✅ Scrollbars (dark theme)

## Testing Themes

### Manual Testing
1. Go to Settings
2. Change theme selector
3. Verify all pages:
   - Mods view (table, sidebars, preview)
   - Tools view
   - Settings view
   - Status bar
4. Check system auto-switching (for auto mode)

### What to Check
- [ ] Text is readable in both themes
- [ ] Borders are visible but not harsh
- [ ] Backgrounds provide proper contrast
- [ ] Status colors (success/error/warning) are clear
- [ ] No hardcoded colors remain
- [ ] Modal/dropdown backgrounds are distinct
- [ ] Scrollbars work in dark mode

## Common Patterns

### Info Box
```tsx
<div style={{
  padding: '12px',
  background: 'var(--color-info-bg)',
  border: '1px solid var(--color-info-border)',
  borderRadius: '4px',
  color: 'var(--color-text-base)'
}}>
  Information message
</div>
```

### Warning Box
```tsx
<div style={{
  padding: '12px',
  background: 'var(--color-warning-bg)',
  borderRadius: '4px',
  color: 'var(--color-text-base)'
}}>
  Warning message
</div>
```

### Status Icon
```tsx
<CheckCircleOutlined style={{ color: 'var(--color-success)' }} />
<ExclamationCircleOutlined style={{ color: 'var(--color-error)' }} />
<WarningOutlined style={{ color: 'var(--color-warning)' }} />
```

## Performance Considerations

1. **CSS Variables are Fast**: Browser-native, no JavaScript overhead
2. **Single Import**: All themes in one file
3. **No Runtime Computation**: Colors defined statically
4. **Efficient Switching**: Only `data-theme` attribute changes

## Migration from Hardcoded Colors

To migrate existing components:

1. Find hardcoded colors: `grep -r "color.*#" src/`
2. Replace with appropriate CSS variable
3. Test in both themes
4. Update border colors: `#d9d9d9` → `var(--color-border-secondary)`
5. Update backgrounds: `#fff` → `var(--color-bg-container)`

## Future Enhancements

- [ ] Additional theme modes (high contrast, custom themes)
- [ ] Theme customization UI (color picker)
- [ ] Export/import custom themes
- [ ] Per-profile theme preferences
- [ ] Theme preview in settings

## Related Documentation

- [CHANGELOG.md](../CHANGELOG.md) - Implementation history
- [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md) - Component locations
- [AI_GUIDE.md](../AI_GUIDE.md) - Best practices

## Troubleshooting

### Theme Not Applying
- Check `data-theme` attribute on `<html>` or `<body>`
- Verify CSS file is imported in App.tsx
- Clear browser cache

### Colors Not Updating
- Ensure using `var(--color-*)` not hardcoded values
- Check CSS specificity (may need `!important` for overrides)
- Verify ThemeProvider wraps entire app

### System Auto-Detection Not Working
- Check browser supports `prefers-color-scheme`
- Verify MediaQuery listener is attached
- Check console for errors

## Best Practices

1. ✅ **Always use CSS variables** for colors
2. ✅ **Test both themes** before committing
3. ✅ **Use semantic variable names** (not literal colors)
4. ✅ **Consider accessibility** (contrast ratios)
5. ✅ **Document new variables** if adding custom colors
6. ❌ **Never hardcode colors** in inline styles
7. ❌ **Don't use hex/rgb values** directly in components
