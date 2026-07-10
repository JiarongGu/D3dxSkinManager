using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Fetch seam for remote-library pages. The "http" transport (plain requests via the shared
/// IDownloadService chokepoint) covers server-rendered sites like huihui168.org; the "webview"
/// transport (<see cref="WebView2PageFetcher"/>, WebView2-rendered, for JS-heavy/anti-bot sites)
/// plugs in behind this interface without touching the parsing layer. A source's
/// <see cref="RemoteSourceConfig.Fetcher"/> field selects one — see <see cref="IRemotePageFetcherRouter"/>.
/// </summary>
public interface IRemotePageFetcher
{
    /// <summary>Which <see cref="RemoteSourceConfig.Fetcher"/> value selects this impl ("http"/"webview").</summary>
    string FetcherId { get; }

    /// <summary>GET a page/API response as a string.</summary>
    Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>POST a JSON body, returning the response body (used by download-host APIs).</summary>
    Task<string> PostJsonAsync(string url, string jsonBody, CancellationToken cancellationToken = default);
}

/// <summary>Picks the fetcher for a source config by its <see cref="RemoteSourceConfig.Fetcher"/>
/// (default "http"). Kept tiny + explicit so engines stay unaware of transport wiring.</summary>
public interface IRemotePageFetcherRouter
{
    IRemotePageFetcher For(RemoteSourceConfig config);
}

/// <inheritdoc cref="IRemotePageFetcherRouter"/>
public class RemotePageFetcherRouter : IRemotePageFetcherRouter
{
    private readonly HttpPageFetcher _http;
    private readonly WebView2PageFetcher _webview;

    public RemotePageFetcherRouter(HttpPageFetcher http, WebView2PageFetcher webview)
    {
        _http = http;
        _webview = webview;
    }

    public IRemotePageFetcher For(RemoteSourceConfig config) =>
        string.Equals(config.Fetcher, "webview", StringComparison.OrdinalIgnoreCase) ? _webview : _http;
}

/// <summary>Plain-HTTP engine. Sends a browser-like User-Agent (some sites gate on it).</summary>
public class HttpPageFetcher : IRemotePageFetcher
{
    public string FetcherId => "http";

    // A desktop-browser UA; the shared HttpClient's default ("D3dxSkinManager") stays for other callers.
    private static readonly IReadOnlyDictionary<string, string> BrowserHeaders = new Dictionary<string, string>
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36",
        ["Accept-Language"] = "zh-CN,zh;q=0.9,en;q=0.8",
    };

    private readonly IDownloadService _download;

    public HttpPageFetcher(IDownloadService download)
    {
        _download = download;
    }

    public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) =>
        _download.GetStringAsync(url, BrowserHeaders, cancellationToken);

    public Task<string> PostJsonAsync(string url, string jsonBody, CancellationToken cancellationToken = default) =>
        _download.PostJsonAsync(url, jsonBody, BrowserHeaders, cancellationToken);
}
