using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.System.Models;

namespace D3dxSkinManager.Tests.Modules.SystemModule.Models;

/// <summary>
/// Tests for the pure auto-update manifest diff (added / updated / removed + download size).
/// </summary>
public class ManifestDiffTests
{
    private static UpdateManifest Manifest(params (string path, string sha, long size)[] files)
    {
        var m = new UpdateManifest { Version = "x" };
        foreach (var (path, sha, size) in files)
        {
            m.Files.Add(new ManifestFile { Path = path, Sha256 = sha, Size = size });
        }
        return m;
    }

    [Fact]
    public void Compute_IdenticalManifests_IsEmpty()
    {
        var a = Manifest(("app.exe", "h1", 100), ("data/en.json", "h2", 50));
        var diff = ManifestDiff.Compute(a, a);

        diff.IsEmpty.Should().BeTrue();
        diff.ChangedFileCount.Should().Be(0);
        diff.DownloadSize.Should().Be(0);
    }

    [Fact]
    public void Compute_DetectsAddedUpdatedRemoved()
    {
        var installed = Manifest(
            ("app.exe", "old", 100),     // will update (sha changes)
            ("libs/7z.dll", "z1", 2000), // unchanged
            ("data/old.json", "o1", 30)); // will be removed
        var target = Manifest(
            ("app.exe", "new", 120),     // updated
            ("libs/7z.dll", "z1", 2000), // unchanged
            ("data/new.json", "n1", 40)); // added

        var diff = ManifestDiff.Compute(installed, target);

        diff.Added.Should().ContainSingle(c => c.Path == "data/new.json" && c.Kind == ManifestChangeKind.Added);
        diff.Updated.Should().ContainSingle(c => c.Path == "app.exe" && c.Kind == ManifestChangeKind.Updated);
        diff.Removed.Should().ContainSingle(c => c.Path == "data/old.json" && c.Kind == ManifestChangeKind.Removed);
        diff.ChangedFileCount.Should().Be(3);
        // Download = added (40) + updated (120); removals download nothing.
        diff.DownloadSize.Should().Be(160);
    }

    [Fact]
    public void Compute_PathMatchIsCaseInsensitive()
    {
        var installed = Manifest(("App.exe", "h1", 100));
        var target = Manifest(("app.exe", "h1", 100));

        ManifestDiff.Compute(installed, target).IsEmpty.Should().BeTrue();
    }
}
