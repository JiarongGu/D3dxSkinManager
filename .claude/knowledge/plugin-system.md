# Plugin system — generic dll plugins with typed capabilities (revived 2026-07-11)

`Modules/Plugin` is a GENERIC extension system: dll packs in **`{profile}/plugins/**`** are loaded
at profile startup, register `IPlugin` implementations, and expose TYPED CAPABILITY interfaces the
host consumes without knowing implementations. First real consumer: the content veil's AI pack
(`plugins/D3dxSkinManager.Plugins.ContentVeil`, see `content-veil.md`).

## Container topology (the part that bit us — a DI crash on startup)

The app has a GLOBAL container (Core/Setting/System/Profile) + PER-PROFILE containers built by
`ProfileServiceRouter.CreateProfileServices` (each module facade via `MapFacade`). The plugin
pieces split across them:

- **`IPluginRegistry` is GLOBAL** (`ApplicationHost.ConfigureServices` → `AddPluginRegistry()`),
  and every profile container RE-SHARES the same instance (registered in `CreateProfileServices`
  BEFORE module configs so `AddPluginsServices`' TryAdd keeps it). This is the seam that lets a
  GLOBAL service (ContentVeilService) consume plugins loaded by a PROFILE's loader. Injecting
  profile-scoped services into global ones crashes DI resolution — share via the registry instead.
- **Loader/context/state/install/facade are PROFILE-scoped** (`AddPluginsServices` inside the
  MapFacade lambda; plugins live under the profile).
- **Startup**: `CreateProfileServices` resolves `IProfileServerService.StartAsync()` after
  migrations — THAT is what loads+inits plugins (it was never called before 2026-07-11; the whole
  system was dormant). Isolated + non-fatal: a broken plugin never blocks a profile.

## Writing a plugin

- Project under **`plugins/`** (own solution `plugins/D3dxSkinManager.Plugins.slnx` — NOT in the
  app sln). Namespace `D3dxSkinManager.Plugins.<Name>`.
- **`plugins/plugins.manifest.json` = source of truth** for official packs (id / name / description /
  `version` / `asset` / `project` / `dll` / `model{url,sha256,dest}`). Plugin versions are INDEPENDENT
  of the app version. **Built by the MAIN release workflow** (`.github/workflows/release.yml`, folded in
  2026-07-11 — the separate `plugins.yml` is GONE): per pack, if `version` matches the previous
  published release's manifest it CARRIES the already-built zip forward (re-download, no rebuild),
  else it BUILDS fresh (fetch model → verify pinned sha256 → `dotnet build -c Release` → zip the single
  dll). Every release carries the pack zip (the in-app download resolves the fixed `asset` name off
  `/releases/latest`) PLUS a public `plugins-manifest.json` asset (id/name/description/version/asset) so
  the app can show the available version. **To ship a pack change: bump `version` in the manifest AND
  the plugin's `IPlugin.Version` + csproj `<Version>` (keep them in sync), then run a release.** The
  `asset` name is the install contract with `PluginInstallService.Catalog` — never rename one side only.
- csproj: `ProjectReference` to the main project with `Private=false` (host types come from the
  already-loaded exe); packages the HOST already embeds (ImageSharp) also `Private=false` —
  Costura resolves them.
- **SINGLE-DLL packs (preferred).** A pack that needs extra libs (a model, a native runtime)
  bundles them ALL inside the one plugin dll as `EmbeddedResource` — the install/CI ships a single
  file. The ContentVeil plugin is the reference: model + the MANAGED OnnxRuntime wrapper + the
  NATIVE onnxruntime dlls are embedded (`GeneratePathProperty` on the package refs + `ExcludeAssets`
  so nothing lands in build output). A `[ModuleInitializer]` (`PluginBootstrap`) installs an
  `AssemblyResolve` hook that serves the managed wrapper from the embedded resource; `InitAsync`
  extracts + `NativeLibrary.TryLoad`s the natives into the plugin data dir before first use
  (DllImport probing doesn't cover the LoadFrom assembly dir). Verified: one 24MB dll loads +
  detects with nothing else beside it.
- Implement `IPlugin` or a capability interface extending it (`Modules/Plugin/Interfaces/`).
  Capability example: **`IImageReviewPlugin`** — an INTERCEPTOR on the content-veil flow: host
  runs its own analysis, then hands `ImageReviewContext(path, currentVerdict, focusRegions)` to
  each reviewer; strongest confidence wins, null = abstain (host verdict stands). Fractional
  `ImageRegion`s keep coordinates decode-independent.
- **Long-running work → `IPluginContext.ReportProgress(title)`.** Returns an `IPluginProgress`
  handle (Report/Complete/Fail + `.Token`; `using` auto-completes) that shows in the status bar +
  Activity panel like any host op — the host owns the ProcessRegistry entry so plugins never touch
  it or the `ProcessType` enum.

## Lifecycle (PluginFacade, module PLUGIN)

- `GET_ALL` → `PluginInfo[]` (id/name/version/description/author/isEnabled/capabilities —
  capabilities include typed interfaces, e.g. "ImageReview"). `GET_DIRECTORY` → plugins dir.
- `ENABLE`/`DISABLE {pluginId}` — INSTANT (registry `PluginEntry.Enabled`; consumers only see
  enabled plugins) and persisted per profile (`PluginStateStore`, {profile}/plugins/plugins.json).
  Enabling a never-initialized plugin runs `InitAsync` then. Disable does NOT dispose.
- `DOWNLOAD_PACK {packId}` — official packs only (`PluginInstallService` catalog → release asset
  name): resolves the LATEST GitHub release asset (URL must start with the official releases
  prefix — same trust model as the updater), fire-and-forget download (ProcessRegistry,
  `process.pluginDownload`) → extract into `{profile}/plugins/{packId}/` → **live load+init** (no
  restart for installs; REMOVING a loaded pack needs a restart — assemblies can't unload).
- Frontend: **Settings → 插件** (`PluginSettingsTab` + `pluginService`): list + enable switches +
  plugins-folder opener + an AI-pack download row shown when no enabled ImageReview plugin exists;
  the list auto-refreshes when a download process completes (processStore watcher).

## Content-veil consumer speed (the interceptor hot path)

`ContentVeilService.AnalyzeFileAsync` checks once whether ANY `IImageReviewPlugin` is registered:
- **plugin present** → run only CV stages 1-2 (`Analyze(regionsOnly:true)` → `FillMetrics` stops
  after skin-region labeling) to supply focus regions; the plugin decides the verdict. The
  expensive CV point/zoom stages are skipped entirely.
- **no plugin** → full CV pipeline (the ~85%-accuracy fallback).
Batch analysis runs in parallel (SemaphoreSlim ≤ cores-1, capped 8). The frontend hook
(`useContentVeil`) streams verdicts in CHUNK_SIZE=6 groups, ≤3 concurrent, re-rendering per chunk
so cards un-veil in waves. Plugin inference tuning (the ONNX floor): IntraOp≈cores/4, planar
float[] tensor fill (not the DenseTensor 4D indexer), flat output scan, decode-once + in-memory
region crops. Bench: `node devtools/dev.mjs veil bench [n]` over the real remote-image cache
(2026-07-11: 64ms/image cold on 22 cores, was 93ms).

## Legacy note

The 14 old `plugins/<Name>` Python-port stubs (against the removed `IMessageHandlerPlugin`) were
DELETED 2026-07-11 — they never compiled and had no value (`git log` has them). The ContentVeil
plugin is the reference implementation.
