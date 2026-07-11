# `devtools/` — D3dxSkinManager dev + test toolkit

This folder **is** the develop-and-test process for the real desktop app. Driving / building /
capturing is done through committed, **allow-listed** `node devtools/dev.mjs …` commands so the loop
runs **prompt-free + unattended**. Adapted from a sibling toolkit —
same stack (WebView2 + WinForms + React + .NET 10).

Strategy + how CDP is wired: [`../.claude/knowledge/desktop-app-testing.md`](../.claude/knowledge/desktop-app-testing.md) ·
conventions: [`../.claude/rules/scripts-live-in-repo.md`](../.claude/rules/scripts-live-in-repo.md) ·
capture limits: [`../.claude/knowledge/screenshot-hygiene.md`](../.claude/knowledge/screenshot-hygiene.md).

## Universal entry

```
node devtools/dev.mjs <command> [...args]
node devtools/dev.mjs help
```
One dispatcher (`dev.mjs`) forwards to every tool. All covered by `Bash(node devtools/dev.mjs:*)`.

## Structure (generic tools vs project inputs — so the toolkit is reusable)
- `dev.mjs` (dispatcher) · **`project.config.mjs` — the ONLY project-specific file** (exe/csproj/client
  paths, CDP port, Vite URL match, `devGlobal`, `playSelector`, `reviewTabs`). Reuse on another app =
  copy `devtools/` + edit this one file.
- `scripts/` — generic dev/test scripts, parameterized from the config.
- `research/`, `wgc-shot/`, `win-input/` — self-contained tool packages (research = TS/puppeteer; the
  other two = zero-NuGet C#, built on first use).

## Adopt this toolkit in another desktop app
The `scripts/` are generic; **only `project.config.mjs` is app-specific** — no `scripts/*.mjs` (core)
hardcodes an app name, selector, or port. To reuse on another WebView2/WinForms (or Electron) app:

1. **Copy the CORE** into the new repo's `devtools/`: `dev.mjs`, `project.config.mjs`, the helpers
   `scripts/{app-dev, drive-cdp, shot-wgc, _capture-util, _cdp-port, _dev-window, win-input,
   check-desktop, review-desktop, crop, research}.mjs`, and the `wgc-shot/`, `win-input/`, `research/`
   packages. **Leave the DOMAIN tools** — `i18n-audit`, `veil-eval`, `build-manifest`,
   `test-update-apply`, `codemod-*`, and the `knowledge` doctors — those are D3dx-specific *examples*;
   write your own domain tools and add them to the `NODE` map in `dev.mjs`.
2. **Edit `project.config.mjs`** — every project input lives there: `name`, `shotPrefix`, `processName`,
   `exe`, `csproj`, `binDir`, `clientDir`, `cdpPort` (fallback), `viteUrlMatch`, `devGlobal`,
   `playSelector`, `reviewTabs`, `healthProbe`, `debugFlags`, `devEnv`. Nothing else to touch.
3. **Allow-list** `Bash(node devtools/dev.mjs:*)` in `.claude/settings.json` so the loop stays prompt-free.
4. Verify: `node devtools/dev.mjs app rebuild && node devtools/dev.mjs check` → a green `healthy` verdict
   (incl. `contentRendered` from your `healthProbe`) confirms the harness is wired to the new app.

> **CORE vs DOMAIN commands.** Portable core: `app · cdp · shot · input · check · review · crop ·
> research`. D3dx domain examples (replace when adopting): `manifest · test-update-apply · i18n · veil ·
> knowledge`.

## The tools

| Command | What |
|---|---|
| `node devtools/dev.mjs app <kill\|build\|tsc\|test\|start\|restart\|rebuild\|wait> [port]` | **App lifecycle + checks.** `rebuild` = kill → `dotnet build` (errors only) → relaunch (dev mode + CDP) → wait. `build` = backend build; `tsc` = frontend typecheck; `test [path]` = vitest in the client dir (no `cd`). `kill` is **path-matched to the repo bin exe** — a user-installed copy running from another folder is never touched. |
| `node devtools/dev.mjs cdp <open\|nav\|menu\|probe\|key\|eval\|evalfile\|reload\|ipc\|events\|iplog\|shot\|grab\|wait> [arg] [port]` | **Drive + capture over CDP.** `open` clicks `playSelector`; `nav "<Tab>"`; `menu "<Item>"` (right-click 1st card → item); `eval "<js>"`; `reload [ms]`; `ipc <MOD> <TYPE> '<json>'` + `events`/`iplog` (via `window.__d3dx`); `shot [label]` = DOM screenshot; `wait <ms>`. |
| `node devtools/dev.mjs shot [label]` | **Occlusion-immune** window capture (Windows.Graphics.Capture; works while the console covers the app). Builds `wgc-shot/` on first use. Targets the **dev-instance window** (path-matched HWND via `_dev-window.mjs`) — never a user-installed copy running alongside. |
| `node devtools/dev.mjs input <click\|rclick\|move\|drag> <x> <y> [x2 y2]` | **Native mouse input** (Win32 `PostMessage` to the leaf HWND; `x,y` = fractions 0..1 of the client area). Builds `win-input/` on first use. Pair with `shot`. Targets the **dev-instance window** only (same path-match). |
| `node devtools/dev.mjs check [port]` | Health verdict over CDP (CSS loaded? React mounted? console errors?) + a DOM screenshot. |
| `node devtools/dev.mjs review [port]` | Sweep every tab in `project.config.mjs` `reviewTabs` + per-tab screenshots (regression). |
| `node devtools/dev.mjs research <search\|scrape> …` | **Web research** (puppeteer + stealth, self-contained `research/`). Auto-installs Chromium (~280MB) on first use. |
| `node devtools/dev.mjs crop <png> <x> <y> <w> <h>` | Crop a capture. |
| `node devtools/dev.mjs manifest <dir> <version> [outFile]` | Generate the auto-update `manifest.json` (path/size/sha256 per file; excludes the launcher). Used by `release.yml`. See `docs/LAUNCHER_ARCHITECTURE.md`. |
| `node devtools/dev.mjs test-update-apply` | End-to-end test of the launcher's APPLY phase: sandbox install + staged update → runs the real launcher `--apply-and-exit` → asserts overlay/add/remove/cleanup. |
| `node devtools/dev.mjs i18n` | **i18n completeness audit**: both language JSONs parse, en↔cn key-set diff, and code-referenced keys (`t('…')`, backend `titleKey`/`detailKey`, `OperationException` codes → `errors.*`) missing from BOTH files. Exit 1 on any issue — run after adding i18n keys. |
| `node devtools/dev.mjs veil [pages\|labels\|sweep]` | **Content-veil eval + tuning** (dev app must be running). `labels` = the user-labeled image corpus `devtools/fixtures/veil/{positive,negative}` (folder = label; cases in `fixtures/veil-labels.json` auto-snapshot on first run — drop extra images in by hand). `sweep` = grid-search `ContentVeilTuning` via per-request overrides (no rebuild per config) — apply the winner to the defaults. Numeric mode = GameBanana Subfeed ratings as WEAK labels. See `.claude/knowledge/content-veil.md`. |
| `node devtools/dev.mjs knowledge <check\|footprint\|new>` | **Rules-system doctors** (two-tier `.claude/rules` core + on-demand `.claude/knowledge/`; see `.claude/rules/RULES_INDEX.md`). `check` = integrity (every rule indexed, links resolve, no stale `.claude/rules/<moved>` refs, core hasn't re-inflated, doc-loader/skill-loader routers reference real+indexed rules). `footprint` = always-loaded session base + 64KB budget. `new <kebab-name> [--core]` = scaffold a rule from `TEMPLATE.md` + auto-add its `RULES_INDEX` row. Run as a **local pre-commit gate** — see `hooks` (replaced the CI workflow). |
| `node devtools/dev.mjs hooks <install\|uninstall\|status>` | **Local git hooks.** `install` points git at `.githooks/` (`core.hooksPath`); the committed `.githooks/pre-commit` runs `knowledge check` + `footprint` ONLY when a commit touches the rules system (`.claude/`, `CLAUDE.md`, the doctor scripts) — read-only, never bumps a version. This is the rules-system gate (there is no GitHub Actions workflow for it). |
| `devtools/downscale.mjs <png>` | Manual downscale fallback (captures are auto-downscaled — see screenshot-hygiene). |

## Ground rules (why the loop is prompt-free)
- Every dev/test step is one allow-listed `node devtools/*.mjs` call — fold any shell step into a script
  action; never an ad-hoc `dotnet build | grep` / `cp` / `ls` compound (those prompt).
- **Never prefix `cd`** — the Bash working dir is already the repo root; `cd …;` trips an
  untrusted-hooks prompt that overrides the allow-list. Run bare.
- Inspect code with Grep/Read/Glob, not Bash `grep`/`cat`. Captures live in `devtools/screenshots/`
  (git-ignored); clean up scratch.

## Extend it
When an action recurs or prompts, add it to the script that owns the concern (`app-dev.mjs` = lifecycle,
`drive-cdp.mjs` = drive/capture) or a new package, register it in `dev.mjs`, document it here. Keep it
zero-dep + prompt-free. Prune superseded scripts.
