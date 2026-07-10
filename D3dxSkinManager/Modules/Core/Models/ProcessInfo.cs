namespace D3dxSkinManager.Modules.Core.Models;

/// <summary>
/// Lifecycle state of a tracked long-running process.
/// NOTE: serialized as camelCase strings (JsonStringEnumConverter(CamelCase)) — frontend type must
/// be 'running' not 'Running'. See .claude/rules/enum-serialization.md.
/// </summary>
public enum ProcessStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    /// <summary>Was Running when the app exited/crashed — detected on next startup from persisted state.</summary>
    Interrupted,
}

/// <summary>
/// Category of a tracked process (drives the icon/grouping in the Activity panel).
/// Serialized as camelCase strings.
/// </summary>
public enum ProcessType
{
    ModLoad,
    ModImport,
    ModDelete,
    PresetApply,
    BatchUpdate,
    Analysis,
    Package,
    Migration,
    Cleanup,
    ArchiveUpdate,
    FileScan,
    Download,
    ModFix,
    Optimize,
    Other,
}

/// <summary>
/// A single tracked long-running operation. The backend ProcessRegistry is the authoritative source;
/// the frontend mirrors the full list via the PROCESS_LIST_UPDATED event.
/// </summary>
public class ProcessInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ProcessType Type { get; set; } = ProcessType.Other;
    public ProcessStatus Status { get; set; } = ProcessStatus.Running;

    /// <summary>Short title shown in the list (e.g. "Loading mod: Blue Hair"). English fallback —
    /// the frontend prefers TitleKey when set so the Activity panel follows the UI language.</summary>
    public string Title { get; set; } = "";
    /// <summary>Frontend i18n key for the title (e.g. "process.modLoad"); interpolates TitleArg as {{arg}}.</summary>
    public string? TitleKey { get; set; }
    /// <summary>Single interpolation argument for TitleKey (mod name, count, ...).</summary>
    public string? TitleArg { get; set; }

    /// <summary>Optional secondary detail line (e.g. current file / sub-step). English fallback.</summary>
    public string? Detail { get; set; }
    /// <summary>Frontend i18n key for the detail stage line (e.g. "process.stage.extracting").</summary>
    public string? DetailKey { get; set; }

    /// <summary>0–100 for determinate progress; null = indeterminate spinner.</summary>
    public int? Progress { get; set; }

    /// <summary>Error message when Status == Failed.</summary>
    public string? Error { get; set; }

    /// <summary>Whether this process exposes a working cancel.</summary>
    public bool Cancellable { get; set; }

    /// <summary>
    /// Whether this process can be resumed from where it left off if interrupted by a crash. The op
    /// opts in (it must checkpoint its own progress + provide a resume entrypoint). Most ops are not
    /// resumable; mod analysis is (it persists + resumes sessions).
    /// </summary>
    public bool Resumable { get; set; }

    /// <summary>
    /// Opaque op-specific token the resume handler uses to continue (e.g. an analysis session id).
    /// Only meaningful when Resumable.
    /// </summary>
    public string? ResumePayload { get; set; }

    /// <summary>
    /// Owning profile for profile-scoped work (analysis/sync/mod ops) — null for app-level ops
    /// (self-update, XXMI installer download). Carried on PROCESS_RESUME_REQUESTED so a resume
    /// targets the RIGHT profile, not whichever happens to be selected.
    /// </summary>
    public string? ProfileId { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}
