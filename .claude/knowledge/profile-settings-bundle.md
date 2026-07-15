# Profile settings export/import (.zip bundle) + cross-profile scoped writes

**Export/import a PORTABLE slice of a profile as a `.zip` via `ProfileBundleService` (GLOBAL, behind
`ProfileFacade`); reach another profile's SCOPED data (categories, remote) through
`IProfileServiceProvider` — never by newing the router or raw-SQL'ing another profile's DB.**

## Why

A profile's data is split three ways and the split dictates the whole design:
- **Global services + files** — profile metadata/config/thumbnail (via `IProfileService` +
  `IGlobalPathService`); `ProfileFacade` is a GLOBAL facade (registered off the root provider in
  `ApplicationHost`, NOT in `ProfileServiceRouter.ConfigureProfileRouter`), so a service behind it has
  **no** injected `IProfilePathService`/`ICategoryService`/remote scoped services.
- **Per-profile SQLite (`{profile}/profile.db`)** — the category tree + `RemoteLibraries`/`RemoteTagLabels`
  tables. Reached only through that profile's SCOPED services (`ICategoryService`, `IRemoteLibraryStore`,
  `IRemoteTagLabelStore`), which resolve their profile from an injected `IProfileContext`.
- **GLOBAL files shared across profiles** — remote **source overlays** (`{data}/remote-sources/*.json`)
  and `online-accounts.json` (DPAPI-bound login creds).

So export reads, and import writes, a profile OTHER than the active one — the "one hard part". The
router (`ProfileServiceRouter`) already builds+migrates+caches a scoped `IServiceProvider` per profile,
but it is `new`'d in `ApplicationHost` AFTER the root container is built, so it can't be DI-registered.

## How to Apply

Triggers: profile settings export/import, "bundle", `ProfileBundleService`, cross-profile scoped
read/write, "write into a non-active profile's DB", `IProfileServiceProvider`.

**Cross-profile access — use the accessor, never a shortcut:**
- `IProfileServiceProvider` (Core) + `ProfileServiceProviderAccessor` (a settable holder registered as a
  Core singleton). `ProfileServiceRouter` implements it; `ApplicationHost` calls
  `accessor.Bind(_profileRouter)` right after creating the router. A global service injects
  `IProfileServiceProvider` and does `GetProfileServices(profileId).GetRequiredService<ICategoryService>()`
  — source scope for export, the freshly-created profile's scope for import. This reuses the tested
  scoped logic (thumbnail conversion, migrations, caches) instead of hand-writing SQLite. Do NOT re-`new`
  the router or open another profile's `profile.db` directly.

**Bundle format** (mirror `ModPackageService`): a `.zip` = `profile.json` manifest + `thumbnails/`
(`profile.png` + `categories/<id>.png`). Bundle-local DTOs (`ProfileBundleModels.cs`), not live domain
models, so the on-disk format is stable. Import accepts a **folder OR a .zip**; a zip is extracted into
the NEW profile's `{profile}/temp` with the `TryResolveEntryPath` traversal guard (`IPathValidator`);
`SanitizeFileName` for the output name. Import ALWAYS creates a new profile.

**Contents:** profile config + metadata + thumbnail, category tree + category thumbnails, remote
libraries + tag-rules + tag-labels + customized source overlays. **EXCLUDES** mod archives/DB rows/
previews and `online-accounts.json`.

**Two hard-won behaviors (don't regress — tests lock them):**
- **Export SANITIZES config** — strip machine-specific/path-leaking fields (launch command, external/
  xxmi work dir, fix-tool interpreter; reset work mode to `internal`). A shared `.zip` must never carry
  `C:\Users\<name>\…` (see `sensitive-info.md`). The import UI re-picks the work mode.
- **Source overlays import ADD-MISSING-ONLY** — overlays are GLOBAL/shared, so import applies one only if
  the target has no local overlay for that source; never overwrite (would silently change every profile).

**Facade = fire-and-forget** (`background-task-tracking.md`): `EXPORT_SETTINGS`/`IMPORT_SETTINGS` ack
`{ started = true }` and deliver the result via `PROFILE/EXPORT_SETTINGS_COMPLETE` /
`IMPORT_SETTINGS_COMPLETE` (a failed run STILL emits); import also emits `CREATED` so the profile list
refreshes. `ANALYZE_BUNDLE` is a quick manifest read → stays awaited. `ProcessType.Package` +
`process.profile*` keys.

**File chain (edit together):** `ProfileBundleModels.cs` → `ProfileBundleService.cs` (+
`IProfileServiceProvider.cs`, `CoreServiceExtensions` reg, `ApplicationHost` bind, `ProfileServiceRouter`
implements it) → `ProfileServiceExtensions` DI → `ProfileFacade` routes + `ProfileEvents` →
`profileService.ts` + `profileBundle.types.ts` + `eventBus.ts` enum → `ProfileManager.tsx` +
`ProfileImportDialog.tsx` → `PROFILE_BUNDLE_*` + `profiles.bundle.*` i18n (en+cn).

## Related

- [module-boundaries.md](module-boundaries.md) — global-service ↔ scoped-service access; the accessor is the sanctioned cross-profile path.
- [background-task-tracking.md](background-task-tracking.md) — the fire-and-forget + `*_COMPLETE` event pattern.
- [use-project-paths.md](use-project-paths.md) — staging in `{profile}/temp` (same volume), never OS temp.
- [sensitive-info.md](sensitive-info.md) — why export strips absolute paths from the shareable `.zip`.
- [xxmi-integration.md](xxmi-integration.md) — the import work-mode picker hands XXMI setup to Settings → Mod Work.
