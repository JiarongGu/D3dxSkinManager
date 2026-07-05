using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Fetch seam for remote-library pages. The "http" engine (plain requests via the shared
/// IDownloadService chokepoint) covers server-rendered sites like huihui168.org; a future
/// "webview" engine (WebView2-rendered, for JS-heavy/anti-bot sites) plugs in behind this
/// interface without touching the parsing layer.
/// </summary>
public interface IRemotePageFetcher
{
    /// <summary>GET a page/API response as a string.</summary>
    Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>POST a JSON body, returning the response body (used by download-host APIs).</summary>
    Task<string> PostJsonAsync(string url, string jsonBody, CancellationToken cancellationToken = default);
}

/// <summary>Plain-HTTP engine. Sends a browser-like User-Agent (some sites gate on it).</summary>
public class HttpPageFetcher : IRemotePageFetcher
{
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
