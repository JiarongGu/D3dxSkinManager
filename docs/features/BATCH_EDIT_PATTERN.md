# Batch Edit Feature Pattern

**Related**: [AI_GUIDE.md](../AI_GUIDE.md)
**Version**: v3.3
**Created**: 2026-03-15
**Last Updated**: 2026-04-11

## Overview

Spreadsheet-style batch metadata editor using AG Grid with VSCode-style find/replace panel.

## Key Components

**BatchEditModsScreen** (`BatchEditModsScreen.tsx`)
- Slide-in screen wrapper managing state and toolbar
- Tracks edited mods vs original mods for change detection
- Global search/replace across all text columns (name, author, description)

**BatchEditGrid** (`BatchEditGrid.tsx`)
- AG Grid with custom theming using `themeQuartz.withParams()`
- Custom cell renderers for inline text highlighting
- Uses `getRowId` with ID to track mods through sorting
- Row height: 39px, Header height: 39px

**FindReplacePanel** (`FindReplacePanel.tsx`)
- VSCode-style dropdown panel at top-right of grid
- Inline toggle buttons (Aa, .*) inside input box
- 24×24px square icon buttons for consistency
- Focus border on input-group container, not individual input
- Debounced search (300ms) for performance

**HighlightCellRenderer** (`HighlightCellRenderer.tsx`)
- Custom AG Grid cell renderer for inline text highlighting
- Highlights matching text with `<mark>` tags
- Uses CSS variable `--search-highlight-bg` (theme-aware)
- Supports both plain text and regex search

## Color Variables Pattern

```css
/* Define base variable */
.batch-edit-grid {
  --search-highlight-bg: rgba(255, 235, 59, 0.4);
}

/* Theme-specific overrides */
:root:not(.dark) .batch-edit-grid {
  --search-highlight-bg: #ffeb3b80;  /* Light theme */
}

:root.dark .batch-edit-grid {
  --search-highlight-bg: rgba(255, 235, 59, 0.3);  /* Dark theme */
}
```

## AG Grid Theming Pattern

```typescript
// Use new Theming API, NOT legacy CSS
import { themeQuartz } from 'ag-grid-community';

const customTheme = themeQuartz.withParams({
  backgroundColor: 'var(--color-bg-container)',
  foregroundColor: 'var(--color-text-base)',
  borderColor: 'var(--color-border-secondary)',
  headerBackgroundColor: 'var(--color-bg-elevated)',
  rowBorder: true,
  borderRadius: 0,
  wrapperBorder: false,
});

<AgGridReact theme={customTheme} ... />
```

## Search/Replace Pattern

```typescript
// Global search across columns
const searchableColumns: Array<'name' | 'author' | 'description'> =
  ['name', 'author', 'description'];

const updated = editedMods.map(mod => {
  const updatedMod = { ...mod };
  searchableColumns.forEach(column => {
    const value = mod[column];
    if (typeof value !== 'string') return;
    // Perform replace on value
    if (newValue !== value) {
      updatedMod[column] = newValue;
    }
  });
  return updatedMod;
});
```

## Debounced Search Pattern

```typescript
import { debounce } from 'lodash-es';

const debouncedSearchChange = useCallback(
  debounce((searchConfig: ReplaceConfig | null) => {
    if (onSearchChange) {
      onSearchChange(searchConfig);
    }
  }, 300),  // 300ms debounce
  [onSearchChange]
);

// Cleanup
useEffect(() => {
  return () => {
    debouncedSearchChange.cancel();
  };
}, [debouncedSearchChange]);
```

## VSCode-Style Input Focus Pattern

```css
/* Focus on container, not input */
.find-replace-input-group {
  border: 1px solid var(--color-border-base);
  transition: border-color 0.2s;
}

.find-replace-input-group:focus-within {
  border-color: var(--color-primary);
}

/* Remove all input focus styling */
.find-replace-input-group .ant-input:focus {
  outline: none !important;
  border: none !important;
  border-color: transparent !important;
  box-shadow: none !important;
}
```

## Icon Button Consistency

```css
/* All icon buttons must be square and same size */
.find-replace-close,
.find-replace-icon-button,
.find-replace-checkbox-inline {
  height: 24px !important;
  width: 24px !important;
  min-width: 24px !important;
  padding: 0 !important;
  /* Add !important to override Ant Design defaults */
}
```

## Key Lessons

1. **AG Grid Theming API** - Use `themeQuartz.withParams()` instead of CSS hacks
2. **Color Variables** - Use project's `--color-*` convention, NOT `--ant-color-*`
3. **Inline Highlighting** - Use custom cell renderer with `<mark>` tags, not cell background
4. **Focus Styling** - Put focus on container (`:focus-within`), remove from input
5. **Button Sizing** - Use inline styles + `!important` to override Ant Design
6. **Debouncing** - Always debounce search input for performance (300ms minimum)
7. **Global Search** - Search across all relevant columns, not just one column

## Related Documentation

- [AI_GUIDE.md](../AI_GUIDE.md) - Main development guide
- AG Grid Documentation: https://www.ag-grid.com/
- Component files: `src/renderer/features/mods/screens/BatchEditModsScreen.tsx`
