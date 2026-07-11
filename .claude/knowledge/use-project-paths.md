# Use the project's own paths — never OS temp/AppData directly

Production code must use the profile/global path services, NOT raw OS locations like
`Path.GetTempPath()` / `%TEMP%` / `Environment.GetFolderPath(...)`.

## Why
Each profile owns its directory tree so data is isolated, discoverable, and cleaned up with the
profile. Writing to the OS temp scatters working files outside the app, bypasses the file-cleanup
tool, and can collide across profiles/instances.

**Performance (the big one):** OS `%TEMP%` is frequently on a **different physical disk** than the mod
data. Staging there and then moving the result into place forces a slow **cross-volume copy**. The
profile's `temp` lives on the **same volume** as the archives/cache it works with, so move/rename is
near-instant (same-volume) instead of a full copy. Keep working files next to the data they touch.

## Use these (inject `IProfilePathService` / `IGlobalPathService`)

| Need | Use |
|------|-----|
| Scratch / staging (extract-then-recompress, working copies) | `IProfilePathService.TempDirectory` (`{profile}/temp`) |
| Extracted mod cache | `CacheModsDirectory` |
| Mod archives | `ModsDirectory` / `GetModArchivePath(id)` |
| Previews / thumbnails / logs / plugins | `PreviewsDirectory` / `ThumbnailsDirectory` / `LogsDirectory` / `PluginsDirectory` |
| Fix-tool library | `FixToolsDirectory` (`{profile}/fixtools`) |

`ModArchiveService` (archive-update temp) and `ModFixService` (fix staging dir, fixed 2026-06-18)
both stage under `_profilePaths.TempDirectory`. Follow that pattern for any new working-file code.

## Dev/test scratch
- Helper scripts + e2e scratch live in the repo (`devtools/`), never `%TEMP%` — see
  [scripts-live-in-repo.md](../rules/scripts-live-in-repo.md).
- xUnit fixtures may use `Path.GetTempPath()` (isolated + cleaned in `Dispose`) — that's test-only and
  fine; the rule above is about **production** code.

## Past incident
- **2026-06-18**: `ModFixService` staged its extract-fix-recompress working dir in `Path.GetTempPath()`.
  Routed to `IProfilePathService.TempDirectory`.
