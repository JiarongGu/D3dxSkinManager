---
name: post-feature
description: Audit recent changes after a feature/fix. Detects new IPC messages, components, store state, and suggests doc updates.
---

# Post-Feature Audit

**Format**: `/post-feature`

Run after completing any non-trivial feature or bug fix. Skip for typo fixes and single-line CSS tweaks.

## Action

### 1. Detect Changes

Run `git diff` (staged + unstaged) and scan for:

| Change Type | How to detect |
|---|---|
| New IPC messages | New lines in `*Facade.cs` matching `"MESSAGE_TYPE" => await` |
| New service methods | New methods in `I*Service.cs` interfaces |
| New frontend IPC methods | New methods in `*Service.ts` extending `BaseModuleService` |
| New store state | New fields in `*Store.ts` state interfaces |
| New React components | New `.tsx` files or new `export const` components |
| New i18n keys | New entries in `Languages/en.json` or `Languages/cn.json` |
| New hooks | New `use*.ts` files |

### 2. Detect New Reusable Patterns

Check if the feature introduced a **multi-file wiring chain** — a sequence of 3+ files that must be edited together in a specific order. Signs:

- Prop/callback threaded through 3+ files (e.g., component → context → panel)
- Cross-module rendering (one module imports a component from another)
- New extension point (a place where future features will follow the same pattern)

If detected → **create `.claude/rules/{pattern-name}.md`** with:
- The exact file chain and what to change in each
- Any grouping/ordering conventions
- Cross-module wiring notes

**This is how the system evolves.** Next session gets the rule auto-loaded and skips the discovery phase.

### 3. Report

List what was detected and which updates to run:
- New IPC/constants/paths → `/doc-update-reference`
- New workflows/skills → `/doc-update-guide`
- Non-obvious patterns/decisions → `/doc-update-technical`
- New wiring chain → `.claude/rules/{name}.md` (created in step 2)
- Updated extension points → update `docs/keywords/FRONTEND.md` or `BACKEND.md`

### 4. Execute

Ask user: "Want me to run the suggested doc updates?" Execute only if confirmed.
