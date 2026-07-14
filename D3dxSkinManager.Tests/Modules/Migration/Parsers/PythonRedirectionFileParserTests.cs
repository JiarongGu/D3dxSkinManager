using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Migration.Parsers;

namespace D3dxSkinManager.Tests.Modules.Migration.Parsers;

/// <summary>
/// The _redirection.ini content is UNTRUSTED (it comes from the Python install being migrated). A folder
/// declaration whose path escapes the source directory (via <c>..</c>) must be rejected — otherwise the
/// parser would enumerate an arbitrary directory. Uses a REAL PathValidator so the confinement is the one
/// production runs.
/// </summary>
public class PythonRedirectionFileParserTests : IDisposable
{
    private readonly string _root;

    public PythonRedirectionFileParserTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "d3dx-redir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public async Task ParseAsync_LoadsInDirFolder_ButRejectsATraversalEscape()
    {
        var baseDir = Path.Combine(_root, "base");
        Directory.CreateDirectory(Path.Combine(baseDir, "sub"));
        File.WriteAllText(Path.Combine(baseDir, "sub", "inside.png"), "x");

        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.png"), "x");

        var ini = Path.Combine(baseDir, "_redirection.ini");
        await File.WriteAllTextAsync(ini, "[*] sub\\*\n[*] ..\\outside\\*\n");

        var image = new Mock<IImageService>();
        image.Setup(i => i.GetSupportedImageExtensions()).Returns(new[] { ".png" });
        var parser = new PythonRedirectionFileParser(image.Object, new PathValidator(), Mock.Of<ILogHelper>());

        var mappings = await parser.ParseAsync(ini);

        mappings.Should().ContainKey("inside");    // in-source folder declaration was honored
        mappings.Should().NotContainKey("secret"); // "..\outside" escape was confined out
    }
}
