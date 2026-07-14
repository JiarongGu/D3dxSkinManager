namespace D3dxSkinManager.Modules.Remote.Models;

/// <summary>
/// A queued remote download+import — the params of one <c>DOWNLOAD_IMPORT</c>, bundled so they can be
/// serialized into the REMOTE_IMPORT <c>WorkflowInfo.Context</c> and run by the import queue actor (via
/// <c>RemoteImportWorkflowHandler</c>). Crash-resume re-runs the download step from these params (no
/// half-file resume). Serialized camelCase.
/// </summary>
public class RemoteImportJob
{
    public string SourceId { get; set; } = string.Empty;
    public string? ListId { get; set; }
    public string? EntryId { get; set; }
    public List<string>? Tags { get; set; }
    public RemoteModDetail Detail { get; set; } = new();
    public RemoteDownloadOption Option { get; set; } = new();
    public string? CategoryId { get; set; }
    public string? Password { get; set; }
}

/// <summary>Which leg of a two-stage remote import a workflow row is on.</summary>
public enum RemoteImportStage
{
    /// <summary>Network-bound: resolve + download the raw bytes to disk (import-queue DOWNLOAD lane).</summary>
    Download,
    /// <summary>CPU-bound: extract + recompress + import from the downloaded bytes (import-queue IMPORT lane).</summary>
    Import,
}

/// <summary>
/// What the DOWNLOAD stage produced, handed to the IMPORT stage — persisted in the workflow context so a
/// finished download WAITS for an import slot (and <c>Cancel</c> between stages can clean it up). The raw
/// archive lands in the managed downloads folder; a MEGA folder streams its file tree straight into
/// <c>{StagingDir}/extract</c> (no single archive). Serialized camelCase.
/// </summary>
public sealed class RemoteDownloadResult
{
    /// <summary>The per-job staging dir ({profile}/temp/remote-*) holding extract/ (and normalized/ later).</summary>
    public string StagingDir { get; set; } = string.Empty;
    /// <summary>The downloaded raw archive in the managed downloads folder; null for a MEGA folder tree.</summary>
    public string? ArchivePath { get; set; }
    /// <summary>MEGA folder: {StagingDir}/extract already holds the decrypted file tree — skip extraction.</summary>
    public bool ExtractPrepopulated { get; set; }
    /// <summary>Re-download dedup hash (may be computed in the import stage for a MEGA folder).</summary>
    public string? ContentSha { get; set; }
    /// <summary>Resolved file name (drives the normalized archive name + the extract password retry message).</summary>
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// The REMOTE_IMPORT workflow context — a two-stage remote download+import: a DOWNLOAD stage (network
/// lane) then an IMPORT stage (compress lane), so a finished download waits for an import slot instead of
/// one shared queue coupling network + CPU. Serialized camelCase into <c>WorkflowInfo.Context</c>.
/// Back-compat: an older row whose context is a bare <see cref="RemoteImportJob"/> deserializes with
/// <see cref="Job"/> populated and <see cref="Stage"/> = Download (re-downloads — same as before).
/// </summary>
public sealed class RemoteImportWorkflowContext
{
    public RemoteImportJob Job { get; set; } = new();
    public RemoteImportStage Stage { get; set; } = RemoteImportStage.Download;
    public RemoteDownloadResult? Download { get; set; }
}
