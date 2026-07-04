---
name: react-component
description: Use when creating a new React component, screen, panel, or dialog. Generates TypeScript component + BEM CSS + hooks with project conventions.
---

# React Component Generator

Generate a complete React component following D3dxSkinManager conventions.

## Arguments

**Format**: `/react-component <ComponentName> <Type> <Features>`

**Example**:
```
/react-component ModDetailsPanel panel ipc,state,events
```

**Parameters**:
- `ComponentName` - Name in PascalCase (e.g., ModDetailsPanel, TexturePreview)
- `Type` - Component type: `panel`, `modal`, `card`, `list`, `button`, `form`
- `Features` - Comma-separated features: `ipc`, `state`, `events`, `context`, `validation`

## What This Skill Generates

1. **Component file** (`{ComponentName}.tsx`)
   - TypeScript React functional component
   - Proper props interface
   - Hooks for state, effects, IPC, context
   - Error handling with handleError
   - Event subscriptions with cleanup
   - Proper return with null check

2. **CSS file** (`{ComponentName}.css`)
   - BEM naming convention
   - Component-scoped styles
   - 12px/14px font sizes only
   - Theme-aware CSS variables

3. **Export** (updates parent index.ts if exists)

## Pattern to Follow

```typescript
// {ComponentName}.tsx
import React, { useState, useEffect, useCallback } from 'react';
import classNames from 'classnames';
import { api } from '@/shared/services/ipc';
import { handleError } from '@/shared/utils/errorHandler';
import { useProfile } from '@/contexts/ProfileContext';  // If needs profile
import { eventBus } from '@/shared/services/EventBus';
import { Module, ModEventType } from '@/shared/constants/modules';
import type { EntityType } from '@/shared/types/moduleTypes';
import './{ComponentName}.css';

interface {ComponentName}Props {
  // Props here
  id?: string;
  className?: string;
  onAction?: (data: any) => void;
}

export const {ComponentName}: React.FC<{ComponentName}Props> = ({
  id,
  className,
  onAction
}) => {
  // State
  const [data, setData] = useState<EntityType>();
  const [loading, setLoading] = useState(false);

  // Context (if needed)
  const { selectedProfileId } = useProfile();

  // Load data
  useEffect(() => {
    if (!selectedProfileId) return;

    const loadData = async () => {
      try {
        setLoading(true);
        const result = await api.module.getData(selectedProfileId, id);
        setData(result);
      } catch (error: unknown) {
        handleError(error);
      } finally {
        setLoading(false);
      }
    };

    void loadData();
  }, [selectedProfileId, id]);

  // Event subscriptions (if needed)
  useEffect(() => {
    if (!selectedProfileId) return;

    const handleEvent = (event: any) => {
      // Handle event
      void loadData();  // Refresh data
    };

    const unsubscribe = eventBus.subscribe(
      Module.MOD,
      ModEventType.EVENT_NAME,
      handleEvent
    );

    return () => {
      unsubscribe();
    };
  }, [selectedProfileId]);

  // Actions
  const handleAction = useCallback(async () => {
    if (!selectedProfileId) return;

    try {
      setLoading(true);
      await api.module.performAction(selectedProfileId, id);
      onAction?.(data);
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setLoading(false);
    }
  }, [selectedProfileId, id, data, onAction]);

  // Early return
  if (!data) return null;

  return (
    <div className={classNames('{component-name}', className)}>
      <div className="{component-name}__header">
        <h2>{data.name}</h2>
      </div>
      <div className="{component-name}__content">
        {/* Content here */}
      </div>
    </div>
  );
};
```

```css
/* {ComponentName}.css */

/* Block */
.{component-name} {
  display: flex;
  flex-direction: column;
  background: var(--color-bg-container);
  border: 1px solid var(--color-border-base);
  border-radius: var(--border-radius-base);
}

/* Elements */
.{component-name}__header {
  padding: 12px 16px;
  border-bottom: 1px solid var(--color-border-secondary);
  font-size: 14px;
  font-weight: 500;
}

.{component-name}__content {
  padding: 16px;
  font-size: 14px;
}

/* Modifiers */
.{component-name}--loading {
  opacity: 0.5;
  pointer-events: none;
}

.{component-name}--disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
```

## Component Type Patterns

### Panel Component
- Container with header and content
- May have toolbar/actions
- BEM: `.panel-name`, `.panel-name__header`, `.panel-name__content`

### Modal/Dialog Component
- **NEVER** use raw `<Modal>` or `Modal.confirm()` — use shared dialog components:
  - `ConfirmDialog` → destructive confirmations (delete, remove) — `import from 'shared/components/dialogs/ConfirmDialog'`
  - `FormDialog` → input/creation (save, create, edit) — `import from 'shared/components/dialogs/FormDialog'`
  - `InfoDialog` → read-only display (about, shortcuts) — `import from 'shared/components/dialogs/InfoDialog'`
- Shared dialogs handle theming, centering, no-animation, close button, and delayed loading
- Has visible state, onOk/onCancel handlers (async supported — loading is automatic)
- BEM: `.modal-name`, `.modal-name__body`, `.modal-name__footer`

### List Component
- Maps over array data
- Uses .map() with proper keys
- BEM: `.list-name`, `.list-name__item`, `.list-name__item-content`

### Form Component
- Uses Ant Design Form
- Has validation
- onFinish handler
- BEM: `.form-name`, `.form-name__field`, `.form-name__actions`

## Steps to Execute

1. **Determine component location**:
   - Module components: `modules/{module}/components/{ComponentName}.tsx`
   - Shared components: `shared/components/common/{ComponentName}.tsx` (atoms in `shared/components/compact/`)
   - Ask user if unclear

2. **Create component file**:
   - Generate proper imports based on features
   - If `ipc` feature: Add IPC service calls
   - If `state` feature: Add useState hooks
   - If `events` feature: Add eventBus subscriptions
   - If `context` feature: Add useProfile or other context
   - If `validation` feature: Add form validation

3. **Create CSS file**:
   - Use BEM naming (lowercase with hyphens)
   - Use CSS variables from theme (`--color-*`, `--border-*`)
   - Font sizes: 12px or 14px only
   - Add responsive styles if needed

4. **Add error handling**:
   - Wrap IPC calls in try-catch
   - Use `handleError(error)` from errorHandler utility
   - Set loading states appropriately

5. **Add event cleanup**:
   - All event subscriptions must have cleanup
   - Return unsubscribe functions in useEffect

6. **Add null checks**:
   - Check for required data before rendering
   - Return null if data not available
   - Use optional chaining for nested properties

7. **Update exports**:
   - If parent folder has index.ts, add export
   - Export pattern: `export { ComponentName } from './{ComponentName}';`

## BEM Naming Convention

- **Block**: `.component-name` (lowercase, hyphen-separated)
- **Element**: `.component-name__element`
- **Modifier**: `.component-name--modifier`

Example:
```css
.mod-details-panel { }                  /* Block */
.mod-details-panel__header { }          /* Element */
.mod-details-panel__content { }         /* Element */
.mod-details-panel--loading { }         /* Modifier */
```

## Font Size Rules

- **14px**: Regular text, labels, body content
- **12px**: Secondary info, captions, small labels
- **NEVER**: 13px or below 12px

## CSS Variables to Use

Common theme variables:
```css
/* Colors */
--color-bg-container
--color-bg-elevated
--color-bg-layout
--color-text-base
--color-text-secondary
--color-border-base
--color-border-secondary
--color-primary

/* Spacing */
--border-radius-base
--padding-base
--margin-base

/* Shadows */
--box-shadow-base
```

## Conditional Styling with classNames

Always use `classnames` library for conditional CSS:

```typescript
import classNames from 'classnames';

className={classNames('component-name', {
  'component-name--loading': loading,
  'component-name--disabled': disabled,
  'component-name--active': isActive
}, className)}  // Allow parent to pass additional classes
```

## Important Rules

- ✅ Use functional components (not class components)
- ✅ Use TypeScript with proper interfaces
- ✅ Use handleError for all errors
- ✅ Use BEM naming for CSS
- ✅ Use classnames library for conditional styles
- ✅ Font sizes: 12px or 14px only
- ✅ Add cleanup for event subscriptions
- ✅ Return null if required data missing
- ✅ Use `undefined` for missing data (NOT null)
- ✅ Use CSS variables for colors/spacing
- ❌ Don't use inline styles (use CSS classes)
- ❌ Don't use manual error notifications (use handleError)
- ❌ Don't forget event cleanup (memory leaks)
- ❌ Don't use class components
- ❌ Don't use 13px fonts
- ❌ Don't use raw `<Modal>` or `Modal.confirm()` — use ConfirmDialog/FormDialog/InfoDialog

## Reference Examples

Look at these existing components for patterns:
- `modules/mod/components/ModListPanel/ModListPanel.tsx` - List component with IPC
- `modules/setting/components/FixToolSettingsCard.tsx` - Form card with per-section save
- `shared/components/common/SlideInScreen.tsx` - Slide-in screen component
- `modules/mod/components/MergeModsDialog/MergeModsDialog.tsx` - Dialog with IPC + events

## Evolution Note

**How to update this skill**:
1. Add new component types to "Component Type Patterns" section
2. Add new features to Features parameter (e.g., `animation`, `drag-drop`)
3. Update CSS variables list as theme evolves
4. Add new reference examples as better patterns emerge
5. Update BEM conventions if project standards change
