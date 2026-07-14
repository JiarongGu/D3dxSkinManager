# UI Design Rules

Collected from design docs and past session mistakes. Check these BEFORE writing any CSS or UI code.

## Font Sizes (STRICT)

**Only 12px or 14px.** No 13px, 15px, or other values. This is in AI_GUIDE.md and is non-negotiable.

- 14px — Standard body text, buttons, inputs
- 12px — Secondary text, labels, metadata, badges

**Titles/section headers use `CompactTitle` (14px), NOT raw antd `<Typography.Title>`.** antd Title levels
render ~20–38px — a rule violation. `CompactTitle` (used by `CompactSection`) now CLAMPS to 14px (12px
for `level={5}`), keeping antd's bold weight (fixed 2026-07-06 — the site-config section titles 基本信息/
游戏 were ~20px). Never restore a bare `<Title level=…>` in app chrome.

**Prominent CONTENT titles MAY be 16–18px (the distinction, clarified 2026-07-07).** The 12/14 rule
governs **chrome**: section headers, inline labels, buttons, table/list text. But a **page / detail-panel
/ hero title** — the mod-detail panel title (`.mod-preview-title`, **18px**), the slide-in header
(`.slide-in-screen-title`, 18px), the remote-detail hero (`.remote-detail__hero-title`, 16px) — carries
the screen's hierarchy and stays **16–18px**, NOT 14px. Don't shrink these to 14px "for the rule" (the
mod-detail title was shrunk to 14px and the user asked for it back at 18px). Rule of thumb: **one main
title per screen/panel = 16–18px; everything else = 12/14.** The in-app help doc content
(`.help-window-content-area`, 16/18px) is the other exception.

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

## A flex item with `overflow` set gets `min-height:auto → 0` and can COLLAPSE to 0px

Per CSS spec, a flex item's automatic minimum size (`min-height:auto`, which normally keeps it at least
its content height) only applies **when `overflow` is `visible`**. Set ANY non-visible `overflow`
(`auto`/`scroll`/`hidden`, incl. `overflow-x`) on a flex item and its `min-height` resolves to **0** —
so under any shrink pressure (a taller sibling in the same flex column) it collapses to 0px even though
it has content. This bit ModFixTool (2026-07-07): the tools table had `flex:1; overflow:auto` and a tall
settings card below it in the same flex column → the table crushed to **0px** (rows present in the DOM,
invisible). **Fix pattern:** don't put `overflow` on the flex item you want visible — let the PANEL
(`.mod-fix`) own the vertical scroll (`overflow-y:auto`) and give the child `flex:0 0 auto` (natural
height, never shrink). For a genuinely-scrolling flex child, add `min-height:0` deliberately (that's the
opposite case — you WANT it to shrink and scroll internally). Know which one you want.

## SlideInScreen `width` prop is a NO-OP — the panel is `flex:1`; use `className` to scope geometry

`useSlideInScreen({ width })` sets an inline `width` on `.slide-in-screen-panel`, but that element is
`flex: 1` (CSS), so it **grows to fill** the container regardless — every slide-in renders ~95% wide and
the `width` value (`'80%'`, `'560px'`, `'50%'`…) is silently ignored (verified 2026-07-07: a `560px`
panel measured 1105px). Do NOT try to narrow a slide-in by changing `width` — it does nothing. To make a
genuinely narrow/focused panel, pass `className` to `useSlideInScreen` (added 2026-07-07; threads to the
`.slide-in-screen-container`) and scope the geometry there — grow the backdrop, fix the panel:
```css
.my-screen .slide-in-screen-blur-backdrop { flex: 1 1 auto; width: auto !important; }  /* backdrop fills */
.my-screen .slide-in-screen-panel { flex: 0 1 560px; max-width: 100%; }                 /* panel fixed  */
```
This is opt-in (other slide-ins keep filling ~95%). ModFixTool uses `className="mod-fix-screen"` for a
560px focused panel. (Globally honoring `width` would resize all 8+ slide-ins at once — don't.)

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

### `<Tag icon={...}>` renders the icon ~1px HIGH (worst on the red/error tag)

antd's `<Tag>` is `display:inline-block` and the icon child keeps a text `vertical-align`, so the glyph
sits above the tag's vertical centre (measured on the `StatusTag` error tag: box 26px, icon top-gap 4px
vs bottom-gap 6px → centre off by −1px). Most visible on the DANGER/error tag (red draws the eye), which
is why it reads as "the danger icon is higher" — but it affects EVERY toned tag with an icon. Not the
same as the `danger` **button** gotcha above (that's a Button; this is a Tag).

**Fix (at the atom, 2026-07-14):** `StatusTag` now always carries a `status-tag` class + `StatusTag.css`
that flex-centres the tag: `.ant-tag.status-tag { display:inline-flex; align-items:center }` (+ `.anticon
{ line-height:0 }`). Re-measured: top-gap 5 == bottom-gap 5, centre offset 0. Fixing the atom aligns
every StatusTag icon app-wide — never patch it per-use.

### Multi-select (`mode="tags"`/`multiple`) chip is taller than its 32px sibling inputs

A multi-select's selected chips make the control taller than adjacent 32px `CompactInput`s, so a
rule/alias row with a tag-select looks uneven once a tag is picked. Two things drive chip height and
BOTH must be set — sizing only `.ant-select-selection-item` is not enough (bit twice, 2026-07-06):
- The chip BOX: `.ant-select-selection-item` — set `height`, `display:inline-flex; align-items:center`,
  small `margin`.
- The chip TEXT: `.ant-select-selection-item-content` — its `line-height` **defaults to the control
  height (30px)** and overflows a shorter chip. Set it to match (e.g. `18px`).

```css
.my-tag-select .ant-select-selection-item {
  height: 20px; display: inline-flex; align-items: center;
  font-size: 12px; margin-top: 2px; margin-bottom: 2px;
}
.my-tag-select .ant-select-selection-item-content { line-height: 18px; } /* was 30px → overflowed */
```
Verify in the app (`cdp eval` the chip's computed `.ant-select-selection-item-content` line-height) —
the box can look right while the text line-height is still 30px.

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
