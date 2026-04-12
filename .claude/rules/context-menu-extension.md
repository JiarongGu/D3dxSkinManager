# Extending Category Right-Click Context Menu

When adding a new action to the category tree right-click menu, follow this 4-file wiring chain:

## Wiring Chain

1. **CategoryContextMenu.tsx** — Add callback prop + menu item
   - Add `onNewAction?: (nodeId: string) => void` to `CategoryContextMenuProps`
   - Add to `getCategoryContextMenu()` params destructuring
   - Add menu item in the correct group (see Grouping below)

2. **CategoryTreeContext.tsx** — Pass callback through context
   - Add `onNewAction?` to `CategoryTreeProviderProps`
   - Destructure in provider component
   - Pass to `getCategoryContextMenu()` call
   - Add to `useMemo` dependency array

3. **CategoryTree.tsx** — Add to public props interface
   - Add `onNewAction?` to `CategoryTreeProps`
   - (Auto-spread via `{...props}` to provider — no extra wiring)

4. **CategoryPanel.tsx** — Implement the handler
   - Add state/callback for the action
   - Pass `onNewAction={handler}` to `<CategoryTree>`
   - Render any UI triggered by the action (dialogs, tools, etc.)

## Menu Grouping Convention

```
Add Sub-Category    ┐
Add Root-Category   ┘ Group 1: Creation
─────────────────
Edit                ┐
Export              ┘ Group 2: Non-destructive operations
─────────────────
Delete              ─ Group 3: Destructive (danger, isolated)
```

## Cross-Module Tool Opening

To open a Tool module component from the Mod module (e.g., ModPackageTool from CategoryPanel):
- Import the tool component directly (no IPC needed — it's same-process React)
- Control visibility with local state
- Pass context via props (e.g., `initialCategoryId`)
- The tool component uses `useSlideInScreen` internally for presentation
