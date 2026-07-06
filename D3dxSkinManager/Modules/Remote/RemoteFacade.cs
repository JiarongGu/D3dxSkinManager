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
    Task<RemoteBrowseResult> SearchAsync(string sourceId, string query, string? listId = null);
    Task<RemoteModDetail> GetDetailAsync(string sourceId, string detailUrl);
    Task<RemoteResolveResult> ResolveDownloadAsync(RemoteDownloadOption option);
    Task<RemoteIndexPage> QueryIndexAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null, string? tag = null);
    string StartIndexSync(string sourceId, string listId, bool full = false);
}

public class RemoteFacade : BaseFacade, IRemoteFacade
{
    protected override string ModuleName => "RemoteFacade";

    private readonly IRemoteBrowseService _browse;
    private readonly IRemoteImportService _import;
    private readonly IRemoteIndexService _index;
    private readonly IRemoteSourceStore _sourceStore;
    private readonly IRemoteLibraryStore _libraries;
    private readonly IOnlineAccountStore _accounts;
    private readonly IExternalLoginService _login;
    private readonly IPayloadHelper _payloadHelper;

    public RemoteFacade(
        IRemoteBrowseService browse,
        IRemoteImportService import,
        IRemoteIndexService index,
        IRemoteSourceStore sourceStore,
        IRemoteLibraryStore libraries,
        IOnlineAccountStore accounts,
        IExternalLoginService login,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _browse = browse ?? throw new ArgumentNullException(nameof(browse));
        _import = import ?? throw new ArgumentNullException(nameof(import));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _sourceStore = sourceStore ?? throw new ArgumentNullException(nameof(sourceStore));
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _login = login ?? throw new ArgumentNullException(nameof(login));
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
            "INDEX_TAGS" => await _index.GetTagsAsync(
                _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId"),
                _payloadHelper.GetRequiredValue<string>(request.Payload, "listId")),
            "INDEX_SYNC" => StartIndexSync(request),
            "SAVE_SOURCE" => SaveSource(request),
            "DELETE_SOURCE" => DeleteSource(request),
            "GET_SOURCE_CONFIG" => _sourceStore.GetById(
                _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId")),
            "TEST_SOURCE" => await TestSourceAsync(request),
            "GET_SOURCE_TEMPLATE" => _sourceStore.GetTemplateJson(),
            // Remote images are served via the app://remote-image proxy (global on-demand cache) —
            // the old RESOLVE_IMAGES preload round-trip is gone.
            // Configured libraries (remote-library-redesign.md): a profile owns many, switchable.
            "LIBRARY_GET_STATE" => _libraries.GetState(),
            "LIBRARY_ADD" => AddLibrary(request),
            "LIBRARY_UPDATE" => _libraries.Update(
                _payloadHelper.GetRequiredValue<RemoteLibrary>(request.Payload, "library")),
            "LIBRARY_REMOVE" => _libraries.Remove(
                _payloadHelper.GetRequiredValue<string>(request.Payload, "libraryId")),
            "LIBRARY_SET_ACTIVE" => _libraries.SetActive(
                _payloadHelper.GetRequiredValue<string>(request.Payload, "libraryId")),
            // Online-storage accounts (auth'd download hosts, e.g. Quark) — the login opens an
            // in-app WebView2 window and captures the session cookie (see ExternalLoginService).
            "ACCOUNT_LIST" => _accounts.List(),
            "ACCOUNT_LOGIN" => await _login.LoginAsync(
                _payloadHelper.GetRequiredValue<string>(request.Payload, "provider")),
            "ACCOUNT_REMOVE" => RemoveAccount(request),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<List<RemoteSourceInfo>> GetSourcesAsync() =>
        await _browse.GetSourcesAsync().ConfigureAwait(false);

    public async Task<RemoteBrowseResult> BrowseAsync(string sourceId, string listId, int page) =>
        await _browse.BrowseAsync(sourceId, listId, page).ConfigureAwait(false);

    public async Task<RemoteBrowseResult> SearchAsync(string sourceId, string query, string? listId = null) =>
        await _browse.SearchAsync(sourceId, query, listId).ConfigureAwait(false);

    public async Task<RemoteModDetail> GetDetailAsync(string sourceId, string detailUrl) =>
        await _browse.GetDetailAsync(sourceId, detailUrl).ConfigureAwait(false);

    public async Task<RemoteResolveResult> ResolveDownloadAsync(RemoteDownloadOption option) =>
        await _import.ResolveAsync(option).ConfigureAwait(false);

    public async Task<RemoteIndexPage> QueryIndexAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null, string? tag = null)
    {
        var result = await _index.QueryAsync(sourceId, listId, search, page, pageSize, sort, tag).ConfigureAwait(false);
        // Flag entries this profile already imported. Primary match = the standardized identity key
        // (sourceId|listId|entryId — survives a site changing hosts); legacy imports fall back to
        // detail-URL matching. The lookup is cached in the import service (no per-page mod rescan).
        var (keys, legacyUrls) = await _import.GetImportedLookupAsync().ConfigureAwait(false);
        if (keys.Count > 0 || legacyUrls.Count > 0)
        {
            foreach (var entry in result.Entries)
                entry.Imported = keys.Contains(RemoteImportService.ImportedKey(sourceId, listId, entry.Id))
                                 || legacyUrls.Contains(entry.DetailUrl);
        }
        return result;
    }

    public string StartIndexSync(string sourceId, string listId, bool full = false) => _index.StartSync(sourceId, listId, full);

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
        var listId = _payloadHelper.GetOptionalValue<string>(request.Payload, "listId");
        return SearchAsync(sourceId, query, listId);
    }

    private async Task<RemoteModDetail> GetDetailAsync(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var detailUrl = _payloadHelper.GetRequiredValue<string>(request.Payload, "url");
        var listId = _payloadHelper.GetOptionalValue<string>(request.Payload, "listId");
        var detail = await GetDetailAsync(sourceId, detailUrl).ConfigureAwait(false);

        // Detail pages can reveal tags the list feed doesn't carry (GameBanana's sub category) —
        // merge them into the index entry so the tag filter learns them over time. Flat tags, no
        // hierarchy; fire-and-forget so the detail view never waits on the index write.
        if (!string.IsNullOrWhiteSpace(listId) && detail.Tags.Count > 0)
        {
            _ = Task.Run(() => _index.MergeEntryTagsByUrlAsync(sourceId, listId!, detail.DetailUrl, detail.Tags));
        }
        return detail;
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
        var listId = _payloadHelper.GetOptionalValue<string>(request.Payload, "listId");
        var entryId = _payloadHelper.GetOptionalValue<string>(request.Payload, "entryId");
        var tags = _payloadHelper.GetOptionalValue<List<string>>(request.Payload, "tags");
        var categoryId = _payloadHelper.GetOptionalValue<string>(request.Payload, "categoryId");
        var password = _payloadHelper.GetOptionalValue<string>(request.Payload, "password");
        var detail = _payloadHelper.GetRequiredValue<RemoteModDetail>(request.Payload, "detail");
        var option = _payloadHelper.GetRequiredValue<RemoteDownloadOption>(request.Payload, "option");
        var processId = _import.StartDownloadImport(sourceId, listId, entryId, tags, detail, option, categoryId, password);
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
        var tag = _payloadHelper.GetOptionalValue<string>(request.Payload, "tag");
        return QueryIndexAsync(sourceId, listId, search, page <= 0 ? 1 : page, pageSize <= 0 ? 60 : pageSize, sort, tag);
    }

    private object StartIndexSync(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var listId = _payloadHelper.GetRequiredValue<string>(request.Payload, "listId");
        var full = _payloadHelper.GetOptionalValue<bool>(request.Payload, "full");
        var processId = StartIndexSync(sourceId, listId, full);
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

    private object AddLibrary(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var listId = _payloadHelper.GetRequiredValue<string>(request.Payload, "listId");
        var name = _payloadHelper.GetOptionalValue<string>(request.Payload, "name");
        var tagRules = _payloadHelper.GetOptionalValue<List<RemoteTagRule>>(request.Payload, "tagRules");
        var library = _libraries.Add(sourceId, listId, name ?? string.Empty, tagRules);
        // Add & sync in one step (the "add library" flow). Idempotent if already syncing.
        var sync = _payloadHelper.GetOptionalValue<bool>(request.Payload, "sync");
        var processId = sync ? _index.StartSync(sourceId, listId) : string.Empty;
        return new { library, processId };
    }

    private Task<RemoteSourceTestResult> TestSourceAsync(IpcRequest request)
    {
        var config = _payloadHelper.GetRequiredValue<RemoteSourceConfig>(request.Payload, "config");
        var listId = _payloadHelper.GetOptionalValue<string>(request.Payload, "listId");
        return _browse.TestConfigAsync(config, listId);
    }

    private object RemoveAccount(IpcRequest request)
    {
        var provider = _payloadHelper.GetRequiredValue<string>(request.Payload, "provider");
        _accounts.Remove(provider);
        _login.ClearProfile(provider); // also wipe the WebView2 login profile so logout is a real logout
        return _accounts.List();
    }
}
