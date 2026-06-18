# UI Design Rules

Collected from design docs and past session mistakes. Check these BEFORE writing any CSS or UI code.

## Font Sizes (STRICT)

**Only 12px or 14px.** No 13px, 15px, or other values. This is in AI_GUIDE.md and is non-negotiable.

- 14px — Standard body text, buttons, inputs
- 12px — Secondary text, labels, metadata, badges

## Colors

**CSS variables only** — `var(--color-*)`. Never hardcode hex colors except in theme definitions.

## Ant Design Component Gotchas

### `danger` prop causes icon button misalignment

Ant Design's `danger` prop on `<Button>` uses a different internal rendering path than `type="primary"`. When placed side-by-side, icon-only `danger` buttons render at a slightly different vertical position than `primary` buttons.

**Workaround:** Use inline `style={{ color: 'var(--color-error)' }}` on the icon instead of the `danger` prop when alignment with adjacent buttons matters. Or accept the minor visual difference for non-icon buttons where it's less noticeable.

### `Empty` component is for "no data" states only

Don't use `<Empty>` for hero/landing screens. It adds unwanted default styling and semantics. Build custom hero layouts with plain divs + BEM classes.

### `Modal` blinks on open — always disable the transitions

Every antd `<Modal>` MUST set **`transitionName="" maskTransitionName=""`**. The default zoom/fade
animation **blinks** in this app (re-render on open + dev StrictMode double-mount fight the animation).
The shared dialogs (`ConfirmDialog`/`FormDialog`/`InfoDialog`) already do this; new/ad-hoc Modals
(MergeModsDialog, UnityArgsDialog, PluginsView) were missing it and blinked. Fixed 2026-06-19 — keep it
on every Modal. (Better: build dialogs on the shared `dialogs/` components, which bake this in.)

## Pattern Reuse (CHECK FIRST)

Before building a new UI pattern, **search for existing implementations**:

| Need | Search for |
|---|---|
| Category selector | `CategorySelect` in `shared/components/`, `flattenCategories` in ExportTab |
| Confirmation dialog | `ConfirmDialog` in `shared/components/dialogs/` |
| Slide-in screen | `useSlideInScreen` hook |
| Compact buttons/inputs | `shared/components/compact/` |
| Count badges | `CountBadge` in `shared/components/common/` |

**Never build a new TreeSelect for categories** — use the shared `CategorySelect` component (flat dropdown with breadcrumb labels).

## Context Menus — no ellipsis

**Never put `…` or `...` in context-menu item labels** (mod list, category tree, preview, fix submenu).
User preference, applied 2026-06-18. Menu items read as plain actions ("Replace Content from File",
"Manage fix tools", "Fix all in category"). Ellipsis is fine elsewhere (input placeholders, loading
text, toolbar buttons), just not in right-click menus.
