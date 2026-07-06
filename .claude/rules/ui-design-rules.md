# UI Design Rules

Collected from design docs and past session mistakes. Check these BEFORE writing any CSS or UI code.

## Font Sizes (STRICT)

**Only 12px or 14px.** No 13px, 15px, or other values. This is in AI_GUIDE.md and is non-negotiable.

- 14px — Standard body text, buttons, inputs
- 12px — Secondary text, labels, metadata, badges

**Titles/section headers use `CompactTitle` (14px), NOT raw antd `<Typography.Title>`.** antd Title levels
render ~20–38px — a rule violation. `CompactTitle` (used by `CompactSection`) now CLAMPS to 14px (12px
for `level={5}`), keeping antd's bold weight (fixed 2026-07-06 — the site-config section titles 基本信息/
游戏 were ~20px). Never restore a bare `<Title level=…>` in app chrome. The ONE sanctioned exception is
the in-app help doc content (`.help-window-content-area`), which scopes h1/h2 to 18/16px for reading
hierarchy — help DOC content, not chrome.

## Scrollbars — ONE global slim style, no per-component overrides

`theme-colors.css` defines a single global slim scrollbar: `* { scrollbar-width: thin; scrollbar-color:
var(--color-border-base) transparent; }`. The STANDARD properties (Chromium/WebView2 121+) render a thin
scrollbar and, when set, TAKE PRECEDENCE over any `::-webkit-scrollbar` customization — so per-component
webkit rules are inert. **Do NOT add `::-webkit-scrollbar*` or `scrollbar-width`/`scrollbar-color` in a
component CSS** — the global rule covers every scroller consistently (7 stale per-component blocks were
removed 2026-07-06). If a scroller looks wrong, fix the container, not the scrollbar.

## `width: 100%` needs `box-sizing: border-box` (else right-trim / overflow)

A block with explicit `width: 100%` + padding/border under the default `content-box` overflows its parent
by (padding+border) → the right edge gets clipped (if an ancestor has `overflow:hidden`) or forces a
scrollbar. This bit the remote Sites list rows (2026-07-06: `width:100%` content-box row overflowed 22px,
right edge trimmed). Always pair explicit `width:100%` with `box-sizing: border-box`. Prefer flex-stretch
(no explicit width) over `width:100%` where possible. And never mask overflow with `overflow-x:hidden` —
it silently trims content; make the content fit instead.

## Colors

**CSS variables only** — `var(--color-*)`. Never hardcode hex colors except in theme definitions.

## Theming — BOTH light and dark must be checked (light was broken until 2026-06-19)

Tokens live in `src/styles/theme-colors.css`: a `:root,[data-theme="light"]` block and a
`[data-theme="dark"]` block. `ThemeContext` sets `data-theme` on `<html>` AND drives the antd v6
ConfigProvider algorithm — so a component that hardcodes a color works in one theme and **breaks in the
other**. Rules:
- **Never hardcode `#fff` / `rgba(255,255,255,…)` for text or chrome** — it's invisible on a light
  surface. The header tabs + ProfileSwitcher did this (white text baked for an old dark-navy header) and
  vanished in light theme. Use `var(--color-text-base|secondary)`, `var(--color-bg-spotlight)`,
  `var(--color-primary|primary-bg)` so the element is theme-aware.
- **Light palette is a hierarchy, not flat grays:** `--color-bg-layout`/`--color-bg-container` = light-gray
  canvas (`#f0f2f5`); cards/inputs/modals = **white** (`--color-card-bg`/`--color-bg-elevated` = `#fff`)
  so surfaces pop; borders must be visible (`--color-border-base #d0d5dd`). The old near-identical grays
  (`#f7f8fa`/`#fafafa`/`#f5f5f5`) made panels invisible.
- **The header is a light surface in light theme** (`--color-header-bg: #fff`, dark text), dark in dark
  theme — driven by tokens, not hardcoded. Don't reintroduce a dark-navy (`#001529`) header.
- **When you touch any chrome CSS, screenshot BOTH themes** (set theme via
  `cdp ipc SETTING UPDATE_FIELD '{"key":"theme","value":"light"|"dark"}'` → `cdp reload`).

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
