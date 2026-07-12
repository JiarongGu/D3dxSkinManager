# Release/update locations live in config, not hardcoded — shipped in res/, offline-first, no UI

**The app-updater and plugin-catalog release LOCATIONS come from `IReleaseEndpointConfig`, not from
constants. They resolve per-field through three layers (highest priority first):**

1. **`data/settings/endpoints.json`** — an operator OVERRIDE (not shipped, not in the UI).
2. **`res/endpoints.json`** — the SHIPPED, repo-managed default (source `Resources/endpoints.json` →
   csproj `Content TargetPath res\endpoints.json`), released together with `res/remote-sources/`.
3. **the code constants** (`ReleaseEndpointConfig.Default*`) — last-resort fallback if the res file is
   missing/corrupt.

So a release location can move by editing the repo-managed `res/endpoints.json` (ships in the next
release) or an operator's `data/settings/endpoints.json` — with NO recompile of the consumers.

## Why

`UpdateService` (app self-update) and `PluginInstallService` (plugin catalog) fetch from GitHub
releases; their repo URLs were hardcoded, so moving/mirroring meant a recompile. The default now lives
in the repo as a SHIPPED res file (visible, versioned, released — the same model as the remote-source
adapter seeds), with a data/settings override on top and the constants only as a safety net. It's a
GLOBAL setting deliberately kept OUT of the settings page (an operator knob, not a user preference).

Offline-first: every layer is a LOCAL read (or pure constants) — a missing/partial/corrupt file just
falls to the next layer, and both fetchers are already network-tolerant (no update shown / empty
catalog on failure). The app always starts, online or off.

## Shipped-config location (res/)

Repo-managed defaults that ship read-only in `res/` (source dir → csproj `Content TargetPath`):
`res/endpoints.json` (release locations) and `res/remote-sources/*.json` (remote-library adapter
seeds — `RemoteSourceStore` copies missing ones into the writable `data/remote-sources/` on first run).
Both are "code managed in repo, released together." New shipped default config belongs here too, NOT in
user `data/`.

## How to Apply

- **Read a location** → inject `IReleaseEndpointConfig` (Core); read `AppReleaseApi` / `AppDownloadBase`
  / `PluginReleaseApi` / `PluginDownloadPrefix` / `PluginManifestAsset`. Never re-hardcode a release URL.
- **Change the shipped default** → edit `Resources/endpoints.json` (→ `res/endpoints.json`) and ship a
  release. Keep the `Default*` constants roughly in sync (they only fire if res is gone).
- **Override at runtime** → drop `data/settings/endpoints.json` with only the fields to change; blank/
  absent field falls through. Not auto-written (avoids a stale copy shadowing a future shipped default);
  case-insensitive.
- **Add a new configurable location** (4 edits): nullable field on `EndpointConfig` (Core/Models);
  `Default…` const + get-only prop + `Resolve` line in `ReleaseEndpointConfig` (Core/Services); prop on
  the interface; then inject. Add the field to `Resources/endpoints.json` too.
- **DI:** `AddSingleton<IReleaseEndpointConfig, ReleaseEndpointConfig>` in `CoreServiceExtensions` — the
  tracked-singleton helper shares ONE instance into every profile container (global `UpdateService` +
  profile-scoped `PluginInstallService` see the same config). The DI ctor is marked
  `[ActivatorUtilitiesConstructor]` (the class has a second, params ctor for tests).
- **Trust gate unchanged:** a resolved plugin asset URL must still `StartsWith(PluginDownloadPrefix)` —
  the trust anchor moves WITH the location (both from the same config).

## Edge cases where it does NOT apply

- **XXMI** (`XxmiService`) fetches a THIRD-PARTY tool's releases (SpectrumQT) — still hardcoded; fold it
  in the same way only if that location needs to move.
- The GitHub `Accept` header + the `d3dx.`-prefix pack-id convention are protocol, not location.

## Related

- [plugin-system.md](plugin-system.md) — the plugin catalog consumer + separate-repo model.
- [launcher-topology.md](launcher-topology.md) — the app updater stages; the launcher applies.
- [download-service.md](download-service.md) — the fetch itself (network-tolerant).
- [use-project-paths.md](use-project-paths.md) — res/ (shipped read-only) vs data/ (writable) via path services.
