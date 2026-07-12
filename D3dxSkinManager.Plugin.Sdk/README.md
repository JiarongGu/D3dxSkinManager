# D3dxSkinManager Plugin SDK

Write plugins for **D3dxSkinManager** — a generic DLL plugin system. A plugin is a single .NET assembly
dropped into `{profile}/plugins/**` (or installed in-app). At profile startup the host loads it, calls
`InitAsync`, and exposes it through typed **capability interfaces** the app consumes without knowing your
implementation.

> The reference plugin is **Content Veil AI** (an `IImageReviewPlugin` that runs an ONNX censor detector).

---

## 1. Layering (what you reference)

```
D3dxSkinManager.Core        ← shared CONTRACTS + host abstractions (IPlugin, IPluginContext, IEventBus,
   ▲            ▲              IMessageDispatcher, IpcRequest/Response, EventMessage, LogLevel, capability
   │            │              interfaces). The HOST implements these.
   │            │
Host app     D3dxSkinManager.Plugin.Sdk   ← what PLUGINS reference (re-exposes Core; authoring helpers).
                ▲
             Your plugin
```

- The **host** references `Core` and implements the interfaces.
- **You** reference **`D3dxSkinManager.Plugin.Sdk`** (which pulls in `Core`). The host never references a plugin.
- Today the SDK is distributed as **tracked DLLs** in the plugin repo's `lib/`. (A NuGet package is the planned
  next step.)

### Referencing it — `Private=false` is mandatory

```xml
<ItemGroup>
  <!-- The host already has Core loaded at runtime; do NOT ship your own copy or you'll get a
       type-identity mismatch (your IPlugin != the host's IPlugin) and the host won't see your plugin. -->
  <Reference Include="D3dxSkinManager.Core">
    <HintPath>lib\D3dxSkinManager.Core.dll</HintPath>
    <Private>false</Private>
  </Reference>
  <Reference Include="D3dxSkinManager.Plugin.Sdk">
    <HintPath>lib\D3dxSkinManager.Plugin.Sdk.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

Target `net10.0-windows`. Enable `<EnableDynamicLoading>true</EnableDynamicLoading>`.

---

## 2. Minimal plugin

```csharp
using D3dxSkinManager.Modules.Core.Helpers;   // LogLevel
using D3dxSkinManager.Modules.Core.Models;    // IpcRequest / IpcResponse
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Services; // IPluginContext

public sealed class HelloPlugin : IPlugin
{
    private IPluginContext? _ctx;

    public string Id => "com.example.hello";   // globally unique, reverse-DNS
    public string Name => "Hello";
    public string Version => "1.0";            // keep in sync with the manifest + csproj <Version>
    public string Description => "A minimal example plugin";
    public string Author => "You";

    public Task InitAsync(IPluginContext context)
    {
        _ctx = context;
        _ctx.Log(LogLevel.Info, $"[{Name}] initialized; data dir = {_ctx.GetPluginDataPath(Id)}");
        return Task.CompletedTask;
    }

    // Return the frontend message types you handle (empty if none).
    public IEnumerable<string> GetHandledMessageTypes() => Array.Empty<string>();

    public Task<IpcResponse> HandleMessageAsync(IpcRequest request) =>
        Task.FromResult(IpcResponse.CreateError(request.Id, $"Unhandled: {request.Type}"));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

`IPlugin` members: `Id`, `Name`, `Version`, `Description`, `Author`, `InitAsync(IPluginContext)`,
`GetHandledMessageTypes()`, `HandleMessageAsync(IpcRequest)`, `DisposeAsync()` (from `IAsyncDisposable`).

---

## 3. `IPluginContext` — what the host gives you

| Member | Use |
|---|---|
| `GetPluginDataPath(pluginId)` | The folder your DLL was loaded from — put extracted files (models, natives) here, next to the dll. |
| `Log(LogLevel, message, exception?)` | Write to the app log. |
| `ReportProgress(title, cancellable?)` | Track long work in the status bar / Activity panel (see §5). |
| `EventBus` | Subscribe to / emit host events (`EventMessage`). |
| `MessageDispatcher` | Send IPC into a module facade (`SendAsync(module, type, profileId?, payload?)`). |
| `RegisterEventHandler(modulePattern, typePattern, handler)` / `UnregisterEventHandler(id)` | Convenience event subscription tied to your plugin's lifetime. |

Example — react to mods loading:

```csharp
_ctx.RegisterEventHandler("MOD", "LOADED", async (EventMessage e) =>
{
    _ctx.Log(LogLevel.Info, $"mod loaded: {e.Payload}");
    await Task.CompletedTask;
});
```

---

## 4. Capability interfaces (the real power)

Implement a capability interface (extends `IPlugin`) and the host discovers you via
`IPluginRegistry.GetPlugins<T>()`. The one shipped today:

### `IImageReviewPlugin` — content-veil interceptor

```csharp
public interface IImageReviewPlugin : IPlugin
{
    // 0..1 sensitivity confidence (1 = certainly explicit), or NULL to ABSTAIN (host verdict stands).
    // Called CONCURRENTLY — be thread-safe.
    Task<double?> ReviewImageAsync(ImageReviewContext context, CancellationToken ct = default);
}
```

`ImageReviewContext(Path, CurrentVerdict, FocusRegions)` — `FocusRegions` are fractional `ImageRegion`s
(0–1 of width/height, decode-independent) the host's own analysis flagged for closer inspection. Return the
strongest confidence; abstain (`null`) on unreadable/unsupported input.

> More capability interfaces (e.g. mod-modification hooks) are planned — they live in `Core` so both host
> and plugins share them.

---

## 5. Long-running work → progress, not the ProcessRegistry

```csharp
using var progress = _ctx.ReportProgress("Analyzing images", cancellable: true);
for (var i = 0; i < total; i++)
{
    progress.Token.ThrowIfCancellationRequested();
    // ... work ...
    progress.Report(percent: i * 100 / total, detail: $"{i}/{total}");
}
// disposing the `using` auto-Completes; or call progress.Complete()/Fail(msg).
```

The host owns the status-bar entry — you never touch `ProcessRegistry` or `ProcessType`.

---

## 6. Packaging — single-DLL packs

A pack ships as **one DLL**. Bundle everything your plugin needs (models, native runtimes) as
`EmbeddedResource` inside it; extract them to `GetPluginDataPath(Id)` on first use.

- **Everything the host already loads** (the SDK, ImageSharp, …) → `Private=false` + `ExcludeAssets=runtime`.
  Resolve them from the host at runtime; never ship a second copy.
- **Native DLLs**: embed, extract to the data dir on `InitAsync`, then `NativeLibrary.TryLoad`. Fail LOUD
  (throw/log + abstain) if a native can't load — don't crash per-call.
- Use a `[ModuleInitializer]` + `AssemblyResolve` hook if you embed a managed dependency dll.

See the Content Veil plugin for the full embed-model-and-natives pattern.

---

## 7. Releasing — the manifest

Official packs are listed in `plugins.manifest.json` (in the plugin repo). Per pack: `id`, `name`,
`description`, `version`, `asset` (the release zip name — the install contract), `project`, `dll`, optional
`model{url,sha256,dest}`.

**To ship a change, bump the version in THREE places (keep them in sync):**
1. `plugins.manifest.json` → `version`
2. your `IPlugin.Version`
3. csproj `<Version>`

The release workflow builds the pack only when its `version` changed (else carries the last zip forward) and
attaches a public `plugins-manifest.json` the app reads to show available versions + offer updates. Plugin
versions are independent of the app version.

**Compatibility:** the SDK exposes `PluginSdk.ContractVersion` — the host can gate on it so a plugin built
against an incompatible (newer-major) contract is rejected cleanly instead of crashing on load.

---

## 8. Lifecycle notes

- `Enable`/`Disable` is instant + per-profile; enabling a never-initialized plugin runs `InitAsync` then.
- **Installing an update** to an already-loaded pack applies on **restart** (a loaded DLL can't be
  overwritten in place — the loader swaps a staged copy on next load). Removing a pack also needs a restart.
- A broken plugin is isolated + non-fatal — it never blocks a profile from opening.
