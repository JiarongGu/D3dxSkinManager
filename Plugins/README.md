# Plugins moved to their own repo

Official plugin projects now live in a **separate repository**, built and released independently of
the app:

**https://github.com/JiarongGu/D3dxSkinManager.Plugins**

The app ships no plugin bytes. Users install packs from **Settings → Plugins** (in-app download,
which pulls the catalog live from the plugin repo's latest release) or by dropping a plugin dll into
`{profile}/plugins/` and restarting.

## Writing a plugin

Plugins reference two vendored contract dlls (tracked in the plugin repo's `lib/`):

- `D3dxSkinManager.Core.dll` — the runtime contracts (`IPlugin`, `IPluginContext`,
  `IImageReviewPlugin`, IPC/event DTOs). Reference with `<Private>false</Private>` — the host
  provides these types at runtime; shipping a second copy causes a type-identity mismatch.
- `D3dxSkinManager.Plugin.Sdk.dll` — authoring helpers + the contract-version constant.

Authoring guide: [`D3dxSkinManager.Plugin.Sdk/README.md`](../D3dxSkinManager.Plugin.Sdk/README.md).
Architecture, capability interfaces, lifecycle and pack conventions:
[`.claude/knowledge/plugin-system.md`](../.claude/knowledge/plugin-system.md).

## Publishing the contract dlls

When the Core/SDK contracts change, rebuild + vendor them into the plugin repo's `lib/`:

```
node devtools/dev.mjs plugin-sdk [../D3dxSkinManager.Plugins]
```
