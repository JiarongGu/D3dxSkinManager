using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Tests MEGA FILE-share resolution (mega.nz/file/…): link parse + the single-file `g` API wiring (public
/// handle `p`, no &amp;n=folder). The attr NAME decrypt + CTR are covered by MegaCryptoTests and the live
/// probe (devtools/mega-probe.mjs, validated against a real huihui file link 2026-07-14). No network.
/// </summary>
public class MegaShareResolverFileTests
{
    // 43 base64url 'A's decode to 32 zero bytes — a valid (deterministic) file key. No real key in tests.
    private const string Key = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string Handle = "2rYCnZwa";
    private const string FileLink = "https://mega.nz/file/" + Handle + "#" + Key;

    private sealed class CapturingFetcher : IRemotePageFetcher
    {
        public string? PostUrl, PostBody;
        public string Reply = "";
        public string FetcherId => "http";
        public Task<string> GetStringAsync(string url, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> PostJsonAsync(string url, string body, CancellationToken ct = default)
        {
            PostUrl = url;
            PostBody = body;
            return Task.FromResult(Reply);
        }
    }

    private readonly CapturingFetcher _fetcher = new();
    private readonly MegaShareResolver _resolver;

    public MegaShareResolverFileTests()
    {
        _resolver = new MegaShareResolver(_fetcher);
    }

    [Fact]
    public void ParseFileLink_ExtractsHandleAnd32ByteKey()
    {
        var (handle, key) = MegaShareResolver.ParseFileLink(FileLink);
        handle.Should().Be(Handle);
        key.Should().HaveCount(32);

        // Wrong key length (a folder link's 16-byte key) and a non-file link both reject.
        var badLen = () => MegaShareResolver.ParseFileLink("https://mega.nz/file/x#AAAAAAAAAAAAAAAAAAAAAA");
        badLen.Should().Throw<OperationException>().Which.Code.Should().Be("MEGA_LINK_UNSUPPORTED");
        var notFile = () => MegaShareResolver.ParseFileLink("https://mega.nz/folder/x#AAAAAAAAAAAAAAAAAAAAAA");
        notFile.Should().Throw<OperationException>().Which.Code.Should().Be("MEGA_LINK_UNSUPPORTED");
    }

    [Fact]
    public void IsFileLink_DistinguishesFileFromFolder()
    {
        MegaShareResolver.IsFileLink(FileLink).Should().BeTrue();
        MegaShareResolver.IsFileLink("https://mega.nz/folder/abc#def").Should().BeFalse();
    }

    [Fact]
    public async Task PrepareFileAsync_CallsTopLevelGetWithPublicHandle_AndMapsTheReply()
    {
        _fetcher.Reply = """[{"g":"https://gfs.example/dl/abc","s":53500000}]"""; // no `at` → name falls back to handle

        var file = await _resolver.PrepareFileAsync(FileLink);

        file.Handle.Should().Be(Handle);
        file.DownloadUrl.Should().Be("https://gfs.example/dl/abc");
        file.Size.Should().Be(53500000);
        file.AesKey.Should().HaveCount(16);
        file.Nonce.Should().HaveCount(8);
        file.RelativePath.Should().Be(Handle); // attr missing → handle fallback

        // A FILE `g` is top-level (no &n=folder) with the PUBLIC handle `p`, requesting the URL (g:1).
        _fetcher.PostUrl.Should().NotContain("&n=");
        _fetcher.PostBody.Should().Contain("\"p\":\"" + Handle + "\"").And.Contain("\"g\":1");
    }

    [Fact]
    public async Task ResolveAsync_FileLink_ReturnsNameSizeAndTheLinkAsDownloadUrl()
    {
        _fetcher.Reply = """[{"g":"https://gfs.example/dl/abc","s":42}]""";

        var result = await _resolver.ResolveAsync(FileLink);

        result.FileName.Should().Be(Handle);
        result.Size.Should().Be(42);
        result.DownloadUrl.Should().Be(FileLink); // confirm-UI resolve; the `g` URL is re-fetched at download time
    }

    [Fact]
    public async Task PrepareFileAsync_NoDownloadUrl_Throws()
    {
        _fetcher.Reply = """[{"s":42}]"""; // MEGA returned metadata but no `g` URL

        var act = () => _resolver.PrepareFileAsync(FileLink);
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("REMOTE_RESOLVE_FAILED");
    }
}
