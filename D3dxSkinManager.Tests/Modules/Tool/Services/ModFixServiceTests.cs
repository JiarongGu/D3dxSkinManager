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
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
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
    }

    private ModFixService CreateService() => new(
        _paths.Object, _query.Object, _archive.Object, _queue,
        _eventBus.Object, Mock.Of<ILogHelper>(), _registry.Object);

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
    public async Task RunFix_AllMods_Success_RunsScriptInPlace_AndRecompresses()
    {
        // One mod with an existing (extracted) cache folder → fix runs in-place, then recompresses.
        var mod = new ModInfo { Id = "MOD1", Name = "Test Mod" };
        Directory.CreateDirectory(Path.Combine(_cacheRoot, mod.Id));
        _query.Setup(q => q.FilterAsync(null, null, null, null, null)).ReturnsAsync(new List<ModInfo> { mod });
        _archive.Setup(a => a.CompressCacheToArchiveAsync(mod.Id, It.IsAny<string>())).ReturnsAsync(true);

        var script = WriteTempScript(".bat", "@echo fixed\r\n@exit /b 0");
        try
        {
            var svc = CreateService();
            var result = await svc.RunFixAsync(new ModFixRequest { ScriptPath = script, RecompressAfter = true });

            result.Total.Should().Be(1);
            result.Succeeded.Should().Be(1);
            result.Failed.Should().Be(0);
            result.Results[0].ExitCode.Should().Be(0);
            _archive.Verify(a => a.CompressCacheToArchiveAsync(mod.Id, It.IsAny<string>()), Times.Once);
            _registry.Verify(r => r.Complete("proc-1"), Times.Once);
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
