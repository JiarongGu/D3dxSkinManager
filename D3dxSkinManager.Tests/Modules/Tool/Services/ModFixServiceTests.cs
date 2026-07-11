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
    private readonly Mock<IModCacheService> _cache = new();
    private readonly Mock<IModRepository> _modRepo = new();
    private readonly Mock<IProfileEventBus> _eventBus = new();
    private readonly Mock<IProcessRegistry> _registry = new();
    private readonly Mock<IProfileContext> _profileContext = new();
    private readonly Mock<IProfileService> _profileService = new();
    private readonly IModOperationQueue _queue = new ModOperationQueue(Mock.Of<ILogHelper>());
    private readonly string _cacheRoot;

    public ModFixServiceTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), "d3dx-modfix-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheRoot);
        _paths.Setup(p => p.CacheModsDirectory).Returns(_cacheRoot);
        // GetCachePath contract: active {id} dir, else DISABLED-{id}, else null — over the temp root.
        _cache.Setup(c => c.GetCachePath(It.IsAny<string>())).Returns((string id) =>
        {
            var active = Path.Combine(_cacheRoot, id);
            if (Directory.Exists(active)) return active;
            var disabled = Path.Combine(_cacheRoot, $"DISABLED-{id}");
            return Directory.Exists(disabled) ? disabled : null;
        });
        _eventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
        _registry.Setup(r => r.Start(It.IsAny<ProcessType>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("proc-1");
        _registry.Setup(r => r.GetToken(It.IsAny<string>())).Returns(CancellationToken.None);
        // Default profile config so the runner resolves effective options to defaults.
        _profileContext.Setup(c => c.ProfileId).Returns("test-profile");
        _profileService.Setup(r => r.GetProfileConfigurationAsync(It.IsAny<string>()))
            .ReturnsAsync(new ProfileConfiguration());
    }

    private ModFixService CreateService() => new(
        _paths.Object, _query.Object, _archive.Object, _cache.Object, _queue, _modRepo.Object,
        _eventBus.Object, Mock.Of<ILogHelper>(), _registry.Object,
        _profileContext.Object, _profileService.Object);

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
    public async Task RunFix_DisabledCache_FixesTheRetainedWorkingCopyInPlace()
    {
        // B3 regression (user report 2026-07-05): an unloaded mod with a DISABLED-{id} cache used to
        // be fixed in a throwaway temp extract — archive patched but the retained working copy left
        // stale, so re-enabling that cache deployed PRE-fix content. The fix must run in the disabled
        // cache itself: both the cache and the archive end up fixed, and nothing is extracted.
        var mod = new ModInfo { Id = "MODDIS", Name = "Disabled Mod" };
        var disabledDir = Path.Combine(_cacheRoot, $"DISABLED-{mod.Id}");
        Directory.CreateDirectory(disabledDir);
        File.WriteAllBytes(Path.Combine(disabledDir, "texture.buf"), new byte[8192]); // bulk, untouched
        _query.Setup(q => q.FilterAsync(null, null, null, null, null)).ReturnsAsync(new List<ModInfo> { mod });
        _archive.Setup(a => a.UpdateFileInArchiveAsync(mod.Id, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var script = WriteTempScript(".bat", "@echo fixed>result.ini\r\n@exit /b 0");
        try
        {
            var svc = CreateService();
            var result = await svc.RunFixAsync(new ModFixRequest { ScriptPath = script, RecompressAfter = true });

            result.Succeeded.Should().Be(1);
            // The disabled cache itself received the fix output...
            File.Exists(Path.Combine(disabledDir, "result.ini")).Should().BeTrue();
            // ...the archive patch sourced the file FROM that cache (not a temp extract)...
            _archive.Verify(a => a.UpdateFileInArchiveAsync(mod.Id,
                It.Is<string>(p => p.StartsWith(disabledDir, StringComparison.OrdinalIgnoreCase)),
                "result.ini"), Times.Once);
            // ...and no extraction happened at all.
            _archive.Verify(a => a.ExtractAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally { File.Delete(script); }
    }

    [Fact]
    public async Task RunFix_SameSizeSameMtimeRewrite_DetectedViaContentHash()
    {
        // B3 regression: a script that rewrites a file preserving its size AND timestamp (temp-write +
        // copystat style) produced an empty length+mtime diff — the fix silently never persisted.
        // Small files are content-hashed in the snapshot, so the rewrite must still be detected.
        var mod = new ModInfo { Id = "MODHASH", Name = "Hash Mod" };
        var cacheDir = Path.Combine(_cacheRoot, mod.Id);
        Directory.CreateDirectory(cacheDir);
        // Bulk untouched file keeps the changed-byte fraction under 50% → fast single-file patch path.
        File.WriteAllBytes(Path.Combine(cacheDir, "texture.buf"), new byte[8192]);
        var target = Path.Combine(cacheDir, "keep.ini");
        File.WriteAllText(target, "AAAA\r\n");
        var fixedStamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(target, fixedStamp);
        _query.Setup(q => q.FilterAsync(null, null, null, null, null)).ReturnsAsync(new List<ModInfo> { mod });
        _archive.Setup(a => a.UpdateFileInArchiveAsync(mod.Id, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        // Rewrite with SAME length (4 chars + CRLF) and restore the SAME mtime.
        var script = WriteTempScript(".bat",
            "@echo BBBB>keep.ini\r\n" +
            "@powershell -NoProfile -Command \"(Get-Item 'keep.ini').LastWriteTimeUtc = [datetime]::new(2020,1,1,0,0,0,[System.DateTimeKind]::Utc)\"\r\n" +
            "@exit /b 0");
        try
        {
            var svc = CreateService();
            var result = await svc.RunFixAsync(new ModFixRequest { ScriptPath = script, RecompressAfter = true });

            result.Succeeded.Should().Be(1);
            _archive.Verify(a => a.UpdateFileInArchiveAsync(mod.Id, It.IsAny<string>(), "keep.ini"), Times.Once);
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
