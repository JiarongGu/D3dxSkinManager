# D3dxSkinManager Plugins

Official plugin projects — built **separately** from the app (`D3dxSkinManager.Plugins.slnx`,
CI: `.github/workflows/plugins.yml` attaches pack zips to releases). The app itself ships no
plugin bytes; users install packs from **Settings → Plugins** (in-app download) or by dropping the
plugin dll into `{profile}/plugins/` and restarting.

Architecture, capability interfaces, lifecycle and pack conventions:
[`.claude/rules/plugin-system.md`](../.claude/rules/plugin-system.md).

## Plugins

| Project | Pack id | What it does |
|---------|---------|--------------|
| `D3dxSkinManager.Plugins.ContentVeil` | `content-veil-ai` | AI detection for the content veil — anime censor-point detector (ONNX, deepghs `censor_detect_v1.0_n`, MIT). Implements the host's `IImageReviewPlugin` interceptor. SINGLE-DLL pack: the model, the managed ONNX Runtime wrapper (AssemblyResolve from embedded resources) and the native onnxruntime dlls (extracted to the plugin data dir at init) all ride inside the one dll. |

> The 14 Python-port plugin stubs that used to live here were removed 2026-07-11 — they targeted
> a long-removed interface and never compiled. `git log` has them if ever needed.

## Building

```
dotnet build plugins/D3dxSkinManager.Plugins.ContentVeil/D3dxSkinManager.Plugins.ContentVeil.csproj
```

The ContentVeil model file is NOT in git — fetch it once into `Models/censor-detect.onnx`
(the CI workflow shows the pinned URL + sha256).
