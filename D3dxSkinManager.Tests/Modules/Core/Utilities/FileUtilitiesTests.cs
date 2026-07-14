using System;
using System.IO;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Utilities;

namespace D3dxSkinManager.Tests.Modules.Core.Utilities;

/// <summary>
/// FileUtilities.CopyDirectory is the shared, de-duplicated replacement for the two identical private
/// copies in ModMergeService / ModFixToolService that mapped destinations with a fragile
/// <c>path.Replace(source, dest)</c>. These lock the relative-path contract: every descendant lands at
/// its correct spot under dest, including a subfolder whose name repeats a source path segment, and
/// existing files are overwritten.
/// </summary>
public class FileUtilitiesTests : IDisposable
{
    private readonly string _root;

    public FileUtilitiesTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "d3dx-fileutil-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void CopyDirectory_CopiesNestedTree_PreservingRelativeLayout()
    {
        // Name the source leaf "mod" and nest a child folder ALSO named "mod" — the case a naive
        // source-prefix string.Replace is fragile against.
        var source = Path.Combine(_root, "mod");
        Directory.CreateDirectory(Path.Combine(source, "mod", "textures"));
        File.WriteAllText(Path.Combine(source, "root.ini"), "root");
        File.WriteAllText(Path.Combine(source, "mod", "nested.ini"), "nested");
        File.WriteAllText(Path.Combine(source, "mod", "textures", "tex.dds"), "tex");

        var dest = Path.Combine(_root, "out");
        FileUtilities.CopyDirectory(source, dest);

        File.ReadAllText(Path.Combine(dest, "root.ini")).Should().Be("root");
        File.ReadAllText(Path.Combine(dest, "mod", "nested.ini")).Should().Be("nested");
        File.ReadAllText(Path.Combine(dest, "mod", "textures", "tex.dds")).Should().Be("tex");
    }

    [Fact]
    public void CopyDirectory_OverwritesExistingFiles()
    {
        var source = Path.Combine(_root, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "new");

        var dest = Path.Combine(_root, "dst");
        Directory.CreateDirectory(dest);
        File.WriteAllText(Path.Combine(dest, "a.txt"), "old");

        FileUtilities.CopyDirectory(source, dest);

        File.ReadAllText(Path.Combine(dest, "a.txt")).Should().Be("new");
    }
}
