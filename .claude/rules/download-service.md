# Downloading files over HTTP — use the shared `IDownloadService` (never `new HttpClient`)

`Modules/Core/Services/DownloadService.cs` is the **single HTTP chokepoint** for fetching files/strings.
Any module that needs to download something injects `IDownloadService` — do NOT create an `HttpClient`
in a feature service (UpdateService used to; it now delegates here).

## API (`IDownloadService`)
- `Task<DownloadResult> DownloadAsync(DownloadRequest, IProgress<DownloadProgress>?, CancellationToken)` —
  stream a URL to a file. Reports progress (bytes + Content-Length total + percent), computes the
  **sha256 while streaming**, and if `DownloadRequest.ExpectedSha256` is set, **verifies it** (mismatch →
  deletes the file + throws `OperationException("DOWNLOAD_HASH_MISMATCH")`). Network/IO failure →
  `OperationException("DOWNLOAD_FAILED")` (partial file removed). Honors cancellation.
- `Task<string> GetStringAsync(url, headers?, ct)` — small JSON/text GET (e.g. a GitHub API call).
- **Managed downloads area** (the "one place kept downloads live + get cleaned"):
  - `string ManagedDirectory` = `IGlobalPathService.DownloadsDirectory` (`{data}/downloads`).
  - `DownloadToManagedAsync(url, fileName, progress?, expectedSha256?, ct)` — downloads into the managed
    dir (basename-sanitized, no traversal).
  - `IReadOnlyList<ManagedDownloadInfo> ListManaged()` — list (name/path/size/modified).
  - `DownloadCleanupResult CleanupManaged(TimeSpan? olderThan = null)` — delete all (or only files older
    than the age); returns count + bytes freed.

## Conventions
- **Progress is decoupled.** The service knows nothing about `ProcessRegistry`; the caller passes an
  `IProgress<DownloadProgress>` and maps it (e.g. UpdateService maps 0–100% → the registry's 0–90% band).
  For a long download, the *caller* registers the `ProcessType.Download` process (see
  `background-task-tracking.md`) and reports via that progress callback. The download itself is still
  kicked off **fire-and-forget** from the facade (never awaited in the IPC handler).
- **A User-Agent is always sent** (`D3dxSkinManager`) — some hosts (GitHub) 403 without one. Pass extra
  per-request headers (e.g. `Accept`) via `DownloadRequest.Headers` / `GetStringAsync` headers.
- **Integrity:** prefer passing `ExpectedSha256` when you know the hash. When you don't (e.g. the update
  zip has no published hash), verify after the fact — the update flow extracts then sha256-checks every
  file against the staged manifest (`UpdateService.VerifyStagedFilesAsync`).
- **Kept vs. staged downloads:** general/kept downloads belong in `ManagedDirectory` (cleanable via
  `CleanupManaged`). The **update** package is a special case — it stages under `{install}/.update` (same
  volume as the install so the launcher can apply it), NOT the managed dir, and is cleared by the
  launcher after apply.
- **Testing:** `DownloadService` has a test constructor taking an `HttpMessageHandler` (stub canned
  responses); the DI container selects the handler-less ctor (it can't resolve `HttpMessageHandler`).
  Feature services that consume `IDownloadService` should inject a **fake `IDownloadService`** in tests
  (see `UpdateServiceTests.FakeDownloadService`) rather than stubbing HTTP. Coverage:
  `DownloadServiceTests` (stream/sha/verify/error/managed/cleanup), `UpdateServiceTests` (consumer).

## Registration
Registered in `CoreServiceExtensions.AddCoreServices` as a singleton (`IDownloadService` →
`DownloadService`), so it's available to every module incl. profile-scoped services. `DownloadsDirectory`
is created by `GlobalPathService.EnsureDirectoriesExist`.
