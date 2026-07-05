using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Modules.Remote;

/// <summary>
/// Facade for the remote mod library.
/// Responsibility: thin IPC delegation to browse/resolve/import services.
/// IPC module: REMOTE. DOWNLOAD_IMPORT is fire-and-forget (immediate ack; progress via the
/// Activity panel — see background-task-tracking.md).
/// </summary>
public interface IRemoteFacade : IModuleFacade
{
    Task<List<RemoteSourceInfo>> GetSourcesAsync();
    Task<RemoteBrowseResult> BrowseAsync(string sourceId, string listId, int page);
    Task<RemoteBrowseResult> SearchAsync(string sourceId, string query);
    Task<RemoteModDetail> GetDetailAsync(string sourceId, string detailUrl);
    Task<RemoteResolveResult> ResolveDownloadAsync(RemoteDownloadOption option);
    string StartDownloadImport(RemoteModDetail detail, RemoteDownloadOption option);
}

public class RemoteFacade : BaseFacade, IRemoteFacade
{
    protected override string ModuleName => "RemoteFacade";

    private readonly IRemoteBrowseService _browse;
    private readonly IRemoteImportService _import;
    private readonly IPayloadHelper _payloadHelper;

    public RemoteFacade(
        IRemoteBrowseService browse,
        IRemoteImportService import,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _browse = browse ?? throw new ArgumentNullException(nameof(browse));
        _import = import ?? throw new ArgumentNullException(nameof(import));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            "GET_SOURCES" => await GetSourcesAsync(),
            "BROWSE" => await BrowseAsync(request),
            "SEARCH" => await SearchAsync(request),
            "GET_DETAIL" => await GetDetailAsync(request),
            "RESOLVE_DOWNLOAD" => await ResolveDownloadAsync(request),
            "DOWNLOAD_IMPORT" => StartDownloadImport(request),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<List<RemoteSourceInfo>> GetSourcesAsync() =>
        await _browse.GetSourcesAsync().ConfigureAwait(false);

    public async Task<RemoteBrowseResult> BrowseAsync(string sourceId, string listId, int page) =>
        await _browse.BrowseAsync(sourceId, listId, page).ConfigureAwait(false);

    public async Task<RemoteBrowseResult> SearchAsync(string sourceId, string query) =>
        await _browse.SearchAsync(sourceId, query).ConfigureAwait(false);

    public async Task<RemoteModDetail> GetDetailAsync(string sourceId, string detailUrl) =>
        await _browse.GetDetailAsync(sourceId, detailUrl).ConfigureAwait(false);

    public async Task<RemoteResolveResult> ResolveDownloadAsync(RemoteDownloadOption option) =>
        await _import.ResolveAsync(option).ConfigureAwait(false);

    public string StartDownloadImport(RemoteModDetail detail, RemoteDownloadOption option) =>
        _import.StartDownloadImport(detail, option);

    // ---- payload-parsing handlers ----------------------------------------------------------

    private Task<RemoteBrowseResult> BrowseAsync(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var listId = _payloadHelper.GetRequiredValue<string>(request.Payload, "listId");
        var page = _payloadHelper.GetOptionalValue<int>(request.Payload, "page");
        return BrowseAsync(sourceId, listId, page <= 0 ? 1 : page);
    }

    private Task<RemoteBrowseResult> SearchAsync(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var query = _payloadHelper.GetRequiredValue<string>(request.Payload, "query");
        return SearchAsync(sourceId, query);
    }

    private Task<RemoteModDetail> GetDetailAsync(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var detailUrl = _payloadHelper.GetRequiredValue<string>(request.Payload, "url");
        return GetDetailAsync(sourceId, detailUrl);
    }

    private Task<RemoteResolveResult> ResolveDownloadAsync(IpcRequest request)
    {
        var option = _payloadHelper.GetRequiredValue<RemoteDownloadOption>(request.Payload, "option");
        return ResolveDownloadAsync(option);
    }

    private object StartDownloadImport(IpcRequest request)
    {
        // Parse + validate synchronously so bad input errors right away; the work itself runs in
        // the background (never await a long op in an IPC handler — the bridge times out).
        var detail = _payloadHelper.GetRequiredValue<RemoteModDetail>(request.Payload, "detail");
        var option = _payloadHelper.GetRequiredValue<RemoteDownloadOption>(request.Payload, "option");
        var processId = StartDownloadImport(detail, option);
        return new { started = true, processId };
    }
}
