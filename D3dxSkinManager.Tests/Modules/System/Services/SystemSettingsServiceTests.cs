using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.System.Services;

namespace D3dxSkinManager.Tests.Modules.SystemModule.Services;

/// <summary>
/// RememberFileDialogPathAsync is an incremental read-modify-write over the shared settings file. Without
/// serialization, concurrent calls for different keys read the same cached settings and the later Save
/// clobbered the earlier key (and raced the shared Dictionary). This locks the no-lost-update guarantee.
/// </summary>
public class SystemSettingsServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly SystemSettingsService _service;

    public SystemSettingsServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-sysset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);

        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.GetGlobalSettingsFilePath("system.json")).Returns(Path.Combine(_dir, "system.json"));

        _service = new SystemSettingsService(paths.Object, Mock.Of<ILogHelper>(),
            new MemoryCache(new MemoryCacheOptions()));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public async Task RememberFileDialogPathAsync_ConcurrentDistinctKeys_AllPersist()
    {
        const int n = 25;

        await Task.WhenAll(Enumerable.Range(0, n)
            .Select(i => _service.RememberFileDialogPathAsync($"key{i}", $"path{i}")));

        for (int i = 0; i < n; i++)
        {
            (await _service.GetFileDialogPathAsync($"key{i}"))
                .Should().Be($"path{i}", $"key{i} must survive the concurrent writes");
        }
    }
}
