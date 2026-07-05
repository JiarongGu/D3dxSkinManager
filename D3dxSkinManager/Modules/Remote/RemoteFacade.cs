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
    string StartDownloadImport(string sourceId, RemoteModDetail detail, RemoteDownloadOption option);
    Task<RemoteIndexPage> QueryIndexAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null);
    string StartIndexSync(string sourceId, string listId);
}

public class RemoteFacade : BaseFacade, IRemoteFacade
{
    protected override string ModuleName => "RemoteFacade";

    private readonly IRemoteBrowseService _browse;
    private readonly IRemoteImportService _import;
    private readonly IRemoteIndexService _index;
    private readonly IRemoteSourceStore _sourceStore;
    private readonly IRemoteBindingStore _binding;
    private readonly IPayloadHelper _payloadHelper;

    public RemoteFacade(
        IRemoteBrowseService browse,
        IRemoteImportService import,
        IRemoteIndexService index,
        IRemoteSourceStore sourceStore,
        IRemoteBindingStore binding,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _browse = browse ?? throw new ArgumentNullException(nameof(browse));
        _import = import ?? throw new ArgumentNullException(nameof(import));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _sourceStore = sourceStore ?? throw new ArgumentNullException(nameof(sourceStore));
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
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
            "INDEX_QUERY" => await QueryIndexAsync(request),
            "INDEX_SYNC" => StartIndexSync(request),
            "SAVE_SOURCE" => SaveSource(request),
            "DELETE_SOURCE" => DeleteSource(request),
            "GET_SOURCE_CONFIG" => _sourceStore.GetById(
                _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId")),
            "TEST_SOURCE" => await TestSourceAsync(request),
            "GET_SOURCE_TEMPLATE" => _sourceStore.GetTemplateJson(),
            "GET_BINDING" => (object?)_binding.Get(),
            "SET_BINDING" => SetBinding(request),
            "CLEAR_BINDING" => ClearBinding(),
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

    public string StartDownloadImport(string sourceId, RemoteModDetail detail, RemoteDownloadOption option) =>
        _import.StartDownloadImport(sourceId, detail, option);

    public async Task<RemoteIndexPage> QueryIndexAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null)
    {
        var result = await _index.QueryAsync(sourceId, listId, search, page, pageSize, sort).ConfigureAwait(false);
        // Flag entries this profile already imported (matched by detail URL from mod Metadata).
        var imported = await _import.GetImportedDetailUrlsAsync().ConfigureAwait(false);
        if (imported.Count > 0)
        {
            foreach (var entry in result.Entries)
                entry.Imported = imported.Contains(entry.DetailUrl);
        }
        return result;
    }

    public string StartIndexSync(string sourceId, string listId) => _index.StartSync(sourceId, listId);

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
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var detail = _payloadHelper.GetRequiredValue<RemoteModDetail>(request.Payload, "detail");
        var option = _payloadHelper.GetRequiredValue<RemoteDownloadOption>(request.Payload, "option");
        var processId = StartDownloadImport(sourceId, detail, option);
        return new { started = true, processId };
    }

    private Task<RemoteIndexPage> QueryIndexAsync(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var listId = _payloadHelper.GetRequiredValue<string>(request.Payload, "listId");
        var search = _payloadHelper.GetOptionalValue<string>(request.Payload, "search");
        var page = _payloadHelper.GetOptionalValue<int>(request.Payload, "page");
        var pageSize = _payloadHelper.GetOptionalValue<int>(request.Payload, "pageSize");
        var sort = _payloadHelper.GetOptionalValue<string>(request.Payload, "sort");
        return QueryIndexAsync(sourceId, listId, search, page <= 0 ? 1 : page, pageSize <= 0 ? 60 : pageSize, sort);
    }

    private object StartIndexSync(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var listId = _payloadHelper.GetRequiredValue<string>(request.Payload, "listId");
        var processId = StartIndexSync(sourceId, listId);
        return new { started = processId.Length > 0, processId };
    }

    private object SaveSource(IpcRequest request)
    {
        var config = _payloadHelper.GetRequiredValue<RemoteSourceConfig>(request.Payload, "config");
        return _sourceStore.Save(config);
    }

    private object DeleteSource(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        return _sourceStore.Delete(sourceId);
    }

    private object SetBinding(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var listId = _payloadHelper.GetRequiredValue<string>(request.Payload, "listId");
        _ = _sourceStore.GetById(sourceId); // validate the source exists before persisting
        var binding = _binding.Set(sourceId, listId);
        // Bind & sync in one step (the setup flow's "绑定并开始同步"). Idempotent if already syncing.
        var sync = _payloadHelper.GetOptionalValue<bool>(request.Payload, "sync");
        var processId = sync ? _index.StartSync(sourceId, listId) : string.Empty;
        return new { binding, processId };
    }

    private object ClearBinding()
    {
        _binding.Clear();
        return true;
    }

    private Task<RemoteSourceTestResult> TestSourceAsync(IpcRequest request)
    {
        var config = _payloadHelper.GetRequiredValue<RemoteSourceConfig>(request.Payload, "config");
        var listId = _payloadHelper.GetOptionalValue<string>(request.Payload, "listId");
        return _browse.TestConfigAsync(config, listId);
    }
}
