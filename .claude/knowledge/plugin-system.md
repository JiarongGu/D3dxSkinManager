# Plugin system — generic dll plugins with typed capabilities (revived 2026-07-11)

`Modules/Plugin` is a GENERIC extension system: dll packs in **`{profile}/plugins/**`** are loaded
at profile startup, register `IPlugin` implementations, and expose TYPED CAPABILITY interfaces the
host consumes without knowing implementations. First real consumer: the content veil's AI pack
(`content-veil-ai`, now in the separate plugin repo — see `content-veil.md`).

## Repo split + contract layering (2026-07-12 — the current model)

Official packs live in their **OWN repo** (`github.com/JiarongGu/D3dxSkinManager.Plugins`), built +
released by THAT repo's workflow. **The app ships no plugin bytes and has no hard-coded pack list** —
it pulls the catalog live from the plugin repo's latest release. Two contract projects in the app sln:

- **`D3dxSkinManager.Core`** (`net10.0-windows`) — the RUNTIME contracts a plugin binds to: `IPlugin`,
  `IPluginContext`, `IImageReviewPlugin`, `IPluginProgress`, `IMessageDispatcher`, `IEventBus`, the
  IPC/event DTOs, and `PluginContract` (version + `IsCompatible` = major-version match). The host
  references Core; interface bodies live in Core, implementations stay in the host.
- **`D3dxSkinManager.Plugin.Sdk`** (references Core) — authoring helpers + `PluginSdk.ContractVersion`.
  This is what a plugin author references (a "fat" SDK — future plugins hook the mod-modification flow).
- **Vendoring:** `node devtools/dev.mjs plugin-sdk [targetRepoDir]` builds Core + Plugin.Sdk (Release)
  and copies the two dlls + `lib/README.md` into the plugin repo's **`lib/`** (tracked — small contract,
  no NuGet yet). Re-run whenever the contracts change so the plugin repo stays in sync.

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

- Project lives in the **PLUGIN repo** (`github.com/JiarongGu/D3dxSkinManager.Plugins`), NOT the app.
  Namespace `D3dxSkinManager.Plugins.<Name>`. References the vendored `lib/D3dxSkinManager.Core.dll`
  (+ `Plugin.Sdk.dll`) with **`<Private>false</Private>`** — the host provides those types at runtime;
  shipping a second copy = type-identity mismatch. Packages the HOST already embeds (ImageSharp) also
  `Private=false` (Costura resolves them).
- **`plugins.manifest.json` (plugin-repo root) = source of truth** for official packs (id / name /
  description / `version` / `asset` / `sdkContractVersion` / `project` / `dll` / `model{url,sha256,dest}`).
  Plugin versions are INDEPENDENT of the app version. **Built by the PLUGIN repo's release workflow**
  (carry-forward unchanged packs, else build fresh: fetch model → verify pinned sha256 → `dotnet build
  -c Release` → zip the single dll). Every release carries the pack zip PLUS a public
  `plugins-manifest.json` asset (id/name/description/version/asset/**sdkContractVersion**) so the app can
  show the available version + compatibility. **To ship a pack change: bump `version` in the manifest AND
  `IPlugin.Version` + csproj `<Version>` (keep in sync), then release the PLUGIN repo.** The `asset` name
  is the install contract the app resolves off the plugin repo's `/releases/latest` — never rename one
  side only.
- **SINGLE-DLL packs (preferred).** A pack that needs extra libs (a model, a native runtime)
  bundles them ALL inside the one plugin dll as `EmbeddedResource` — the install/CI ships a single
  file. The ContentVeil plugin is the reference: model + the MANAGED OnnxRuntime wrapper + the
  NATIVE onnxruntime dlls are embedded (`GeneratePathProperty` on the package refs + `ExcludeAssets`
  so nothing lands in build output). A `[ModuleInitializer]` (`PluginBootstrap`) installs an
  `AssemblyResolve` hook that serves the managed wrapper from the embedded resource; `InitAsync`
  extracts + `NativeLibrary.TryLoad`s the natives into the plugin data dir before first use
  (DllImport probing doesn't cover the LoadFrom assembly dir). Verified: one 24MB dll loads +
  detects with nothing else beside it.
- **One folder per pack (2026-07-12).** `IPluginContext.GetPluginDataPath(id)` returns the folder the
  plugin's OWN DLL was loaded from (its install dir) — so the extracted natives land NEXT TO the dll,
  not in a separate `{plugins}/{pluginId}` dir. Before, install used the PACK id (`content-veil-ai`)
  and the data dir used the PLUGIN id (`d3dx.content-veil-ai`) → two folders per pack. The getter also
  retires the legacy per-id dir once (regenerable natives). No fallback — packs always load from disk.
- **Missing/unloadable native = LOUD fail (plugin side).** `PluginBootstrap.EnsureNativeLibraries`
  THROWS a descriptive reason (native absent from the package, or the OS can't load it — e.g. missing
  VC++ redist) instead of ignoring `TryLoad`. `ContentVeilAiPlugin.InitAsync` catches it, logs a clear
  WARN via `context.Log`, and sets `_nativeReady=false` so `ReviewImageAsync` ABSTAINS (null → host
  falls back to the CV heuristic) rather than crashing once per image.
- Implement `IPlugin` or a capability interface extending it (`Modules/Plugin/Interfaces/`).
  Capability example: **`IImageReviewPlugin`** — an INTERCEPTOR on the content-veil flow: host
  runs its own analysis, then hands `ImageReviewContext(path, currentVerdict, focusRegions)` to
  each reviewer. **Contract v2 (`PluginContract.Version` = "2.0"): a reviewer returns a bool
  VERDICT** (`true`=sensitive / `false`=safe / `null`=abstain) and OWNS its own threshold — the
  host holds no cutoff; any SENSITIVE verdict wins, null = abstain (host verdict stands). Improve a
  detector by retraining/rethresholding the PLUGIN, not by tuning the host. Fractional `ImageRegion`s
  keep coordinates decode-independent. (v1 returned a `double?` confidence the host thresholded;
  bumping the interface signature = a MAJOR contract bump → old packs gated out until rebuilt.)
- **Long-running work → `IPluginContext.ReportProgress(title)`.** Returns an `IPluginProgress`
  handle (Report/Complete/Fail + `.Token`; `using` auto-completes) that shows in the status bar +
  Activity panel like any host op — the host owns the ProcessRegistry entry so plugins never touch
  it or the `ProcessType` enum.

## Lifecycle (PluginFacade, module PLUGIN)

- `GET_ALL` → `PluginInfo[]` (id/name/version/description/author/isEnabled/capabilities —
  capabilities include typed interfaces, e.g. "ImageReview"). `GET_DIRECTORY` → plugins dir, ENSURED
  to exist (open-folder never fails). Frontend opens it via `systemService.openDirectory` — NOT the
  file opener (`openFileInExplorer` validates `File.Exists`, false for a dir → "File not found").
- `ENABLE`/`DISABLE {pluginId}` — INSTANT (registry `PluginEntry.Enabled`; consumers only see
  enabled plugins) and persisted per profile (`PluginStateStore`, {profile}/plugins/plugins.json).
  Enabling a never-initialized plugin runs `InitAsync` then. Disable does NOT dispose.
- `GET_AVAILABLE_PACKS` → `PluginPackInfo[]` (id/name/description/version/asset/sdkContractVersion/
  **compatible**/**installed**) — the DYNAMIC catalog (no hard-coded list). `PluginInstallService`
  fetches the PLUGIN repo's `/releases/latest` ONCE (asset map + the `plugins-manifest.json` URL), then
  the manifest; each pack gets `Compatible = PluginContract.IsCompatible(sdkContractVersion)` (major
  match) + `Installed` (registry has `d3dx.{id}`). Network-tolerant — `[]` on any failure. Frontend
  (`PluginSettingsTab`) renders the available list from THIS (was a hard-coded array); incompatible
  packs show a "Requires newer app" tag + disabled install.
- `DOWNLOAD_PACK {packId}` — resolves the pack's `asset` from the plugin repo's latest-release manifest
  (throws `PLUGIN_PACK_UNKNOWN` / `PLUGIN_PACK_INCOMPATIBLE` / `PLUGIN_PACK_NOT_AVAILABLE`); the asset
  URL must start with the plugin repo's releases prefix — same trust model as the updater. Fire-and-forget
  download (ProcessRegistry,
  `process.pluginDownload`) → extract → load. **Fresh install** extracts into `{profile}/plugins/{packId}/`
  and live-loads (no restart). **UPDATE** (pack already installed → its dll is LOADED + locked) can't
  overwrite in place, so it extracts into `{profile}/plugins/.pending/{packId}/` and
  `PluginLoader.ApplyPendingUpdates` (start of `LoadPluginsAsync`, before the dll scan, `.pending`
  excluded from the scan) swaps it into place on the next load → **applies on RESTART**. UI: Settings →
  插件 shows an **Update** button on installed official-pack rows (`downloadPack(packId)`, packId =
  pluginId minus the `d3dx.` prefix) + a "restart to apply" toast. To publish a pack change, bump the
  version in BOTH `plugins.manifest.json` AND `IPlugin.Version` + csproj `<Version>` (see below) so the
  release rebuilds the zip — done for content-veil-ai 1.0→1.1 (the loud-native-fail change).
  (REMOVING a loaded pack still needs a restart — assemblies can't unload.)
- `CHECK_UPDATES` → `PluginUpdateInfo[]` (pluginId / packId / installedVersion / availableVersion /
  updateAvailable) for each INSTALLED official pack: fetches the PLUGIN repo's latest-release public
  `plugins-manifest.json` asset (trusted only under that repo's releases prefix), maps installed pluginId →
  packId (drop the `d3dx.` prefix) + Catalog-gates to official packs, compares versions (numeric
  `Version`, string-diff fallback). Network-tolerant — returns `[]` on any failure (offline / no
  release / no manifest asset), so the UI just shows no badges. Frontend runs it as a BACKGROUND,
  non-blocking check on the 插件 tab (the plugin list never waits on GitHub); a **"vX available"**
  badge + the **Update** button appear ONLY when `updateAvailable`.
- Frontend: **Settings → 插件** (`PluginSettingsTab` + `pluginService`): installed list + enable
  switches + plugins-folder opener + a DYNAMIC "available" section (`getAvailablePacks` → every
  uninstalled pack from the plugin repo manifest, incompatible ones disabled). Both auto-refresh when
  a download process completes (processStore watcher).

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
