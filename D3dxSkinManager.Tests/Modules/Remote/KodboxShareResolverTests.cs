using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Tests the kodbox share resolution against a canned share/get reply shaped like the real
/// http://174.136.207.5 (kodbox 1.62) response captured live 2026-07-14. No network.
/// </summary>
public class KodboxShareResolverTests
{
    private sealed class FakeFetcher : IRemotePageFetcher
    {
        public readonly Dictionary<string, string> Gets = new();
        public string FetcherId => "http";

        public Task<string> GetStringAsync(string url, CancellationToken ct = default) =>
            Gets.TryGetValue(url, out var body) ? Task.FromResult(body) : throw new InvalidOperationException("unexpected GET " + url);

        public Task<string> PostJsonAsync(string url, string body, CancellationToken ct = default) =>
            throw new InvalidOperationException("kodbox resolver must not POST JSON: " + url);
    }

    private const string Origin = "http://174.136.207.5";
    private const string Key = "_1I87x0w";
    private const string ShareUrl = Origin + "/#s/" + Key;
    private const string GetUrl = Origin + "/index.php?explorer/share/get&shareID=" + Key;

    private readonly FakeFetcher _fetcher = new();
    private readonly KodboxShareResolver _resolver;

    public KodboxShareResolverTests()
    {
        _resolver = new KodboxShareResolver(_fetcher);
    }

    [Fact]
    public async Task Resolve_FileShare_BuildsTheFileDownloadGetUrl()
    {
        // Real shape: sourceInfo.size is a JSON number, path is the kodbox "{shareItemLink:key}/" token.
        _fetcher.Gets[GetUrl] =
            """
            {"code":true,"data":{"shareHash":"_1I87x0w","title":"mod.zip","isLink":"1",
              "sourceInfo":{"name":"mod.zip","path":"{shareItemLink:_1I87x0w}/","type":"file","size":68455268,"ext":"zip"}}}
            """;

        var result = await _resolver.ResolveAsync(ShareUrl);

        result.FileName.Should().Be("mod.zip");
        result.Size.Should().Be(68455268);
        // path {shareItemLink:_1I87x0w}/ → %7BshareItemLink%3A_1I87x0w%7D%2F (validated live over GET).
        result.DownloadUrl.Should().Be(
            Origin + "/index.php?explorer/share/fileDownload&shareID=_1I87x0w&path=%7BshareItemLink%3A_1I87x0w%7D%2F");
        result.DownloadHeaders.Should().BeNull(); // anonymous — no cookie/UA needed
    }

    [Fact]
    public async Task Resolve_FolderShare_UsesZipDownloadAndAppendsZip()
    {
        _fetcher.Gets[GetUrl] =
            """
            {"code":true,"data":{"title":"My Mod Folder",
              "sourceInfo":{"name":"My Mod Folder","path":"{shareItemLink:_1I87x0w}/","type":"folder","size":0}}}
            """;

        var result = await _resolver.ResolveAsync(ShareUrl);

        result.FileName.Should().Be("My Mod Folder.zip");
        result.Size.Should().Be(0);
        result.DownloadUrl.Should().Contain("explorer/share/zipDownload").And.Contain("shareID=_1I87x0w");
    }

    [Fact]
    public async Task Resolve_UnavailableShare_ThrowsKodboxError()
    {
        _fetcher.Gets[GetUrl] = """{"code":false,"data":"分享不存在"}""";

        var act = () => _resolver.ResolveAsync(ShareUrl);
        var ex = (await act.Should().ThrowAsync<OperationException>()).Which;
        ex.Code.Should().Be("KODBOX_SHARE_UNAVAILABLE");
    }

    [Fact]
    public async Task Resolve_SizeAsString_IsParsed()
    {
        // kodbox often serializes numbers as strings — the resolver must still read the size.
        _fetcher.Gets[GetUrl] =
            """{"code":true,"data":{"title":"m.7z","sourceInfo":{"name":"m.7z","path":"{shareItemLink:_1I87x0w}/","type":"file","size":"12345"}}}""";

        var result = await _resolver.ResolveAsync(ShareUrl);

        result.Size.Should().Be(12345);
    }

    [Fact]
    public void ParseShareUrl_ExtractsOriginAndKey_FromHashRouteAndPath()
    {
        // The real IP/VPN mirror form: key in the fragment hash route.
        KodboxShareResolver.ParseShareUrl("http://174.136.207.5/#s/_1I87x0w")
            .Should().Be(("http://174.136.207.5", "_1I87x0w"));
        // Defensive: a server path form should also parse.
        KodboxShareResolver.ParseShareUrl("http://host.example/s/abcDEF")
            .Should().Be(("http://host.example", "abcDEF"));

        var act = () => KodboxShareResolver.ParseShareUrl("http://host.example/not-a-share");
        act.Should().Throw<OperationException>().Which.Code.Should().Be("REMOTE_RESOLVE_FAILED");
    }
}
