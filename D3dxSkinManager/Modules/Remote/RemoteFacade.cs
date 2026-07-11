using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Event;
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
    Task<RemoteIndexPage> QueryIndexAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null, string? tag = null, bool importedOnly = false);
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
    private readonly IEventBus _eventBus;
    private readonly IPayloadHelper _payloadHelper;

    public RemoteFacade(
        IRemoteBrowseService browse,
        IRemoteImportService import,
        IRemoteIndexService index,
        IRemoteSourceStore sourceStore,
        IRemoteLibraryStore libraries,
        IOnlineAccountStore accounts,
        IExternalLoginService login,
        IEventBus eventBus,
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
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
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
            "RESOLVE_IMPORT_CATEGORY" => ResolveImportCategory(request),
            "INDEX_QUERY" => await QueryIndexAsync(request),
            "GET_IMPORTED_STATE" => await GetImportedStateAsync(request),
            "INDEX_TAGS" => await _index.GetTagsAsync(
                _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId"),
                _payloadHelper.GetRequiredValue<string>(request.Payload, "listId")),
            "INDEX_SYNC" => StartIndexSync(request),
            "SAVE_SOURCE" => SaveSource(request),
            "DELETE_SOURCE" => DeleteSource(request),
            "GET_SOURCE_CONFIG" => _sourceStore.GetById(
                _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId")),
            "TEST_SOURCE" => await TestSourceAsync(request),
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
            "ACCOUNT_LOGIN" => StartAccountLogin(request),
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

    public async Task<RemoteIndexPage> QueryIndexAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null, string? tag = null, bool importedOnly = false)
    {
        // Import-domain logic lives in RemoteImportService (imported lookup is cached there — no per-page
        // mod rescan). "Downloaded only" restricts the query to this source+list's imported entry ids;
        // then each returned entry is flagged/located against the imported lookup.
        var onlyEntryIds = importedOnly
            ? await _import.GetImportedEntryIdsAsync(sourceId, listId).ConfigureAwait(false)
            : null;

        var result = await _index.QueryAsync(sourceId, listId, search, page, pageSize, sort, tag, onlyEntryIds).ConfigureAwait(false);
        await _import.AnnotateImportedAsync(result.Entries, sourceId, listId).ConfigureAwait(false);
        return result;
    }

    public string StartIndexSync(string sourceId, string listId, bool full = false) => _index.StartSync(sourceId, listId, full);

    /// <summary>
    /// IPC handler for one entry's imported state — the detail screen re-queries this when a
    /// background remote import completes so the "already imported" banner appears live.
    /// IPC Message: GET_IMPORTED_STATE
    /// Payload: { sourceId, listId?, entryId?, detailUrl? }
    /// </summary>
    private async Task<object?> GetImportedStateAsync(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var listId = _payloadHelper.GetOptionalValue<string>(request.Payload, "listId");
        var entryId = _payloadHelper.GetOptionalValue<string>(request.Payload, "entryId");
        var detailUrl = _payloadHelper.GetOptionalValue<string>(request.Payload, "detailUrl");

        var (imported, localModIds) = await _import
            .GetImportedStateAsync(sourceId, listId, entryId, detailUrl).ConfigureAwait(false);
        return new { imported, localModIds };
    }

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

    /// <summary>Preview the local category a would-be import resolves to (the library's ORDERED tag
    /// rules — same logic the import uses), so the download-confirm popup can PRESELECT it. Returns
    /// { categoryId: null } when there's no library/list or no rule matches.</summary>
    private object ResolveImportCategory(IpcRequest request)
    {
        var sourceId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceId");
        var listId = _payloadHelper.GetOptionalValue<string>(request.Payload, "listId");
        var tags = _payloadHelper.GetOptionalValue<List<string>>(request.Payload, "tags") ?? new List<string>();
        var title = _payloadHelper.GetOptionalValue<string>(request.Payload, "title");
        if (string.IsNullOrWhiteSpace(listId)) return new { categoryId = (string?)null };
        var library = _libraries.FindBySourceList(sourceId, listId!);
        var categoryId = library == null ? null : RemoteImportService.MatchTagRules(library.TagRules, tags, title);
        return new { categoryId };
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
        var importedOnly = _payloadHelper.GetOptionalValue<bool>(request.Payload, "importedOnly");
        return QueryIndexAsync(sourceId, listId, search, page <= 0 ? 1 : page, pageSize <= 0 ? 60 : pageSize, sort, tag, importedOnly);
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

    /// <summary>Open the login window FIRE-AND-FORGET (a real QR login easily outlives the 30s IPC
    /// bridge timeout, which would leave the card stuck on "not logged in"). Ack immediately; when the
    /// window finishes (captured or cancelled), emit ONLINE_ACCOUNT_CHANGED so the card refreshes.</summary>
    private object StartAccountLogin(IpcRequest request)
    {
        var provider = _payloadHelper.GetRequiredValue<string>(request.Payload, "provider");
        _ = Task.Run(async () =>
        {
            try { await _login.LoginAsync(provider).ConfigureAwait(false); }
            catch (Exception ex) { _logger.Error($"[Remote] account login failed: {ex.Message}", ModuleName, ex); }
            finally { await EmitAccountChangedAsync().ConfigureAwait(false); }
        });
        return new { started = true };
    }

    private object RemoveAccount(IpcRequest request)
    {
        var provider = _payloadHelper.GetRequiredValue<string>(request.Payload, "provider");
        _accounts.Remove(provider);
        _login.ClearProfile(provider); // also wipe the WebView2 login profile so logout is a real logout
        _ = EmitAccountChangedAsync();
        return _accounts.List();
    }

    private Task EmitAccountChangedAsync() =>
        _eventBus.EmitAsync(ModuleNames.SYSTEM, SystemEvents.ONLINE_ACCOUNT_CHANGED);
}
