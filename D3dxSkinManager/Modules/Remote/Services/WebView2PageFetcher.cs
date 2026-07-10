using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// The "webview" transport (<see cref="IRemotePageFetcher"/>): fetches a page by rendering it in a
/// single, persistent, OFF-SCREEN WebView2 and reading the JS-produced DOM (`outerHTML`) — for
/// JS-heavy / anti-bot sites that a plain HTTP GET returns empty/blocked. Selected per source via
/// <see cref="Models.RemoteSourceConfig.Fetcher"/> = "webview"; the parsing engine (http-regex) is
/// unchanged and runs over the rendered HTML.
///
/// Modeled on the proven off-screen-WebView2 pattern in <see cref="ExternalLoginService"/>: all
/// WebView2 work marshals to the WinForms UI thread; one hidden window is reused across fetches and
/// navigations are serialized (a WebView2 hosts one document at a time). POST (download-host JSON
/// APIs) never needs JS rendering, so it delegates to plain HTTP.
///
/// NOTE (2026-07-11): shipped as the seam's real implementation but NOT yet verified against a live
/// JS site — no configured source currently needs it (huihui + GameBanana are plain HTTP). Confirm
/// against a real webview-transport source before relying on it.
/// </summary>
public class WebView2PageFetcher : IRemotePageFetcher
{
    /// <summary>Max wait for a navigation to complete before giving up on a page.</summary>
    private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(30);
    /// <summary>Settle delay after NavigationCompleted so late SPA/JS content renders before we read
    /// the DOM. A conservative default; a per-site knob can be added if a real site needs tuning.</summary>
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(1500);

    private readonly IFormInteractionService _forms;
    private readonly HttpPageFetcher _http; // POSTs (JSON APIs) don't need rendering — reuse plain HTTP
    private readonly IGlobalPathService _globalPaths;
    private readonly ILogHelper _logger;

    /// <summary>Serializes navigations — a WebView2 renders one document at a time.</summary>
    private readonly SemaphoreSlim _navGate = new(1, 1);

    // The single persistent hidden window + WebView2 (created lazily on the UI thread, reused).
    private Form? _form;
    private WebView2? _webView;

    public WebView2PageFetcher(
        IFormInteractionService forms,
        HttpPageFetcher http,
        IGlobalPathService globalPaths,
        ILogHelper logger)
    {
        _forms = forms;
        _http = http;
        _globalPaths = globalPaths;
        _logger = logger;
    }

    public string FetcherId => "webview";

    // JSON POST APIs are server endpoints, not JS-rendered pages — plain HTTP is correct + far cheaper.
    public Task<string> PostJsonAsync(string url, string jsonBody, CancellationToken cancellationToken = default) =>
        _http.PostJsonAsync(url, jsonBody, cancellationToken);

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
    {
        await _navGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunOnUiThreadAsync(() => NavigateAndReadAsync(url, cancellationToken)).ConfigureAwait(false);
        }
        catch (OperationException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.Warn($"[WebView2Fetcher] fetch failed for {url}: {ex.Message}", "WebView2PageFetcher");
            throw new OperationException("REMOTE_FETCH_FAILED", "url", url);
        }
        finally
        {
            _navGate.Release();
        }
    }

    /// <summary>Marshal an async unit of WebView2 work onto the WinForms UI thread (WebView2 is
    /// UI-affine). Throws REMOTE_FETCH_FAILED if there's no UI (e.g. headless/tests).</summary>
    private Task<string> RunOnUiThreadAsync(Func<Task<string>> action)
    {
        var mainForm = _forms.GetMainForm()
            ?? throw new OperationException("REMOTE_FETCH_FAILED", "url", "webview-unavailable");
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        mainForm.BeginInvoke(new Action(async () =>
        {
            try { tcs.TrySetResult(await action().ConfigureAwait(true)); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }));
        return tcs.Task;
    }

    /// <summary>Runs ON the UI thread: ensure the WebView2, navigate, wait for load + settle, read DOM.</summary>
    private async Task<string> NavigateAndReadAsync(string url, CancellationToken ct)
    {
        await EnsureWebViewAsync().ConfigureAwait(true);
        var core = _webView!.CoreWebView2;

        // NavigationCompleted → complete the wait TCS with success/failure.
        var navTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnNav(object? _, CoreWebView2NavigationCompletedEventArgs e) => navTcs.TrySetResult(e.IsSuccess);
        core.NavigationCompleted += OnNav;
        try
        {
            core.Navigate(url);
            var completed = await Task.WhenAny(navTcs.Task, Task.Delay(NavigationTimeout, ct)).ConfigureAwait(true);
            if (completed != navTcs.Task)
                throw new OperationException("REMOTE_FETCH_FAILED", "url", url); // timed out
            if (!await navTcs.Task.ConfigureAwait(true))
                throw new OperationException("REMOTE_FETCH_FAILED", "url", url); // navigation error
        }
        finally
        {
            core.NavigationCompleted -= OnNav;
        }

        // Let late JS/SPA content render, then read the produced DOM.
        await Task.Delay(SettleDelay, ct).ConfigureAwait(true);
        var json = await core.ExecuteScriptAsync("document.documentElement.outerHTML").ConfigureAwait(true);

        // ExecuteScriptAsync returns the JS value JSON-encoded (a quoted, escaped string).
        var html = string.IsNullOrEmpty(json)
            ? null
            : global::System.Text.Json.JsonSerializer.Deserialize<string>(json);
        return html ?? string.Empty;
    }

    /// <summary>Create the hidden off-screen window + WebView2 once (UI thread). A real window handle
    /// is required for WebView2 to run, so it's shown OFF-SCREEN + off the taskbar (never visible).</summary>
    private async Task EnsureWebViewAsync()
    {
        if (_webView?.CoreWebView2 != null) return;

        _form = new Form
        {
            Text = "Remote fetch (hidden)",
            Width = 1280,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ShowInTaskbar = false,
        };
        _webView = new WebView2 { Dock = DockStyle.Fill };
        _form.Controls.Add(_webView);
        _form.Show(); // off-screen — creates the handle so the WebView2 can run

        var userDataFolder = Path.Combine(_globalPaths.GlobalSettingsDirectory, "webview-fetch");
        Directory.CreateDirectory(userDataFolder);
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder).ConfigureAwait(true);
        await _webView.EnsureCoreWebView2Async(env).ConfigureAwait(true);
    }
}
