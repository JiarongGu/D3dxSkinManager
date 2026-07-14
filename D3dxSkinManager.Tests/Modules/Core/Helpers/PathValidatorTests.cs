using System.IO;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Modules.Core.Helpers;

/// <summary>
/// Tests for <see cref="PathValidator.IsPathWithin"/> — the reusable path-confinement primitive used to
/// defeat path-traversal from untrusted inputs (package manifest fields, IPC-supplied version names).
/// Pure string logic; these paths need not exist on disk.
/// </summary>
public class PathValidatorTests
{
    private readonly IPathValidator _validator = new PathValidator();

    // A non-existent-but-well-formed absolute root (IsPathWithin never touches disk).
    private static string Root => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "d3dx-pkg-root"));

    [Fact]
    public void NestedFile_IsWithin()
        => _validator.IsPathWithin(Root, Path.Combine(Root, "mods", "a.zip")).Should().BeTrue();

    [Fact]
    public void EqualToRoot_IsWithin()
        => _validator.IsPathWithin(Root, Root).Should().BeTrue();

    [Fact]
    public void TrailingSeparatorOnRoot_StillWithin()
        => _validator.IsPathWithin(Root + Path.DirectorySeparatorChar, Path.Combine(Root, "x")).Should().BeTrue();

    [Fact]
    public void CaseInsensitive_IsWithin()
        => _validator.IsPathWithin(Root.ToUpperInvariant(), Path.Combine(Root.ToLowerInvariant(), "x")).Should().BeTrue();

    [Fact]
    public void ParentTraversal_IsRejected()
        => _validator.IsPathWithin(Root, Path.Combine(Root, "..", "evil")).Should().BeFalse();

    [Fact]
    public void SiblingWithSharedPrefix_IsRejected()
        => _validator.IsPathWithin(Root, Root + "-evil").Should().BeFalse();

    [Fact]
    public void AbsoluteSibling_IsRejected()
        => _validator.IsPathWithin(Root, Path.Combine(Path.GetTempPath(), "somewhere-else", "x")).Should().BeFalse();

    [Fact]
    public void RootedSegmentEscape_IsRejected()
    {
        // Models the attack: Path.Combine(root, "<rooted>") discards root entirely. A manifest field of
        // "C:\Windows\System32" would otherwise resolve outside the package dir.
        var escaped = Path.Combine(Root, Path.Combine(@"C:\Windows", "System32"));
        _validator.IsPathWithin(Root, escaped).Should().BeFalse();
    }

    [Fact]
    public void EmptyRoot_IsRejected()
        => _validator.IsPathWithin("", Path.Combine(Root, "x")).Should().BeFalse();

    [Fact]
    public void EmptyCandidate_IsRejected()
        => _validator.IsPathWithin(Root, "").Should().BeFalse();
}
