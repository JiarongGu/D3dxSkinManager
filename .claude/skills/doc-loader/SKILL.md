---
name: doc-loader
description: Load the right project docs for a task. Routes by scope keyword to specific doc files.
---

# Doc Loader

**Format**: `/doc-loader "task description" <scope>`
**Scopes**: `backend` | `frontend` | `ipc` | `testing` | `architecture` | `all`

## Action

Read the docs listed for the matching scope. Skill routing is handled by `/skill-loader` (a separate core skill).

## Routing Table

**Always read first** (every task):
- `docs/AI_GUIDE.md` — skills table, architecture patterns

### By Scope

| Scope | Read These |
|-------|-----------|
| `backend` | `docs/core/DESIGN_DECISIONS.md`, `docs/keywords/BACKEND.md` |
| `frontend` | `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md`, `docs/keywords/FRONTEND.md` |
| `ipc` | `docs/core/DESIGN_DECISIONS.md`, `docs/keywords/BACKEND.md`, `docs/keywords/FRONTEND.md` |
| `testing` | `docs/ai-assistant/TESTING_GUIDE.md` |
| `architecture` | `docs/core/DESIGN_DECISIONS.md`, `docs/architecture/CURRENT_ARCHITECTURE.md` |

### By Task Keyword (additional docs)

| Keyword in task | Also read |
|----------------|-----------|
| service, facade, event | `docs/core/DESIGN_DECISIONS.md` |
| component, hook, context | `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md` |
| error, i18n | `docs/core/DESIGN_DECISIONS.md` |
| cache | `docs/core/ADVANCED_PATTERNS.md` |
| concurrency, race condition, file system conflict, lock, planner, queue | `docs/core/DESIGN_DECISIONS.md` + rule `.claude/knowledge/filesystem-operation-serialization.md` |
| database, migration | `docs/architecture/DATABASE_MIGRATION_ARCHITECTURE.md` |
| test | `docs/ai-assistant/TESTING_GUIDE.md` |
| batch | `docs/keywords/BACKEND.md` |
| menu, context menu, right-click | `docs/keywords/FRONTEND.md` |
| export, import, package | `docs/keywords/FRONTEND.md` |
| drag, drop, reorder | `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md`, `docs/keywords/FRONTEND.md` |

### By Feature → Authoritative Rule (READ THE RULE FIRST)

`.claude/rules/*.md` are the **canonical, hot** layer — hard-won wiring chains + gotchas from past
sessions. Where a rule and a `docs/` file cover the same topic, **the rule wins** (docs/ is the deep
expansion, and can be stale). Match the task keyword → open that rule before the docs/ file.

| Task keyword | Authoritative rule |
|--------------|--------------------|
| remote library, gamebanana, huihui, quark, cloudreve, download site, site adapter | `remote-library.md` (site/API facts) + `remote-library-redesign.md` (architecture) |
| plugin, capability interface, onnx, image-review | `plugin-system.md` |
| content veil, nsfw, sensitive, blur, veil false positive / tuning | `content-veil.md` (+ `plugin-system.md` when the AI interceptor plugin is involved) |
| xxmi, launch, importer, game folder, deploy | `xxmi-integration.md` |
| onboarding, wizard, first-run, setup step | `xxmi-integration.md` (location/login onboarding) + `/react-component` |
| `.ini`, mod-merge / merge mods / combine same-character mods, namespace, texture-override, keybinding parse/rebind/reorder/drag | `3dmigoto-ini-interface.md` (write-back → also `filesystem-operation-serialization.md` archive patch) |
| mod import, workflow, resume, crash, temp cleanup, priority | `mod-import-workflow.md` |
| needs-refix, game updated, watermark | `needs-refix-watermark.md` |
| process, status bar, activity panel, long-running, fire-and-forget, blocking the UI, don't await a long op, IPC timeout | `background-task-tracking.md` |
| download, http, HttpClient, managed downloads | `download-service.md` |
| token/secret/credential at rest, login cookie, DPAPI, protect at rest, plaintext | `remote-library.md` (SecretProtector / CookieProtected — never store plaintext) |
| `app://`, webview resource, serve image, deferral | `webview-resource-serving.md` |
| concurrency, race, planner, lock, queue, archive patch / write-back, `UpdateFileInArchive`, `Directory.*`/`File.*` on mod data | `filesystem-operation-serialization.md` |
| test the app, cdp, native input, devtools loop | `desktop-app-testing.md` |
| screenshot size / oversized-image error | `screenshot-hygiene.md` |
| helper script, scratch file, tmp | `scripts-live-in-repo.md` |
| path, temp dir, profile/global path | `use-project-paths.md` |
| enum, camelCase serialization | `enum-serialization.md` |
| css, font size, antd gotcha, theme, scrollbar | `ui-design-rules.md` |
| atom, L1/L2/L3, compact component, raw antd | `ui-component-layers.md` |
| shared util (formatBytes, clipboard, tree flatten) | `shared-utilities.md` |
| per-mod / per-category derived UI data, badge | `mod-list-derived-data.md` |
| context menu, right-click, category tree | `context-menu-extension.md` |
| module boundary, cross-module access, another module's repository/service | `module-boundaries.md` |
| facade→service, fat facade, move business logic out of a facade, thin facade | `module-boundaries.md` + `risky-change-tests-first.md` |
| purge/replace git blob, force push, NSFW commit | `git-history-blob-purge.md` |
| dpi, px scaling, monitor scaling, high-DPI, 125/150/200%, secondary window size | `dpi-scaling.md` |
| in-app guide, user guide markdown | `in-app-guide.md` |
| global memory vs project rules | `no-global-memory.md` |
| risky change, wide sweep, shared type/generic, tests-first | `risky-change-tests-first.md` |
| store slice, read store, subscribe to store, component reads a store field, `useModsStore`/`useModsState`, zustand selector | `shared-utilities.md` |
| test coverage, what to test, coverage gap, P0 test | `test-coverage-priorities.md` |

Not listed? Still scan `.claude/rules/*.md` filenames — a match you don't see beats a generic template.
