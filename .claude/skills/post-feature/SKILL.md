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

### 2. Report

List what was detected and which doc-update skill to run for each:
- New IPC/constants/paths → `/doc-update-reference`
- New workflows/skills → `/doc-update-guide`
- Non-obvious patterns/decisions → `/doc-update-technical`

### 3. Execute

Ask user: "Want me to run the suggested doc updates?" Execute only if confirmed.
