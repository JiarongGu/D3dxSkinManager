using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
