# `devtools/` — D3dxSkinManager dev + test toolkit

This folder **is** the develop-and-test process for the real desktop app. Driving / building /
capturing is done through committed, **allow-listed** `node devtools/dev.mjs …` commands so the loop
runs **prompt-free + unattended**. Adapted from the SiblingApp toolkit (`SiblingApp`) —
same stack (WebView2 + WinForms + React + .NET 10).

Strategy + how CDP is wired: [`../.claude/rules/desktop-app-testing.md`](../.claude/rules/desktop-app-testing.md) ·
conventions: [`../.claude/rules/scripts-live-in-repo.md`](../.claude/rules/scripts-live-in-repo.md) ·
capture limits: [`../.claude/rules/screenshot-hygiene.md`](../.claude/rules/screenshot-hygiene.md).

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

## The tools

| Command | What |
|---|---|
| `node devtools/dev.mjs app <kill\|build\|tsc\|test\|start\|restart\|rebuild\|wait> [port]` | **App lifecycle + checks.** `rebuild` = kill → `dotnet build` (errors only) → relaunch (dev mode + CDP) → wait. `build` = backend build; `tsc` = frontend typecheck; `test [path]` = vitest in the client dir (no `cd`). `kill` is **path-matched to the repo bin exe** — a user-installed copy running from another folder is never touched. |
| `node devtools/dev.mjs cdp <open\|nav\|menu\|probe\|key\|eval\|evalfile\|reload\|ipc\|events\|iplog\|shot\|grab\|wait> [arg] [port]` | **Drive + capture over CDP.** `open` clicks `playSelector`; `nav "<Tab>"`; `menu "<Item>"` (right-click 1st card → item); `eval "<js>"`; `reload [ms]`; `ipc <MOD> <TYPE> '<json>'` + `events`/`iplog` (via `window.__d3dx`); `shot [label]` = DOM screenshot; `wait <ms>`. |
| `node devtools/dev.mjs shot [label]` | **Occlusion-immune** window capture (Windows.Graphics.Capture; works while the console covers the app). Builds `wgc-shot/` on first use. |
| `node devtools/dev.mjs input <click\|rclick\|move\|drag> <x> <y> [x2 y2]` | **Native mouse input** (Win32 `PostMessage` to the leaf HWND; `x,y` = fractions 0..1 of the client area). Builds `win-input/` on first use. Pair with `shot`. |
| `node devtools/dev.mjs check [port]` | Health verdict over CDP (CSS loaded? React mounted? console errors?) + a DOM screenshot. |
| `node devtools/dev.mjs review [port]` | Sweep every tab in `project.config.mjs` `reviewTabs` + per-tab screenshots (regression). |
| `node devtools/dev.mjs research <search\|scrape> …` | **Web research** (puppeteer + stealth, self-contained `research/`). Auto-installs Chromium (~280MB) on first use. |
| `node devtools/dev.mjs crop <png> <x> <y> <w> <h>` | Crop a capture. |
| `node devtools/dev.mjs manifest <dir> <version> [outFile]` | Generate the auto-update `manifest.json` (path/size/sha256 per file; excludes the launcher). Used by `release.yml`. See `docs/LAUNCHER_ARCHITECTURE.md`. |
| `node devtools/dev.mjs test-update-apply` | End-to-end test of the launcher's APPLY phase: sandbox install + staged update → runs the real launcher `--apply-and-exit` → asserts overlay/add/remove/cleanup. |
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
