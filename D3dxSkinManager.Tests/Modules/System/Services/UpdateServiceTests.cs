using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.System.Services;

namespace D3dxSkinManager.Tests.Modules.SystemModule.Services;

/// <summary>
/// Tests for UpdateService's file-side logic: staged-update state (ready.json) and sha256 verification
/// of staged files against the staged manifest. The network paths (check/download) are integration-only
/// (static HttpClient) and not covered here. BaseDirectory is mocked to a temp install dir.
/// </summary>
public class UpdateServiceTests : IDisposable
{
    private readonly string _installDir;
    private readonly UpdateService _service;

    public UpdateServiceTests()
    {
        _installDir = Path.Combine(Path.GetTempPath(), "d3dx-upd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_installDir);

        var appEnv = new Mock<IAppEnvironment>();
        appEnv.Setup(e => e.BaseDirectory).Returns(_installDir);

        _service = new UpdateService(
            new Mock<ILogHelper>().Object,
            new Mock<IProcessRegistry>().Object,
            appEnv.Object);
    }

    // ---- GetUpdateStateAsync ------------------------------------------------

    [Fact]
    public async Task GetUpdateStateAsync_NoMarker_NotPending()
    {
        var state = await _service.GetUpdateStateAsync();
        state.Pending.Should().BeFalse();
    }

    [Fact]
    public async Task GetUpdateStateAsync_MarkerPresent_PendingWithVersion()
    {
        var updateDir = Path.Combine(_installDir, ".update");
        Directory.CreateDirectory(updateDir);
        await File.WriteAllTextAsync(
            Path.Combine(updateDir, "ready.json"),
            JsonSerializer.Serialize(new { version = "2.5" }));

        var state = await _service.GetUpdateStateAsync();

        state.Pending.Should().BeTrue();
        state.PendingVersion.Should().Be("2.5");
    }

    // ---- VerifyStagedFilesAsync ---------------------------------------------

    [Fact]
    public async Task VerifyStagedFilesAsync_AllHashesMatch_NoProblems()
    {
        var staged = StageWith(("D3dxSkinManager.exe", "binary-bytes"), ("data/en.json", "{}"));

        (await _service.VerifyStagedFilesAsync(staged)).Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyStagedFilesAsync_TamperedFile_ReportsMismatch()
    {
        var staged = StageWith(("D3dxSkinManager.exe", "original"));
        // Tamper the file after the manifest hash was computed.
        await File.WriteAllTextAsync(Path.Combine(staged, "D3dxSkinManager.exe"), "tampered");

        var problems = await _service.VerifyStagedFilesAsync(staged);

        problems.Should().ContainSingle().Which.Should().Contain("hash mismatch");
    }

    [Fact]
    public async Task VerifyStagedFilesAsync_MissingFile_ReportsMissing()
    {
        var staged = StageWith(("data/en.json", "{}"));
        File.Delete(Path.Combine(staged, "data", "en.json"));

        var problems = await _service.VerifyStagedFilesAsync(staged);

        problems.Should().ContainSingle().Which.Should().Contain("missing");
    }

    [Fact]
    public async Task VerifyStagedFilesAsync_NoManifest_ReportsProblem()
    {
        var staged = Path.Combine(_installDir, "staged-empty");
        Directory.CreateDirectory(staged);

        var problems = await _service.VerifyStagedFilesAsync(staged);

        problems.Should().ContainSingle().Which.Should().Contain("manifest.json");
    }

    // ---- CheckForUpdateAsync (stubbed GitHub) -------------------------------

    [Fact]
    public async Task CheckForUpdateAsync_NewerTag_UpdateAvailableWithReleaseFields()
    {
        var svc = ServiceWith(ReleasesJson("v2.5"), zip: null, currentVersion: "2.4");

        var info = await svc.CheckForUpdateAsync();

        info.UpdateAvailable.Should().BeTrue();
        info.LatestVersion.Should().Be("2.5");
        info.CurrentVersion.Should().Be("2.4");
        info.ReleaseName.Should().Be("D3dxSkinManager v2.5");
        info.ReleaseNotes.Should().Be("notes");
        info.ReleaseUrl.Should().Contain("/tag/v2.5");
    }

    [Fact]
    public async Task CheckForUpdateAsync_OlderTag_NotAvailable()
    {
        var svc = ServiceWith(ReleasesJson("v2.3"), zip: null, currentVersion: "2.4");

        (await svc.CheckForUpdateAsync()).UpdateAvailable.Should().BeFalse();
    }

    // ---- DownloadUpdateAsync (stubbed GitHub: releases + zip) ----------------

    [Fact]
    public async Task DownloadUpdateAsync_GoodZip_VerifiesAndWritesReadyMarker()
    {
        var svc = ServiceWith(ReleasesJson("v2.5"), zip: BuildUpdateZip(tamper: false), currentVersion: "2.4");

        await svc.DownloadUpdateAsync();

        var marker = Path.Combine(_installDir, ".update", "ready.json");
        File.Exists(marker).Should().BeTrue();
        JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(marker))
            .GetProperty("version").GetString().Should().Be("2.5");
        File.Exists(Path.Combine(_installDir, ".update", "staged", "D3dxSkinManager.exe")).Should().BeTrue();
    }

    [Fact]
    public async Task DownloadUpdateAsync_TamperedZip_FailsVerificationNoMarker()
    {
        var svc = ServiceWith(ReleasesJson("v2.5"), zip: BuildUpdateZip(tamper: true), currentVersion: "2.4");

        await svc.DownloadUpdateAsync(); // swallows + Fails the process; never throws

        File.Exists(Path.Combine(_installDir, ".update", "ready.json")).Should().BeFalse();
    }

    // ---- helpers ------------------------------------------------------------

    private UpdateService ServiceWith(string releasesJson, byte[]? zip, string currentVersion)
    {
        var appEnv = new Mock<IAppEnvironment>();
        appEnv.Setup(e => e.BaseDirectory).Returns(_installDir);
        var handler = new StubHandler(releasesJson, zip);
        return new TestableUpdateService(
            new Mock<ILogHelper>().Object, new Mock<IProcessRegistry>().Object,
            appEnv.Object, handler, currentVersion);
    }

    private static string ReleasesJson(string tag) =>
        $"{{\"tag_name\":\"{tag}\",\"name\":\"D3dxSkinManager {tag}\",\"body\":\"notes\"," +
        $"\"html_url\":\"https://github.com/JiarongGu/D3dxSkinManager/releases/tag/{tag}\"," +
        $"\"published_at\":\"2026-06-19T00:00:00Z\",\"assets\":[]}}";

    // An in-memory release zip: app exe + data/en.json + a manifest.json with their sha256s.
    // tamper=true writes a wrong hash for the exe so verification must fail.
    private static byte[] BuildUpdateZip(bool tamper)
    {
        var files = new (string path, string content)[] { ("D3dxSkinManager.exe", "APP"), ("data/en.json", "{}") };
        string Sha(string c) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(c))).ToLowerInvariant();

        var entries = new StringBuilder();
        for (int i = 0; i < files.Length; i++)
        {
            var (path, content) = files[i];
            var hash = (tamper && i == 0) ? Sha("WRONG") : Sha(content);
            if (i > 0) entries.Append(',');
            entries.Append($"{{\"path\":\"{path}\",\"size\":{Encoding.UTF8.GetByteCount(content)},\"sha256\":\"{hash}\"}}");
        }
        var manifest = $"{{\"version\":\"2.5\",\"generatedAt\":\"\",\"files\":[{entries}]}}";

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var e = zip.CreateEntry(path);
                using var s = e.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                s.Write(bytes, 0, bytes.Length);
            }
            var me = zip.CreateEntry("manifest.json");
            using var ms2 = me.Open();
            var mb = Encoding.UTF8.GetBytes(manifest);
            ms2.Write(mb, 0, mb.Length);
        }
        return ms.ToArray();
    }

    /// <summary>Routes GitHub URLs to canned responses: the releases API JSON + the release zip bytes.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _releasesJson;
        private readonly byte[]? _zip;
        public StubHandler(string releasesJson, byte[]? zip) { _releasesJson = releasesJson; _zip = zip; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            if (url.EndsWith("/releases/latest"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_releasesJson, Encoding.UTF8, "application/json"),
                });
            }
            if (url.Contains("/releases/latest/download/") && url.EndsWith(".zip") && _zip != null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_zip),
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class TestableUpdateService : UpdateService
    {
        private readonly string _version;
        public TestableUpdateService(ILogHelper l, IProcessRegistry p, IAppEnvironment e,
            HttpMessageHandler h, string version) : base(l, p, e, h) => _version = version;
        protected override string GetCurrentVersion() => _version;
    }

    // Build a staged dir containing the given files + a manifest.json with their real sha256s.
    private string StageWith(params (string path, string content)[] files)
    {
        var staged = Path.Combine(_installDir, "staged-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staged);

        var entries = new System.Text.StringBuilder();
        for (int i = 0; i < files.Length; i++)
        {
            var (rel, content) = files[i];
            var full = Path.Combine(staged, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var bytes = Encoding.UTF8.GetBytes(content);
            File.WriteAllBytes(full, bytes);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (i > 0) entries.Append(',');
            entries.Append($"{{\"path\":\"{rel}\",\"size\":{bytes.Length},\"sha256\":\"{hash}\"}}");
        }

        var manifest = $"{{\"version\":\"2.5\",\"generatedAt\":\"\",\"files\":[{entries}]}}";
        File.WriteAllText(Path.Combine(staged, "manifest.json"), manifest);
        return staged;
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_installDir)) Directory.Delete(_installDir, true); } catch { }
    }
}
