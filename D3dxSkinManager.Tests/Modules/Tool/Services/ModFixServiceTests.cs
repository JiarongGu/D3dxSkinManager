using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Tool.Models;
using D3dxSkinManager.Modules.Tool.Services;

namespace D3dxSkinManager.Tests.Modules.Tool.Services;

/// <summary>
/// Tests for the mod-fix script runner. Validation paths (bad script / no mods) and the happy path
/// (run a real .bat against an extracted mod and persist via recompress) are covered. The runner is
/// game-agnostic — it just executes a user-supplied script with cwd=mod folder.
/// </summary>
public class ModFixServiceTests : IDisposable
{
    private readonly Mock<IProfilePathService> _paths = new();
    private readonly Mock<IModQueryService> _query = new();
    private readonly Mock<IModArchiveService> _archive = new();
    private readonly Mock<IProfileEventBus> _eventBus = new();
    private readonly Mock<IProcessRegistry> _registry = new();
    private readonly Mock<IProfileContext> _profileContext = new();
    private readonly Mock<IProfileRepository> _profileRepo = new();
    private readonly IModOperationQueue _queue = new ModOperationQueue(Mock.Of<ILogHelper>());
    private readonly string _cacheRoot;

    public ModFixServiceTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), "d3dx-modfix-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheRoot);
        _paths.Setup(p => p.CacheModsDirectory).Returns(_cacheRoot);
        _eventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
        _registry.Setup(r => r.Start(It.IsAny<ProcessType>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Returns("proc-1");
        _registry.Setup(r => r.GetToken(It.IsAny<string>())).Returns(CancellationToken.None);
        // Default profile config so the runner resolves effective options to defaults.
        _profileContext.Setup(c => c.ProfileId).Returns("test-profile");
        _profileRepo.Setup(r => r.GetProfileConfigurationAsync(It.IsAny<string>()))
            .ReturnsAsync(new ProfileConfiguration());
    }

    private ModFixService CreateService() => new(
        _paths.Object, _query.Object, _archive.Object, _queue,
        _eventBus.Object, Mock.Of<ILogHelper>(), _registry.Object,
        _profileContext.Object, _profileRepo.Object);

    private static string WriteTempScript(string ext, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"fix-{Guid.NewGuid():N}{ext}");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task RunFix_ScriptMissing_ThrowsScriptNotFound()
    {
        var svc = CreateService();
        var act = () => svc.RunFixAsync(new ModFixRequest { ScriptPath = @"C:\does\not\exist.py" });
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("FIX_SCRIPT_NOT_FOUND");
    }

    [Fact]
    public async Task RunFix_UnsupportedExtension_Throws()
    {
        var script = WriteTempScript(".txt", "not a script");
        try
        {
            var svc = CreateService();
            var act = () => svc.RunFixAsync(new ModFixRequest { ScriptPath = script });
            (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("FIX_SCRIPT_UNSUPPORTED");
        }
        finally { File.Delete(script); }
    }

    [Fact]
    public async Task RunFix_NoMods_Throws()
    {
        var script = WriteTempScript(".bat", "@exit /b 0");
        try
        {
            _query.Setup(q => q.FilterAsync(null, null, null, null, null)).ReturnsAsync(new List<ModInfo>());
            var svc = CreateService();
            var act = () => svc.RunFixAsync(new ModFixRequest { ScriptPath = script });
            (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("FIX_NO_MODS");
        }
        finally { File.Delete(script); }
    }

    [Fact]
    public async Task RunFix_Success_SmallChangeVsBigTexture_PatchesIndividually_NoFullRecompress()
    {
        // A big (unchanged) texture + a tiny .ini the fix writes → changed bytes are well under 50% of
        // the mod, so only that .ini is patched into the archive (fast path), not a full recompress.
        var mod = new ModInfo { Id = "MOD1", Name = "Test Mod" };
        var cacheDir = Path.Combine(_cacheRoot, mod.Id);
        Directory.CreateDirectory(cacheDir);
        File.WriteAllBytes(Path.Combine(cacheDir, "texture.buf"), new byte[8192]); // bulk, untouched
        _query.Setup(q => q.FilterAsync(null, null, null, null, null)).ReturnsAsync(new List<ModInfo> { mod });
        _archive.Setup(a => a.UpdateFileInArchiveAsync(mod.Id, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var script = WriteTempScript(".bat", "@echo fixed>result.ini\r\n@exit /b 0");
        try
        {
            var svc = CreateService();
            var result = await svc.RunFixAsync(new ModFixRequest { ScriptPath = script, RecompressAfter = true });

            result.Succeeded.Should().Be(1);
            result.Results[0].ExitCode.Should().Be(0);
            _archive.Verify(a => a.UpdateFileInArchiveAsync(mod.Id, It.IsAny<string>(), "result.ini"), Times.Once);
            _archive.Verify(a => a.CompressCacheToArchiveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _registry.Verify(r => r.Complete("proc-1"), Times.Once);
        }
        finally { File.Delete(script); }
    }

    [Fact]
    public async Task RunFix_Success_BigChangeFraction_FallsBackToFullRecompress()
    {
        // The fix rewrites the mod's only/bulk file → changed bytes >= 50% of the mod → full recompress.
        var mod = new ModInfo { Id = "MODBIG", Name = "Big Mod" };
        var cacheDir = Path.Combine(_cacheRoot, mod.Id);
        Directory.CreateDirectory(cacheDir);
        File.WriteAllBytes(Path.Combine(cacheDir, "model.buf"), new byte[4096]);
        _query.Setup(q => q.FilterAsync(null, null, null, null, null)).ReturnsAsync(new List<ModInfo> { mod });
        _archive.Setup(a => a.CompressCacheToArchiveAsync(mod.Id, It.IsAny<string>())).ReturnsAsync(true);

        var script = WriteTempScript(".bat", "@echo rewritten>model.buf\r\n@exit /b 0");
        try
        {
            var svc = CreateService();
            var result = await svc.RunFixAsync(new ModFixRequest { ScriptPath = script, RecompressAfter = true });

            result.Succeeded.Should().Be(1);
            _archive.Verify(a => a.CompressCacheToArchiveAsync(mod.Id, It.IsAny<string>()), Times.Once);
            _archive.Verify(a => a.UpdateFileInArchiveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally { File.Delete(script); }
    }

    [Fact]
    public async Task RunFix_Success_FileDeleted_FallsBackToFullRecompress()
    {
        // A fix that removes a file can't be appended away → full recompress.
        var mod = new ModInfo { Id = "MODDEL", Name = "Del Mod" };
        var cacheDir = Path.Combine(_cacheRoot, mod.Id);
        Directory.CreateDirectory(cacheDir);
        File.WriteAllText(Path.Combine(cacheDir, "stale.ini"), "remove me");
        _query.Setup(q => q.FilterAsync(null, null, null, null, null)).ReturnsAsync(new List<ModInfo> { mod });
        _archive.Setup(a => a.CompressCacheToArchiveAsync(mod.Id, It.IsAny<string>())).ReturnsAsync(true);

        var script = WriteTempScript(".bat", "@del stale.ini\r\n@exit /b 0");
        try
        {
            var svc = CreateService();
            var result = await svc.RunFixAsync(new ModFixRequest { ScriptPath = script, RecompressAfter = true });

            result.Succeeded.Should().Be(1);
            _archive.Verify(a => a.CompressCacheToArchiveAsync(mod.Id, It.IsAny<string>()), Times.Once);
            _archive.Verify(a => a.UpdateFileInArchiveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally { File.Delete(script); }
    }

    [Fact]
    public async Task RunFix_Success_NoFileChange_LeavesArchiveUntouched()
    {
        // A fix that changes nothing on disk → neither a patch nor a recompress.
        var mod = new ModInfo { Id = "MODNOOP", Name = "Noop Mod" };
        var cacheDir = Path.Combine(_cacheRoot, mod.Id);
        Directory.CreateDirectory(cacheDir);
        File.WriteAllText(Path.Combine(cacheDir, "keep.ini"), "unchanged");
        _query.Setup(q => q.FilterAsync(null, null, null, null, null)).ReturnsAsync(new List<ModInfo> { mod });

        var script = WriteTempScript(".bat", "@exit /b 0");
        try
        {
            var svc = CreateService();
            var result = await svc.RunFixAsync(new ModFixRequest { ScriptPath = script, RecompressAfter = true });

            result.Succeeded.Should().Be(1);
            _archive.Verify(a => a.CompressCacheToArchiveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _archive.Verify(a => a.UpdateFileInArchiveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally { File.Delete(script); }
    }

    [Fact]
    public async Task RunFix_NonZeroExit_MarksFailed_AndSkipsRecompress()
    {
        var mod = new ModInfo { Id = "MOD2", Name = "Bad Mod" };
        Directory.CreateDirectory(Path.Combine(_cacheRoot, mod.Id));
        _query.Setup(q => q.FilterAsync(null, null, null, null, null)).ReturnsAsync(new List<ModInfo> { mod });

        var script = WriteTempScript(".bat", "@exit /b 3");
        try
        {
            var svc = CreateService();
            var result = await svc.RunFixAsync(new ModFixRequest { ScriptPath = script, RecompressAfter = true });

            result.Failed.Should().Be(1);
            result.Results[0].Success.Should().BeFalse();
            result.Results[0].ExitCode.Should().Be(3);
            _archive.Verify(a => a.CompressCacheToArchiveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally { File.Delete(script); }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_cacheRoot)) Directory.Delete(_cacheRoot, true); } catch { }
    }
}
