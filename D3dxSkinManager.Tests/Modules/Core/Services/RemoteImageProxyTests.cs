using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Core.Services;

/// <summary>
/// The remote-image proxy is hit CONCURRENTLY for the same URL — the &lt;img&gt; serve
/// (CustomSchemeHandler) and the content-veil check (ContentVeilService) resolve the same URL at the same
/// time. The bug: the cache streamed straight to the final path, so a concurrent reader saw a PARTIAL
/// file (image "load failed" on first paint, fine after a hard reload once the cache was complete). These
/// tests lock: (1) concurrent same-URL fetches coalesce to ONE download, (2) the download goes to a temp
/// path then atomically renames in — the final path only ever exists COMPLETE.
/// </summary>
public class RemoteImageProxyTests : IDisposable
{
    private readonly string _dataDir;
    private readonly Mock<IGlobalPathService> _paths = new();

    public RemoteImageProxyTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "d3dx-rimg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);
        _paths.Setup(p => p.BaseDataPath).Returns(_dataDir);
    }

    private RemoteImageProxy Make(IDownloadService dl) =>
        new RemoteImageProxy(_paths.Object, dl, new Mock<ILogHelper>().Object);

    [Fact]
    public async Task ConcurrentSameUrl_CoalescesToOneDownload()
    {
        var dl = new RecordingDownloadService(new byte[] { 1, 2, 3, 4 }, delayMs: 60);
        var proxy = Make(dl);
        const string url = "https://example.com/a.jpg";

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => proxy.GetOrFetchAsync(url)));

        dl.Calls.Should().Be(1, "concurrent fetches of the same url must share ONE download");
        results.Should().OnlyContain(r => r != null && File.Exists(r!));
        results.Distinct().Should().HaveCount(1, "every caller resolves to the same cached file");
    }

    [Fact]
    public async Task DownloadsToTempThenRenames_FinalPathNeverPartial()
    {
        var dl = new RecordingDownloadService(new byte[] { 9, 9, 9 }, delayMs: 0);
        var proxy = Make(dl);

        var path = await proxy.GetOrFetchAsync("https://example.com/b.png");

        path.Should().NotBeNull();
        path!.Should().NotEndWith(".tmp");
        File.Exists(path).Should().BeTrue();
        File.ReadAllBytes(path).Should().Equal(new byte[] { 9, 9, 9 });
        // The actual write targeted a temp path (atomic rename into place) — never the final path, so a
        // concurrent reader can never observe a half-written file at the final path.
        dl.Destinations.Should().OnlyContain(d => d.EndsWith(".tmp"));
    }

    [Fact]
    public async Task CacheHit_ReturnsExistingWithoutRedownloading()
    {
        var dl = new RecordingDownloadService(new byte[] { 1 }, delayMs: 0);
        var proxy = Make(dl);
        const string url = "https://example.com/c.jpg";

        var first = await proxy.GetOrFetchAsync(url);
        dl.Calls.Should().Be(1);

        var second = await proxy.GetOrFetchAsync(url);
        second.Should().Be(first);
        dl.Calls.Should().Be(1, "the second call is a cache hit — no re-download");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://x/y.jpg")]
    [InlineData("not-a-url")]
    public async Task InvalidUrl_ReturnsNull_NoDownload(string url)
    {
        var dl = new RecordingDownloadService(new byte[] { 1 }, delayMs: 0);
        (await Make(dl).GetOrFetchAsync(url)).Should().BeNull();
        dl.Calls.Should().Be(0);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, recursive: true); } catch { }
    }

    /// <summary>Records each DownloadAsync (count + destination) and simulates a streamed write with an
    /// optional delay so concurrent fetches overlap.</summary>
    private sealed class RecordingDownloadService : IDownloadService
    {
        private readonly byte[] _bytes;
        private readonly int _delayMs;
        public int Calls;
        public readonly ConcurrentBag<string> Destinations = new();

        public RecordingDownloadService(byte[] bytes, int delayMs) { _bytes = bytes; _delayMs = delayMs; }

        public async Task<DownloadResult> DownloadAsync(DownloadRequest request,
            IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            Destinations.Add(request.DestinationPath);
            var dir = Path.GetDirectoryName(request.DestinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (_delayMs > 0) await Task.Delay(_delayMs, cancellationToken);
            await File.WriteAllBytesAsync(request.DestinationPath, _bytes, cancellationToken);
            return new DownloadResult { FilePath = request.DestinationPath, Bytes = _bytes.Length, Sha256 = string.Empty };
        }

        public Task<string> GetStringAsync(string url, IReadOnlyDictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string> PostJsonAsync(string url, string jsonBody, IReadOnlyDictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string> PostFormAsync(string url, IReadOnlyDictionary<string, string> form, IReadOnlyDictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public string ManagedDirectory => string.Empty;
        public Task<DownloadResult> DownloadToManagedAsync(string url, string fileName,
            IProgress<DownloadProgress>? progress = null, string? expectedSha256 = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IReadOnlyList<ManagedDownloadInfo> ListManaged() => Array.Empty<ManagedDownloadInfo>();
        public DownloadCleanupResult CleanupManaged(TimeSpan? olderThan = null) => new();
    }
}
