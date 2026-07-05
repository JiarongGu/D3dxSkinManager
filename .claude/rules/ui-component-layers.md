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
  `common/{CloseButton,StatusIcon,HealthStatusIcon,CountBadge,StatusTag,KeyValueRows}`, `TagChip`.
  - `KeyValueRows` (added 2026-07-05): aligned label/value rows for config summaries + confirm dialogs
    (paths, commands, bindings) — `rows` + optional `title`/`hint`; `boxed` renders a bordered panel.
    Values are monospace + break-all. Use it instead of hand-rolling `__row`/`__label` path lists
    (deduped the XXMI binding summary + bind-confirm dialog in ModWorkSettingsTab).
  - `CompactField` (added 2026-06-18): standardized labeled-field row — `label` + optional
    `description`/`hint` + control via children. Use it for ALL config/tooling form rows instead of
    hand-rolling `*__label` + control + `*__hint`. Adopted by GameLaunchTab + ModFixTool.
  - `CompactIconButton` (added 2026-06-18): standardized borderless square icon-action button with a
    semantic `tone` (default/success/danger/primary). Use it for ALL inline icon actions
    (edit/confirm/cancel/etc.) instead of re-styling an antd `<Button type="text">` per component.
    Adopted by KeybindingPreview; migrate other ad-hoc icon buttons to it as you touch them.
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
   atom. Never hand-roll a styled `<button>`/`<div>` that duplicates an atom. (See `shared-utilities.md`.)
2. **New atom (L1):** pure props, no IPC/store, in `compact/` (or `common/` for non-form visuals). Must be
   usable in pure-UI Chrome with no backend.
3. **New connected piece (L3):** keep the visual part as an L1/L2 component and add a thin L3 wrapper that
   fetches/dispatches — don't bake IPC into the view.
4. **Tokens only** (per `ui-design-rules.md`): 12/14px fonts, `var(--color-*)`, BEM classes. Atoms own the
   visual tokens so consumers don't restyle.
5. **Migration is incremental** — do not mass-move files (breaks ~30 imports). When you touch a component,
   move it toward the right layer and update this table.

## Why
Without this, business logic leaks into visuals (the documented pure-UI crash class: components that
`.map` IPC data and blow up with no backend), styling drifts, and the same control is re-implemented
five ways. Layering makes L1/L2 trivially reusable + verifiable in plain Chrome, and concentrates
backend coupling in L3 where the error boundaries already live.
