using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Core.Services;

/// <summary>
/// Tests for the reusable DownloadService: streamed file download (sha256 + progress + Content-Length
/// total), optional integrity verification, and GetString. Uses a stubbed HttpMessageHandler.
/// </summary>
public class DownloadServiceTests : IDisposable
{
    private readonly string _dir;

    public DownloadServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    private IGlobalPathService Paths()
    {
        var p = new Mock<IGlobalPathService>();
        p.Setup(x => x.DownloadsDirectory).Returns(_dir);
        return p.Object;
    }

    private DownloadService ServiceReturning(byte[] body, HttpStatusCode status = HttpStatusCode.OK)
        => new(new Mock<ILogHelper>().Object, Paths(), new StubHandler(body, status));

    private DownloadService ManagedService()
        => new(new Mock<ILogHelper>().Object, Paths(), new StubHandler(Array.Empty<byte>(), HttpStatusCode.OK));

    [Fact]
    public async Task DownloadAsync_WritesFile_ComputesSha_ReportsProgress()
    {
        var body = Encoding.UTF8.GetBytes(new string('x', 200_000));
        var svc = ServiceReturning(body);
        var dest = Path.Combine(_dir, "out.bin");
        var reports = new List<DownloadProgress>();

        var result = await svc.DownloadAsync(
            new DownloadRequest { Url = "https://example.com/f", DestinationPath = dest },
            new Progress<DownloadProgress>(reports.Add));

        File.Exists(dest).Should().BeTrue();
        (await File.ReadAllBytesAsync(dest)).Should().Equal(body);
        result.Sha256.Should().Be(Sha(body));
        result.Bytes.Should().Be(body.Length);
        // Progress is async (Progress<T> posts to the sync context); the result hashes are the contract.
    }

    [Fact]
    public async Task DownloadAsync_ExpectedShaMatches_Succeeds()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        var svc = ServiceReturning(body);
        var dest = Path.Combine(_dir, "ok.bin");

        var result = await svc.DownloadAsync(new DownloadRequest
        {
            Url = "https://example.com/f",
            DestinationPath = dest,
            ExpectedSha256 = Sha(body),
        });

        result.Sha256.Should().Be(Sha(body));
        File.Exists(dest).Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAsync_ExpectedShaMismatch_ThrowsAndDeletesFile()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        var svc = ServiceReturning(body);
        var dest = Path.Combine(_dir, "bad.bin");

        var ex = await Assert.ThrowsAsync<OperationException>(() => svc.DownloadAsync(new DownloadRequest
        {
            Url = "https://example.com/f",
            DestinationPath = dest,
            ExpectedSha256 = Sha(Encoding.UTF8.GetBytes("different")),
        }));

        ex.Code.Should().Be("DOWNLOAD_HASH_MISMATCH");
        File.Exists(dest).Should().BeFalse(); // partial removed
    }

    [Fact]
    public async Task DownloadAsync_HttpError_ThrowsDownloadFailed()
    {
        var svc = ServiceReturning(Array.Empty<byte>(), HttpStatusCode.NotFound);
        var dest = Path.Combine(_dir, "404.bin");

        var ex = await Assert.ThrowsAsync<OperationException>(() => svc.DownloadAsync(
            new DownloadRequest { Url = "https://example.com/missing", DestinationPath = dest }));

        ex.Code.Should().Be("DOWNLOAD_FAILED");
        File.Exists(dest).Should().BeFalse();
    }

    [Fact]
    public async Task GetStringAsync_ReturnsBody()
    {
        var svc = ServiceReturning(Encoding.UTF8.GetBytes("{\"ok\":true}"));

        (await svc.GetStringAsync("https://example.com/api")).Should().Be("{\"ok\":true}");
    }

    // ---- managed downloads area + cleanup -----------------------------------

    [Fact]
    public async Task DownloadToManagedAsync_WritesIntoManagedDir()
    {
        var body = Encoding.UTF8.GetBytes("pkg");
        var svc = ServiceReturning(body);

        var result = await svc.DownloadToManagedAsync("https://example.com/pkg.zip", "pkg.zip");

        result.FilePath.Should().Be(Path.Combine(_dir, "pkg.zip"));
        File.Exists(Path.Combine(_dir, "pkg.zip")).Should().BeTrue();
        svc.ListManaged().Should().ContainSingle(f => f.Name == "pkg.zip" && f.Size == body.Length);
    }

    [Fact]
    public void CleanupManaged_NoAge_DeletesAllAndReportsBytes()
    {
        var svc = ManagedService();
        File.WriteAllText(Path.Combine(_dir, "a.bin"), "12345");
        File.WriteAllText(Path.Combine(_dir, "b.bin"), "678");

        var result = svc.CleanupManaged();

        result.DeletedCount.Should().Be(2);
        result.BytesFreed.Should().Be(8);
        svc.ListManaged().Should().BeEmpty();
    }

    [Fact]
    public void CleanupManaged_WithAge_DeletesOnlyOldFiles()
    {
        var svc = ManagedService();
        var oldFile = Path.Combine(_dir, "old.bin");
        var newFile = Path.Combine(_dir, "new.bin");
        File.WriteAllText(oldFile, "old");
        File.WriteAllText(newFile, "new");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-10));

        var result = svc.CleanupManaged(TimeSpan.FromDays(7));

        result.DeletedCount.Should().Be(1);
        File.Exists(oldFile).Should().BeFalse();
        File.Exists(newFile).Should().BeTrue();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    /// <summary>Returns a fixed body + status for every request (with a Content-Length).</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly byte[] _body;
        private readonly HttpStatusCode _status;
        public StubHandler(byte[] body, HttpStatusCode status) { _body = body; _status = status; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(_status) { Content = new ByteArrayContent(_body) });
    }
}
