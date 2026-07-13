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
