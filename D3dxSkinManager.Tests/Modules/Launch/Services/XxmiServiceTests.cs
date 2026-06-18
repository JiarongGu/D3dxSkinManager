using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Launch.Services;

namespace D3dxSkinManager.Tests.Modules.Launch.Services;

/// <summary>
/// Unit tests for XxmiService — detects an XXMI Launcher install and resolves importer Mods paths
/// by parsing "XXMI Launcher Config.json". Uses a temp-dir fixture (test-only OS temp is allowed).
/// </summary>
public class XxmiServiceTests : IDisposable
{
    private readonly string _root;
    private readonly XxmiService _service;

    public XxmiServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xxmi-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _service = new XxmiService(new Mock<ILogHelper>().Object);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_root, "XXMI Launcher Config.json"), json);

    private const string SampleConfig = @"{
        ""Launcher"": { ""active_importer"": ""ZZMI"", ""enabled_importers"": [ ""EFMI"", ""ZZMI"" ] },
        ""Importers"": {
            ""ZZMI"": { ""Importer"": { ""importer_folder"": ""ZZMI/"", ""game_folder"": ""E:/Games/ZZZ"" } },
            ""EFMI"": { ""Importer"": { ""importer_folder"": ""EFMI/"", ""game_folder"": """" } },
            ""WWMI"": { ""Importer"": { ""importer_folder"": ""WWMI/"" } }
        }
    }";

    /// <summary>Create an importer subfolder with a Mods dir so it's discovered on disk.</summary>
    private void MakeImporter(string name) =>
        Directory.CreateDirectory(Path.Combine(_root, name, "Mods"));

    [Fact]
    public async Task DetectAsync_DiscoversImportersOnDisk_AndResolvesModsPaths()
    {
        WriteConfig(SampleConfig);
        MakeImporter("ZZMI");
        MakeImporter("EFMI");
        // WWMI is in config but NOT on disk → must NOT appear (list is disk-driven, not the game set).
        Directory.CreateDirectory(Path.Combine(_root, "Resources")); // non-importer dir, ignored

        var result = await _service.DetectAsync(_root);

        result.Found.Should().BeTrue();
        result.ConfigPath.Should().Be(Path.Combine(_root, "XXMI Launcher Config.json"));

        var zzmi = result.Importers.Single(i => i.Name == "ZZMI");
        zzmi.ImporterDir.Should().Be(Path.GetFullPath(Path.Combine(_root, "ZZMI")));
        zzmi.ModsDir.Should().Be(Path.Combine(Path.GetFullPath(Path.Combine(_root, "ZZMI")), "Mods"));
        zzmi.GameFolder.Should().Be("E:/Games/ZZZ"); // enriched from config
        zzmi.IsActive.Should().BeTrue();
        zzmi.IsInstalled.Should().BeTrue();

        result.Importers.Should().Contain(i => i.Name == "EFMI");
        result.Importers.Should().NotContain(i => i.Name == "WWMI"); // config-only, not on disk
        result.Importers.Should().NotContain(i => i.Name == "Resources");
    }

    [Fact]
    public async Task DetectAsync_DiscoversImporterWithoutConfigEntry()
    {
        WriteConfig(SampleConfig);
        MakeImporter("FUTUREMI"); // a custom importer not in the config game list

        var result = await _service.DetectAsync(_root);

        result.Importers.Should().Contain(i => i.Name == "FUTUREMI" && i.IsInstalled);
    }

    [Fact]
    public async Task DetectAsync_OrdersActiveFirst()
    {
        WriteConfig(SampleConfig);
        MakeImporter("EFMI");
        MakeImporter("ZZMI"); // active per config

        var result = await _service.DetectAsync(_root);

        result.Importers[0].Name.Should().Be("ZZMI"); // active → first regardless of disk order
    }

    [Fact]
    public async Task DetectAsync_AcceptsLauncherExePath_AndWalksUpToRoot()
    {
        WriteConfig(SampleConfig);
        var binDir = Path.Combine(_root, "Resources", "Bin");
        Directory.CreateDirectory(binDir);
        var exe = Path.Combine(binDir, "XXMI Launcher.exe");
        File.WriteAllText(exe, "stub");

        var result = await _service.DetectAsync(exe);

        result.Found.Should().BeTrue();
        result.LauncherExe.Should().Be(exe);
    }

    [Fact]
    public async Task DetectAsync_Throws_WhenConfigMissing()
    {
        var act = async () => await _service.DetectAsync(_root); // no config written

        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("XXMI_CONFIG_NOT_FOUND");
    }
}
