using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Launch.Services;
using D3dxSkinManager.Modules.System.Services;
using D3dxSkinManager.Modules.Tool.Services;

namespace D3dxSkinManager.Tests.Modules.Launch.Services;

/// <summary>
/// Locks the path-traversal guard in <see cref="D3DMigotoService.DeployVersionAsync"/>: the IPC-supplied
/// versionName is a plain identifier, never a path. A traversal value must be rejected BEFORE any file op;
/// a legit simple name must pass the guard (and then fail only because no archive exists in the test dir).
/// </summary>
public class D3DMigotoServiceDeployGuardTests : IDisposable
{
    private readonly string _tdMigotoDir;
    private readonly string _workDir;
    private readonly D3DMigotoService _service;

    public D3DMigotoServiceDeployGuardTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "d3dx-deploy-test-" + Guid.NewGuid().ToString("N"));
        _tdMigotoDir = Path.Combine(root, "3dmigoto");
        _workDir = Path.Combine(root, "work");
        Directory.CreateDirectory(_tdMigotoDir);
        Directory.CreateDirectory(_workDir);

        var paths = new Mock<IProfilePathService>();
        paths.Setup(p => p.TdMigotoDirectory).Returns(_tdMigotoDir);
        paths.Setup(p => p.ProfilePath).Returns(root);

        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.GetWorkDirectory()).Returns(_workDir);

        _service = new D3DMigotoService(
            paths.Object,
            Mock.Of<IFileHelper>(),
            config.Object,
            Mock.Of<ISystemProcessService>(),
            Mock.Of<IArchiveHelper>(),
            Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_tdMigotoDir)!, true); } catch { }
    }

    [Theory]
    [InlineData(@"..\..\Windows\System32\evil")]
    [InlineData("../../evil")]
    [InlineData(@"C:\Windows\System32\cmd")]
    [InlineData("sub/dir")]
    public async Task DeployVersionAsync_TraversalName_RejectedBeforeFileOps(string versionName)
    {
        var result = await _service.DeployVersionAsync(versionName);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid version name");
    }

    [Fact]
    public async Task DeployVersionAsync_LegitName_PassesGuard()
    {
        // No archive present, so it fails at the archive lookup — proving the guard let the plain name
        // through rather than rejecting it as invalid.
        var result = await _service.DeployVersionAsync("3dmigoto v1.2.3");

        result.Success.Should().BeFalse();
        result.Error.Should().NotContain("Invalid version name");
        result.Error.Should().Contain("not found");
    }
}
