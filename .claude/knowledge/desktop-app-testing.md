# Testing & developing the REAL desktop app (CDP + screen capture + native input + research)

The app is **WebView2 + WinForms + React + .NET 10**. To verify anything backend-real — IPC
round-trips, WebView2 behaviour, real profile/mod data, file operations — you must drive the **actual
desktop app**, not a web preview. The develop+test process lives in `devtools/` as allow-listed
`node devtools/dev.mjs …` commands so the loop runs **prompt-free + unattended**.

> **Command reference: [`devtools/README.md`](../../devtools/README.md).** This doc is the strategy.
> The toolkit was adapted from a sibling project — same stack.

## How CDP is enabled here (one-time, already wired)

WebView2 speaks the Chrome DevTools Protocol when launched with `--remote-debugging-port`. This app
sets its own `CoreWebView2EnvironmentOptions.AdditionalBrowserArguments`, which makes WebView2 **ignore
the `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` env var**. So `WebViewInitializer.InitAsync` appends that
env var to its own args **in dev mode only** (`IsDevelopmentMode()`). `devtools/scripts/app-dev.mjs`
sets the env to `--remote-debugging-port=<port>` when it launches the app → CDP attaches. Never in prod.

Dev mode = `ASPNETCORE_ENVIRONMENT=Development` (loads the live Vite server instead of the embedded prod
bundle). `app-dev.mjs start` sets it. A bare exe runs production (old embedded UI).

## Ports (unique — chosen to avoid collisions)

- **Vite dev server: `3517`** (fixed; `strictPort: true`). NOT the common 3000. It's hardcoded into the
  backend's dev navigation (`WebViewInitializer.NavigateToDevelopment` + `SecondaryWindowService`), so
  it's a fixed constant — change it in **three** synced places: `vite.config.ts`, those two C# files,
  and `project.config.mjs` `viteUrlMatch`. Run **this** project's `D3dxSkinManager.Client` dev server on
  3517 before testing (`check` prints the page title/URL — confirm it's this app).
- **CDP remote-debugging port: RANDOMIZED per launch** (9300–9999). `app-dev` picks a port each
  start/restart/rebuild, launches the app with it, and persists it to `devtools/.cdp-port` (git-ignored);
  `check`/`cdp`/`review` read that file, so no number needs passing. An explicit numeric arg overrides;
  `project.config.mjs` `cdpPort` is the last-resort fallback. This avoids colliding with any other
  Chromium/WebView2 debug instance. (`_cdp-port.mjs` owns this.)

## The canonical prompt-free loop

```
node devtools/dev.mjs app rebuild           # backend change → kill + dotnet build (errors only) + relaunch (random CDP port) + wait
node devtools/dev.mjs cdp wait 2000
node devtools/dev.mjs check                 # health verdict over CDP (CSS/React/console)
node devtools/dev.mjs shot mods             # NATIVE WGC capture → devtools/screenshots/d3dx-<ts>-mods.png (then Read it)
node devtools/dev.mjs input click 0.1 0.2   # NATIVE click (real Win32 input) — then `shot` again to see the result
```
Frontend-only change → skip `rebuild`; Vite serves it live — `node devtools/dev.mjs cdp reload` is enough.

> **Every native tool targets the DEV instance only, path-matched to the repo bin exe** — the user
> often runs their own INSTALLED copy of the app at the same time. `app kill`/`rebuild` kill by
> path-matched PID (`app-dev.mjs devPids()`); `shot` and `input` resolve the dev instance's HWND via
> `scripts/_dev-window.mjs` (a bare process-NAME match captured the user's window / killed their
> instance — both happened 2026-07-05). CDP tools are inherently safe (the debug port only exists on
> the dev instance).

## Driving + capturing — prefer NATIVE over CDP for real interactions

**CDP synthetic events (`Runtime.evaluate` `.click()` / `dispatchEvent`) are NOT real input.** They
fire DOM-level events but do NOT exercise the Win32 input path or the browser's real
pointer/drag-drop machinery. **Drag-drop is not a CDP activity** — a `cdp eval` "drag" can't reorder a
category, drag a mod between panels, or accept a dropped file. For anything a user does with the mouse,
use the **native** tools.

**Default to native:**
- `node devtools/dev.mjs input <click|rclick|move|drag> <x> <y> [x2 y2]` — **native mouse input** via
  Win32 `PostMessage` to the leaf child HWND (`x,y` = fractions 0..1 of the client area; `drag` takes a
  second point). Real pointer-down/move/up at the OS level → exercises the app's actual input handling.
  Builds `devtools/win-input/` (C#) on first use.
- `node devtools/dev.mjs shot [label]` — **occlusion-immune WGC window capture**: the window's own
  composited frame regardless of z-order (works even while the agent console covers it). This is the
  capture to trust for "what the window actually shows." Builds `devtools/wgc-shot/` (C#) on first use.
- **Verify loop for an interaction:** `shot before` → `input drag x1 y1 x2 y2` → `shot after`, then Read
  both PNGs and compare. Get target coordinates from a prior `shot` (fractions of the window).

**CDP is for STATE + IPC inspection, not for driving real interactions:**
- `cdp eval "<jsExpr>"` / `cdp probe` — read DOM/values, overlay state (cheap text verification).
- `cdp ipc|events|iplog` — drive/observe IPC via `window.__d3dx` (bypass native dialogs; see below).
- `cdp shot` — DOM-only screenshot (fine for quick pure-DOM checks; prefer native `shot` for truth).
- `cdp open|nav|menu|key` — convenience DOM clicks for *navigation* only; do NOT rely on them for
  drag-drop or anything where real input matters.

> **File drag-drop caveat:** dropping a file from Explorer onto the window is OLE drag-drop
> (`IDropTarget`/`WM_DROPFILES`), which neither CDP nor synthetic mouse fully simulates. To exercise
> mod-import-by-drop logic, drive the underlying IPC directly: `cdp ipc <MOD/WORKFLOW> <TYPE> '<json>'`.
> Use native `input drag` for IN-window mouse drags (reorder/move); use IPC for file drops.

> This app has **no native video overlay or layered chrome** (unlike a sibling app), so there are no
> `bin/**/data/logs/*.png` dumps — `cdp grab` exists but produces nothing here unless such dumps are added.

## Dev IPC + event interceptor — drive IPC directly, bypass native dialogs, observe events

A **dev-only** interceptor (`shared/services/devInterceptor.ts`, installed in `index.tsx` behind
`import.meta.env.DEV`, stripped from prod) wraps `bridgeService.sendMessage` + `eventBus.emit` and
exposes **`window.__d3dx`**. Drive backend flows the UI gates behind a native dialog (folder picker)
or that are event-driven:
- `node devtools/dev.mjs cdp ipc <MODULE> <TYPE> '<json>'` — invoke ANY IPC via `window.__d3dx.call`.
  e.g. `cdp ipc MOD LOAD "{\"id\":\"<modId>\"}"`. Pass payload as a JSON string (parsed in-page so
  Windows backslash paths survive).
- `cdp events [n]` / `cdp iplog [n]` — read the last n intercepted events / IPC calls (payload, result,
  ms, ok) from the ring buffers — verify a flow actually fired.
- In a `cdp eval` you can also use `window.__d3dx.waitEvent('MOD','MOD_LIST_UPDATED')` (awaitPromise).

## Web research (puppeteer + stealth)

`node devtools/dev.mjs research search "<q>"` / `research scrape <url> [--selector|--json]` — JS-rendered
pages WebFetch can't get (anti-bot / SPA). Self-contained `devtools/research/`; auto-installs Chromium
(~280MB) on first use. Ground decisions in sources, not guesses.

## Pure-UI testing in plain Chrome (no desktop app, no backend)

The frontend is pure React served by Vite, so for **component / layout / styling / i18n** work you can
skip the desktop app entirely: run the dev server and open `http://localhost:3517` in a normal Chrome
tab via the `claude-in-chrome` MCP tools (`tabs_context_mcp` → `tabs_create_mcp` → `navigate` →
`javascript_tool` / `computer` screenshot). No WebView2 bridge there, so IPC is inert — drive the UI
with the **DEV-only `window` affordances** instead of the backend:
- `window.__processStore.getState().setProcesses([...])` — populate the Activity panel / status bar
  with mock processes (exposed by `processBridge` under `import.meta.env.DEV`).
- `window.__d3dx` — the IPC/event interceptor (`call`, `recentIpc`, `recentEvents`).

Add a `window.__<store>` DEV exposure for any store you want to drive this way. This is the fast loop
for visual design; it does NOT exercise IPC/native — use the desktop tools below for those.

**How it works / caveats:**
- `bridgeService` has a DEV fake-bridge: with no WebView2 it resolves IPC with canned bootstrap data
  (settings + a fake profile + empty lists) so the shell renders instead of the "must run in desktop
  mode" gate. Components that assume preloaded array data can still throw on the empty mocks — the
  top-level `ErrorBoundary` shows the error + component stack (read it via `read_console_messages` for
  `[ErrorBoundary]`, or screenshot), so fixing those is fast. Guard such components with `Array.isArray`.
- **i18n shows raw keys** in pure-UI mode (the language file isn't loaded over the inert bridge) — fine
  for layout/styling/structure verification; use the desktop app to verify translated copy.
- Flow that works today: `tabs_context_mcp` → `tabs_create_mcp` → `navigate` 3517 → wait → inject via
  `window.__processStore` → click `.app-status-bar-task-area` → `screenshot`.

## The game is NOT required for most testing
Mod **management** + 3DMigoto **file** work — load/unload, fix, keybind rebind, deploy into the Mods
folder, package import/export, analysis, merge — are DB + filesystem operations on the importer's
`Mods/` folder. They are **fully e2e-testable without launching the game**, and since the game isn't
running, loading/deploying/modifying mods for a test is **non-destructive** (no live injection to
disrupt). So drive these freely: `cdp ipc MOD LOAD …`, rebind, fix, then verify via `GET_*` round-trips
+ native `input`/`shot`. Many flows gate UI on `mod.hasCache` — **load the mod first** (user's rule:
"load the mod, then you can see the keybindings"). Reserve "needs the game" ONLY for actual in-game
injection / rendering / on-screen-toggle behaviour — the user verifies that separately.

## When to use what
- Pure component / CSS / layout / i18n → **plain Chrome + Vite (3517) + `window.__*` mocks** (above). Fastest.
- Anything touching IPC / WebView2 / real profile+mod data / file operations → the real app via the tools above.
- After a **backend** change: `app rebuild` (kill first — the running exe locks the DLL). After a
  **frontend** change: `cdp reload` (Vite HMR). Editing files while verifying can HMR-corrupt a session —
  reload fresh before judging.

See [scripts-live-in-repo.md](../rules/scripts-live-in-repo.md) (why the loop is prompt-free) and
[screenshot-hygiene.md](screenshot-hygiene.md) (keep captures agent-readable).
