using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Tests.Modules.Core.Helpers;

/// <summary>
/// Regression for the code-review finding: CompressFolderAsync did not forward its CancellationToken
/// to Task.Run, so a cancelled request still ran the whole compression (and only reported cancelled
/// afterward). Now the token is forwarded — a pre-cancelled request never starts and leaves no output.
/// Uses real 7z (libs/7z.dll ships in the test bin), same env setup as ArchiveHelperUpdateTests.
/// </summary>
public class ArchiveHelperCompressCancellationTests : IDisposable
{
    private static readonly IAppEnvironment _env = MakeEnv(AppContext.BaseDirectory);
    private readonly ArchiveHelper _helper = new(Mock.Of<ILogHelper>(), _env);
    private readonly string _root;

    private static IAppEnvironment MakeEnv(string baseDir)
    {
        var m = new Mock<IAppEnvironment>();
        m.Setup(e => e.BaseDirectory).Returns(baseDir);
        return m.Object;
    }

    public ArchiveHelperCompressCancellationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "d3dx-arc-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public async Task CompressFolderAsync_WithAlreadyCancelledToken_ThrowsAndWritesNoOutput()
    {
        var src = Path.Combine(_root, "src");
        Directory.CreateDirectory(src);
        await File.WriteAllTextAsync(Path.Combine(src, "a.txt"), "hello");
        var outPath = Path.Combine(_root, "out.7z");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _helper.CompressFolderAsync(src, outPath, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(outPath).Should().BeFalse(
            "a pre-cancelled compress must not run or leave output (the token is now forwarded to Task.Run)");
    }
}
