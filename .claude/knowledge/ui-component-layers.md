# UI Component Layers (atomic design: L1 / L2 / L3)

Standardize UI into three layers by **what a component is allowed to depend on**. The layer is defined
by dependencies, not by size. This keeps visuals reusable, testable, and consistent.

## The three layers

| Layer | Name | May import | MUST NOT import | Lives in |
|-------|------|-----------|-----------------|----------|
| **L1** | Atom — pure visual | antd, icons, `classNames`, CSS, design tokens; text via **props** | services/ipc, stores (zustand), `useProfile`, event bus, business logic | `shared/components/compact/`, `shared/components/common/` |
| **L2** | Molecule — composition + presentational logic | L1 atoms, antd, hooks for **local** UI state, data passed via **props** | services/ipc, stores, event bus | `shared/components/dialogs/`, `menu/`, `notification/`, `common/` (DataTable, SlideInScreen) |
| **L3** | Connected — business logic | L1 + L2, services/ipc, stores, contexts, event bus, i18n data | — (top of the chain) | `shared/components/` (connected, e.g. CategorySelect) and **module** `components/` |

**Golden rule:** data and side-effects flow *down* as props/callbacks. L1/L2 are dumb; only L3 talks to
the backend (`services/ipc`), stores, or the event bus. If a "pure" component reaches for `modService`
or `useModsStore`, it's actually L3 — split it: a dumb L1/L2 view + an L3 wrapper that feeds it.

## Current classification (audit 2026-06-18)

- **L1 atoms:** `compact/Compact{Button,Input,Select,Switch,Text,Card,Alert,Divider,Space,Section,Field}`,
  `compact/{CompactIconButton,CountChip}`,
  `common/{CloseButton,StatusIcon,HealthStatusIcon,CountBadge,StatusTag,KeyValueRows}`, `TagChip`.
  - `KeyValueRows` (added 2026-07-05): aligned label/value rows for config summaries + confirm dialogs
    (paths, commands, bindings) — `rows` + optional `title`/`hint`; `boxed` renders a bordered panel.
    Values are monospace + break-all. Use it instead of hand-rolling `__row`/`__label` path lists
    (deduped the XXMI binding summary + bind-confirm dialog in ModWorkSettingsTab).
  - `CompactField` (added 2026-06-18): standardized labeled-field row — `label` + optional
    `description`/`hint` + control via children. Use it for ALL config/tooling form rows instead of
    hand-rolling `*__label` + control + `*__hint`. Adopted by GameLaunchTab + ModFixTool.
  - `CompactIconButton` (added 2026-06-18): standardized borderless square icon-action button with a
    semantic `tone` (default/success/danger/primary/**warning** — warning added 2026-07-14). Use it for
    ALL inline icon actions (edit/confirm/cancel/etc.) instead of re-styling an antd `<Button
    type="text">` per component. Adopted by KeybindingPreview, analyzer HistoryView + FindingsView
    fix-dropdown (2026-07-05 — replacing a `type="primary"` + `type="primary" danger` bordered pair whose
    1px borders rasterized on different pixel rows at fractional DPI, the recurring "danger button sits
    higher" report), and the ModImportWorkflowTable row actions (2026-07-14 — they were wrongly using
    `CompactButton.Primary/.Danger/...`, re-introducing the misalignment; the atom centres the glyph
    deterministically, verified glyph-vs-box offset 0). Migrate other ad-hoc icon buttons to it as you
    touch them.
  - `CountChip` (added 2026-07-14): label + count pill / filter toggle with a `tone`
    (default/running/waiting/completed/failed) + `active` + optional leading `icon`. Equalizes the
    (often CJK) label and the Latin count digit's line boxes so the digit doesn't float higher — see
    `ui-design-rules.md` "CJK label + Latin count digit". Use it for ALL "label N" count pills instead of
    hand-rolling spans with `line-height: normal`. Adopted by the mod import queue filter chips.
  - `StatusTag` (added 2026-06-18): semantic status tag — `tone` (success/error/warning/processing/
    neutral/info) → consistent color + default icon (`icon={null}` suppresses, e.g. dense count pills).
    Use it for ALL status pills (process status, fix results, mod/health states). Adopted by
    ActivityPanel, ModFixTool, HistoryView, FindingsView, ModIdMigrationTool, ProfileManager,
    ModImportWorkflowTable, ImportTab, ModList, AppStatusBar. **Leave** content/categorical/interactive
    tags as antd `<Tag>` (mod tags via `TagChip`, age via `GradingTag`, category/author/type pills,
    asset-presence labels, closable filter chips, help/about prose).
- **L2 molecules:** `dialogs/{ConfirmDialog,FormDialog,InfoDialog}`, `menu/ContextMenu`,
  `notification/CustomNotification`, `common/{DataTable,SlideInScreen}`, `compact/Compact{Upload,ThumbnailUpload}`.
- **L3 connected (shared):** `CategorySelect` (loads categories via IPC).
- **L3 connected (module):** panels/screens under `modules/*/components/` (ModListPanel, CategoryPanel,
  GameLaunchTab, ModFixTool, LaunchButton, ActivityPanel, …) — these own IPC/store/event wiring.

## Rules for the agent

1. **Reuse before creating.** Need a button/input/tag/badge/card? Use the `compact/` atom or a `common/`
   atom. Never hand-roll a styled `<button>`/`<div>` that duplicates an atom, and **never use raw antd
   form controls (`Button`/`Input`/`TextArea`/`Select`/`Switch`/`Checkbox`/`InputNumber`) in L3 views**
   — an app-wide sweep migrated the offending files (2026-07-05 for Button/Input/…; 2026-07-11 for
   Checkbox→`CompactCheckbox` + InputNumber→`CompactInputNumber`; the mixed-height toolbar bug class).
   Raw antd stays ONLY for components with no atom (Modal→dialogs/,
   Table/Tabs/Tree/Dropdown/Pagination/Spin/Empty/Tooltip/Segmented/Radio) and `Input.Search`.
   `CompactInput`/`CompactButton`/`CompactInputNumber` forward refs. (See `shared-utilities.md`.)
2. **New atom (L1):** pure props, no IPC/store, in `compact/` (or `common/` for non-form visuals). Must be
   usable in pure-UI Chrome with no backend.
3. **New connected piece (L3):** keep the visual part as an L1/L2 component and add a thin L3 wrapper that
   fetches/dispatches — don't bake IPC into the view.
4. **Tokens only** (per `ui-design-rules.md`): 12/14px fonts, `var(--color-*)`, BEM classes. Atoms own the
   visual tokens so consumers don't restyle.
5. **Migration is incremental** — do not mass-move files (breaks ~30 imports). When you touch a component,
   move it toward the right layer and update this table.

## Atom sizing contract — the atoms are STRICT; make a NEW atom when a pattern doesn't fit (2026-07-07)

The form-control atoms **enforce their size/hover with `!important`** so the app stays uniform:
`CompactButton` = 32px (medium) via `.compact-button-medium { height:32px !important }`, `CompactInput`/
`CompactSelect` = 32px, `CompactIconButton` = a 26px borderless square whose hover is a **background
fill** (`--color-bg-spotlight` / tone-tinted), and `CompactTab` = a transparent 40px toolbar item.

**Consequence (learned the hard way — a whole session of regressions):** you CANNOT drop a standard
atom onto a component that needs different chrome and override it with plain CSS — the atom's
`!important` wins and clobbers your styling (broke the header tabs, the profile button, and the icon
actions). So:
- **A control's chrome differs from the standard atom → make a NEW L1 atom** (as `CompactTab` was made
  for the 40px transparent toolbar tabs + profile trigger). Do NOT revert to raw antd, and do NOT fight
  the atom's `!important` with more `!important`.
- **Icon-only actions → `CompactIconButton`** (never a raw `<Button type="text">` or a bare
  `CompactButton` with just an icon). Its hover is a real bg fill; the old ad-hoc icon buttons had
  border-only or invisible (`:hover{background:var(--color-bg-elevated)}` = the panel colour) hovers
  that read as faint "ghost" buttons.
- **Search inputs → `CompactInput` + a `prefix={<SearchOutlined/>}`**, NOT antd `Input.Search` (its
  bordered search-button segment is redundant when search is live via `onChange`, and looks split).
- **A `+`/toolbar action next to a search → `CompactButton type="default"` (icon-only) or
  `CompactIconButton`** — keep the two search bars' containers identical (`.mod-list-panel-search-bar` ↔
  `.category-grid-header`: same padding/border/48px/align-items).
- **Panels that fill height** (mod-list, etc.): the antd `Sider` children live in
  `.ant-layout-sider-children` — make THAT the flex column and let content `flex:1 1 0`; never a fixed
  `height: calc(100% - Npx)` that ignores a conditional row (the on-search filter chip pushed the status
  bar off-screen).
- `InputNumber` → `CompactInputNumber` (24/32/40px heights matching `CompactInput`); `Checkbox` →
  `CompactCheckbox`. Both are drop-in (forward all antd props). `Radio` still has no atom (raw allowed).

## Why
Without this, business logic leaks into visuals (the documented pure-UI crash class: components that
`.map` IPC data and blow up with no backend), styling drifts, and the same control is re-implemented
five ways. Layering makes L1/L2 trivially reusable + verifiable in plain Chrome, and concentrates
backend coupling in L3 where the error boundaries already live.
