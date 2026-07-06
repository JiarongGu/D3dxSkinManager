using System.Security.Cryptography;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Reusable HTTP download service — the single chokepoint for fetching files/strings over http(s).
/// Streams to disk with progress, a Content-Length total, incremental sha256, optional integrity
/// verification, and cancellation. Module-agnostic: any service can inject it (app updates, future
/// plugin/asset downloads, etc.). Progress is decoupled (caller supplies <see cref="IProgress{T}"/>);
/// the service knows nothing about the ProcessRegistry.
/// </summary>
public interface IDownloadService
{
    /// <summary>
    /// Download <paramref name="request"/>.Url to its DestinationPath, streaming + reporting progress.
    /// Computes the sha256 while streaming; if ExpectedSha256 is set and differs, deletes the file and
    /// throws. Throws OperationException("DOWNLOAD_FAILED") on network/IO failure (partial file removed).
    /// </summary>
    Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>GET a small resource as a string (e.g. a JSON API response).</summary>
    Task<string> GetStringAsync(string url, IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>POST a JSON body and return the response body (e.g. a JSON API call).</summary>
    Task<string> PostJsonAsync(string url, string jsonBody, IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>The managed downloads directory — the one place kept downloads live + get cleaned.</summary>
    string ManagedDirectory { get; }

    /// <summary>Download a URL into the managed downloads directory under <paramref name="fileName"/>.</summary>
    Task<DownloadResult> DownloadToManagedAsync(string url, string fileName,
        IProgress<DownloadProgress>? progress = null, string? expectedSha256 = null,
        CancellationToken cancellationToken = default);

    /// <summary>List the files currently in the managed downloads directory.</summary>
    IReadOnlyList<ManagedDownloadInfo> ListManaged();

    /// <summary>
    /// Delete managed downloads. With <paramref name="olderThan"/>, only files older than that age are
    /// removed; null deletes them all. Returns how many files + bytes were freed.
    /// </summary>
    DownloadCleanupResult CleanupManaged(TimeSpan? olderThan = null);
}

/// <summary>Implementation of <see cref="IDownloadService"/>.</summary>
public class DownloadService : IDownloadService
{
    private const int BufferSize = 81920;

    private readonly HttpClient _http;
    private readonly ILogHelper _logger;
    private readonly IGlobalPathService _globalPaths;

    // DI constructor — the container can't resolve HttpMessageHandler, so it selects this one.
    public DownloadService(ILogHelper logger, IGlobalPathService globalPaths) : this(logger, globalPaths, null) { }

    // Test constructor — inject a stub handler to fake responses.
    public DownloadService(ILogHelper logger, IGlobalPathService globalPaths, HttpMessageHandler? handler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _globalPaths = globalPaths ?? throw new ArgumentNullException(nameof(globalPaths));
        // No total timeout — large downloads can exceed the default 100s; the stream copy honors the token.
        _http = handler != null
            ? new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }
            : new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        // A User-Agent is required by some hosts (e.g. GitHub returns 403 without one). Callers may
        // override per-request via headers.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("D3dxSkinManager");
    }

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request,
        IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var dest = request.DestinationPath;
        try
        {
            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using var req = BuildRequest(request.Url, request.Headers);
            using var resp = await _http
                .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength;
            long received = 0;
            string sha256;

            // Scope the streams so the file handle is closed before we (possibly) delete it below.
            {
                await using var src = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var dst = File.Create(dest);
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                var buffer = new byte[BufferSize];
                int n;
                while ((n = await src.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), cancellationToken).ConfigureAwait(false);
                    hasher.AppendData(buffer, 0, n);
                    received += n;
                    progress?.Report(new DownloadProgress
                    {
                        BytesReceived = received,
                        TotalBytes = total,
                        Percent = total > 0 ? (int)(received * 100 / total) : null,
                    });
                }

                sha256 = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            }

            if (!string.IsNullOrEmpty(request.ExpectedSha256) &&
                !string.Equals(sha256, request.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(dest);
                throw new OperationException(
                    "DOWNLOAD_HASH_MISMATCH",
                    new Dictionary<string, string> { { "url", request.Url } },
                    $"Downloaded file failed integrity check: {request.Url}");
            }

            return new DownloadResult { FilePath = dest, Bytes = received, Sha256 = sha256 };
        }
        catch (OperationException)
        {
            throw; // already structured (hash mismatch)
        }
        catch (OperationCanceledException)
        {
            TryDelete(dest);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Download failed for {request.Url}: {ex.Message}", "DownloadService");
            TryDelete(dest);
            throw new OperationException(
                "DOWNLOAD_FAILED",
                new Dictionary<string, string> { { "url", request.Url }, { "reason", ex.Message } },
                $"Download failed: {ex.Message}");
        }
    }

    public async Task<string> GetStringAsync(string url, IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = BuildRequest(url, headers);
            using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            return await ReadOrThrowAsync(resp, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new OperationException(
                "DOWNLOAD_FAILED",
                new Dictionary<string, string> { { "url", url }, { "reason", ex.Message } },
                $"Request failed: {ex.Message}");
        }
    }

    public async Task<string> PostJsonAsync(string url, string jsonBody,
        IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = BuildRequest(url, headers);
            req.Method = HttpMethod.Post;
            req.Content = new StringContent(jsonBody, global::System.Text.Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            return await ReadOrThrowAsync(resp, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new OperationException(
                "DOWNLOAD_FAILED",
                new Dictionary<string, string> { { "url", url }, { "reason", ex.Message } },
                $"Request failed: {ex.Message}");
        }
    }

    /// <summary>Read the response body, then on a non-2xx status throw INCLUDING that body — many JSON
    /// APIs (e.g. Quark) return their real error code/message in the body even on a 400, and
    /// EnsureSuccessStatusCode() discards it (leaving an opaque "Response status code ... 400"). The
    /// caller wraps this into DOWNLOAD_FAILED, so the surfaced reason now carries the API's message.</summary>
    private static async Task<string> ReadOrThrowAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var snippet = body.Length > 500 ? body[..500] + "…" : body;
            throw new HttpRequestException(
                $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}{(string.IsNullOrWhiteSpace(snippet) ? "" : $": {snippet}")}");
        }
        return body;
    }

    public string ManagedDirectory => _globalPaths.DownloadsDirectory;

    public Task<DownloadResult> DownloadToManagedAsync(string url, string fileName,
        IProgress<DownloadProgress>? progress = null, string? expectedSha256 = null,
        CancellationToken cancellationToken = default)
    {
        // Guard against path traversal — keep the file inside the managed dir.
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            throw new OperationException("DOWNLOAD_FAILED",
                new Dictionary<string, string> { { "url", url }, { "reason", "invalid file name" } },
                "Download failed: invalid file name");
        }

        var dest = Path.Combine(ManagedDirectory, safeName);
        return DownloadAsync(
            new DownloadRequest { Url = url, DestinationPath = dest, ExpectedSha256 = expectedSha256 },
            progress, cancellationToken);
    }

    public IReadOnlyList<ManagedDownloadInfo> ListManaged()
    {
        if (!Directory.Exists(ManagedDirectory)) return Array.Empty<ManagedDownloadInfo>();

        var list = new List<ManagedDownloadInfo>();
        foreach (var path in Directory.EnumerateFiles(ManagedDirectory))
        {
            var fi = new FileInfo(path);
            list.Add(new ManagedDownloadInfo
            {
                Name = fi.Name,
                Path = fi.FullName,
                Size = fi.Length,
                ModifiedUtc = fi.LastWriteTimeUtc,
            });
        }
        return list;
    }

    public DownloadCleanupResult CleanupManaged(TimeSpan? olderThan = null)
    {
        if (!Directory.Exists(ManagedDirectory))
        {
            return new DownloadCleanupResult { DeletedCount = 0, BytesFreed = 0 };
        }

        var cutoff = olderThan.HasValue ? DateTime.UtcNow - olderThan.Value : (DateTime?)null;
        int deleted = 0;
        long freed = 0;

        foreach (var path in Directory.EnumerateFiles(ManagedDirectory))
        {
            var fi = new FileInfo(path);
            if (cutoff.HasValue && fi.LastWriteTimeUtc >= cutoff.Value) continue;
            try
            {
                var size = fi.Length;
                fi.Delete();
                deleted++;
                freed += size;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not delete managed download {fi.Name}: {ex.Message}", "DownloadService");
            }
        }

        _logger.Info($"Cleaned {deleted} managed download(s), freed {freed} bytes.", "DownloadService");
        return new DownloadCleanupResult { DeletedCount = deleted, BytesFreed = freed };
    }

    private static HttpRequestMessage BuildRequest(string url, IReadOnlyDictionary<string, string>? headers)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (headers != null)
        {
            foreach (var (k, v) in headers)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }
        }
        return req;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ }
    }
}
