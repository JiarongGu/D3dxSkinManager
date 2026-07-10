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
/// Tests the Cloudreve v4 share resolution against canned API responses shaped like the real
/// cloudreve.huihui123.org replies (captured live 2026-07-05). No network.
/// </summary>
public class CloudreveShareResolverTests
{
    private sealed class FakeFetcher : IRemotePageFetcher
    {
        public readonly Dictionary<string, string> Gets = new();
        public readonly Dictionary<string, string> Posts = new();
        public string? LastPostBody;

        public string FetcherId => "http";

        public Task<string> GetStringAsync(string url, CancellationToken ct = default) =>
            Gets.TryGetValue(url, out var body) ? Task.FromResult(body) : throw new InvalidOperationException("unexpected GET " + url);

        public Task<string> PostJsonAsync(string url, string body, CancellationToken ct = default)
        {
            LastPostBody = body;
            return Posts.TryGetValue(url, out var reply) ? Task.FromResult(reply) : throw new InvalidOperationException("unexpected POST " + url);
        }
    }

    private const string Origin = "https://cloudreve.huihui123.org";
    private const string ShareUrl = Origin + "/s/qLzLs9";

    private readonly FakeFetcher _fetcher = new();
    private readonly CloudreveShareResolver _resolver;

    public CloudreveShareResolverTests()
    {
        _resolver = new CloudreveShareResolver(_fetcher);
    }

    private void SetupHappyPath(string infoJson = """{"code":0,"data":{"id":"qLzLs9","name":"mod.zip","unlocked":true,"expired":false}}""")
    {
        _fetcher.Gets[$"{Origin}/api/v4/share/info/qLzLs9"] = infoJson;
        _fetcher.Gets[$"{Origin}/api/v4/file?uri=" + Uri.EscapeDataString("cloudreve://qLzLs9@share")] =
            """
            {"code":0,"data":{"files":[
              {"type":1,"id":"d1","name":"folder","path":"cloudreve://qLzLs9@share/folder","size":0},
              {"type":0,"id":"f1","name":"readme.txt","path":"cloudreve://qLzLs9@share/readme.txt","size":999999},
              {"type":0,"id":"f2","name":"mod.zip","path":"cloudreve://qLzLs9@share/mod.zip","size":178748}
            ]}}
            """;
        _fetcher.Posts[$"{Origin}/api/v4/file/url"] =
            """{"code":0,"data":{"urls":[{"url":"https://pan.huihui123.org/uploads/mod.zip?X-Amz-Signature=abc"}]}}""";
    }

    [Fact]
    public async Task Resolve_PicksTheArchive_EvenWhenALargerNonArchiveExists()
    {
        SetupHappyPath();

        var result = await _resolver.ResolveAsync(ShareUrl);

        result.FileName.Should().Be("mod.zip");
        result.Size.Should().Be(178748);
        result.DownloadUrl.Should().StartWith("https://pan.huihui123.org/uploads/mod.zip");
        _fetcher.LastPostBody.Should().Contain("cloudreve://qLzLs9@share/mod.zip").And.Contain("\"download\":true");
    }

    [Fact]
    public async Task Resolve_LockedShare_Throws()
    {
        SetupHappyPath("""{"code":0,"data":{"id":"qLzLs9","name":"x","unlocked":false,"expired":false}}""");
        var act = () => _resolver.ResolveAsync(ShareUrl);
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("REMOTE_SHARE_LOCKED");
    }

    [Fact]
    public async Task Resolve_ExpiredShare_Throws()
    {
        SetupHappyPath("""{"code":0,"data":{"id":"qLzLs9","name":"x","unlocked":true,"expired":true}}""");
        var act = () => _resolver.ResolveAsync(ShareUrl);
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("REMOTE_SHARE_EXPIRED");
    }

    [Fact]
    public async Task Resolve_ApiError_SurfacesTheMessage()
    {
        _fetcher.Gets[$"{Origin}/api/v4/share/info/qLzLs9"] = """{"code":404,"msg":"Shared file does not exist"}""";
        var act = () => _resolver.ResolveAsync(ShareUrl);
        var ex = (await act.Should().ThrowAsync<OperationException>()).Which;
        ex.Code.Should().Be("REMOTE_RESOLVE_FAILED");
    }

    [Fact]
    public void ParseShareUrl_ExtractsOriginAndKey()
    {
        CloudreveShareResolver.ParseShareUrl("https://cloudreve.huihui123.org/s/qLzLs9")
            .Should().Be(("https://cloudreve.huihui123.org", "qLzLs9"));
        CloudreveShareResolver.ParseShareUrl("https://host.example/s/abc/sub%20path")
            .Should().Be(("https://host.example", "abc"));

        var act = () => CloudreveShareResolver.ParseShareUrl("https://host.example/not-a-share");
        act.Should().Throw<OperationException>().Which.Code.Should().Be("REMOTE_RESOLVE_FAILED");
    }
}
