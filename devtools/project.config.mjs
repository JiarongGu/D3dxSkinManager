// project.config.mjs — the ONLY project-specific inputs for the devtools toolkit.
//
// The tools under scripts/ are otherwise generic (lifecycle, CDP drive/capture, window capture, web
// research). To reuse this toolkit on another desktop app, copy devtools/ and edit THIS file — paths,
// ports, the CDP page match, the dev-interceptor global, and the "open"/review selectors.
//
// Adapted from a sibling devtools toolkit. Both apps are
// WebView2 + WinForms + React + .NET 10, so the harness ports directly.

export default {
  name: 'D3dxSkinManager',
  /** Short slug used to prefix capture filenames (devtools/screenshots/<slug>-<ts>-<label>.png). */
  shotPrefix: 'd3dx',
  /** Process/window name for capture (shot --proc) and taskkill (app kill). */
  processName: 'D3dxSkinManager',
  /** Repo-relative path to the built exe (app-dev launches + kills this; locks the DLL during build). */
  exe: 'D3dxSkinManager/bin/Debug/net10.0-windows/D3dxSkinManager.exe',
  /** Backend project to build. */
  csproj: 'D3dxSkinManager/D3dxSkinManager.csproj',
  /** Where native bitmap dumps live, under <binDir> then data/logs (drive-cdp grab searches here).
   *  This app has no native overlay/chrome dumps today; `grab` stays available for future use. */
  binDir: 'D3dxSkinManager/bin',
  /** Frontend dir (cwd for tsc / vitest). */
  clientDir: 'D3dxSkinManager.Client',
  /** FALLBACK CDP remote-debugging port. The toolkit normally RANDOMIZES the CDP port per launch
   *  (app-dev picks one, persists it to devtools/.cdp-port; check/cdp/review read that). This value is
   *  only used when no port was persisted and none passed explicitly. */
  cdpPort: 9319,
  /** CDP page URL substring identifying the app page (the Vite dev server) — see vite.config.ts. */
  viteUrlMatch: '3517',
  /** The dev-only IPC/event interceptor global (shared/services/devInterceptor.ts, DEV builds only).
   *  drive-cdp `ipc`/`events`/`iplog` call window[devGlobal]. */
  devGlobal: '__d3dx',
  /** drive-cdp `open` clicks this element + a few ancestors. TUNE for this app's primary open action
   *  (a mod card / load button). The slide-in + context-menu selectors in drive-cdp already match this app. */
  playSelector: '.mod-card, [class*="mod-card"]',
  /** review tabs: left-nav labels the regression sweep visits. TUNE to this app's actual tab labels. */
  reviewTabs: ['Mods', 'Tools', 'Settings'],
  /** `check` health probe — a CSS selector that only matches once the app's REAL shell has rendered
   *  (a status bar, nav, primary list). check-desktop reports the match count as `contentReady` and
   *  folds "> 0" into the `healthy` verdict. Set '' to skip. TUNE per app. */
  healthProbe: '[class*="app-status-bar"], .category-card, .mod-list-item, .mod-list-panel-content',
  /** app-dev debug flags → the env var each sets on the launched app. None defined for this app yet. */
  debugFlags: {},
  /** Env to launch in dev mode (loads the live Vite server, not the embedded prod bundle). */
  devEnv: { ASPNETCORE_ENVIRONMENT: 'Development' },
};
