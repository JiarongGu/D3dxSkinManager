using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Quark share resolve flow (SAVE-then-download-then-delete) against canned apiv1 JSON — verifies:
/// pwd_id parse, best-archive pick, the token→detail→save→poll→download URL chain, the 23018
/// size-limit path (cleanup + REMOTE_QUARK_SIZE_LIMIT), not-logged-in, and 401 → account invalidated.
/// No HTTP: IDownloadService is a URL-routing stub. See remote-library.md for the live protocol.
/// </summary>
public class QuarkShareResolverTests
{
    private const string Share = "https://pan.quark.cn/s/abc123";

    private readonly Mock<IDownloadService> _download = new();
    private readonly Mock<IOnlineAccountStore> _accounts = new();
    private readonly QuarkShareResolver _resolver;

    // Per-test overrides for specific endpoints (keyed by a URL fragment); else the happy default.
    private readonly Dictionary<string, string> _getOverrides = new();
    private readonly Dictionary<string, string> _postOverrides = new();

    public QuarkShareResolverTests()
    {
        _accounts.Setup(a => a.Get("quark")).Returns(new OnlineStorageAccount { Provider = "quark", Cookie = "ck=1" });

        _download.Setup(d => d.GetStringAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Returns((string url, IReadOnlyDictionary<string, string>? _, CancellationToken _) => Task.FromResult(RouteGet(url)));
        _download.Setup(d => d.PostJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Returns((string url, string _, IReadOnlyDictionary<string, string>? _, CancellationToken _) => Task.FromResult(RoutePost(url)));

        _resolver = new QuarkShareResolver(_download.Object, _accounts.Object, Mock.Of<ILogHelper>());
    }

    private string RouteGet(string url)
    {
        foreach (var (frag, json) in _getOverrides) if (url.Contains(frag)) return json;
        if (url.Contains("/share/sharepage/detail"))
            return """{"code":0,"data":{"list":[{"dir":false,"fid":"F1","share_fid_token":"TOK1","file_name":"mod_v2.7z","size":5000},{"dir":false,"fid":"F2","share_fid_token":"TOK2","file_name":"readme.txt","size":10}]}}""";
        if (url.Contains("/file/sort")) // drive root listing — app folder already exists
            return """{"code":0,"data":{"list":[{"dir":true,"file_name":"D3dxSkinManager","fid":"APPFID"}]}}""";
        if (url.Contains("/clouddrive/task"))
            return """{"code":0,"data":{"status":2,"save_as":{"save_as_top_fids":["SAVED1"]}}}""";
        throw new InvalidOperationException($"unexpected GET {url}");
    }

    private string RoutePost(string url)
    {
        foreach (var (frag, json) in _postOverrides) if (url.Contains(frag)) return json;
        if (url.Contains("/share/sharepage/token")) return """{"code":0,"data":{"stoken":"ST"}}""";
        if (url.Contains("/share/sharepage/save")) return """{"code":0,"data":{"task_id":"T1"}}""";
        if (url.Contains("/file/download")) return """{"code":0,"data":[{"download_url":"https://cdn.quark/x.7z"}]}""";
        if (url.Contains("/file/delete")) return """{"code":0,"data":{"task_id":"T2"}}""";
        throw new InvalidOperationException($"unexpected POST {url}");
    }

    [Theory]
    [InlineData("https://pan.quark.cn/s/abc123", "abc123")]
    [InlineData("https://pan.quark.cn/s/xyz789/", "xyz789")]
    [InlineData("https://pan.quark.cn/s/qq/list/all", "qq")]
    public void ParsePwdId_ReadsTheShareId(string url, string expected)
    {
        QuarkShareResolver.ParsePwdId(url).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://pan.quark.cn/notashare")]
    [InlineData("not a url")]
    public void ParsePwdId_RejectsNonShareUrls(string url)
    {
        Assert.Throws<OperationException>(() => QuarkShareResolver.ParsePwdId(url));
    }

    [Fact]
    public async Task ResolveAsync_PicksTheLargestArchive()
    {
        var result = await _resolver.ResolveAsync(Share);

        result.FileName.Should().Be("mod_v2.7z", "the archive wins over the .txt");
        result.Size.Should().Be(5000);
    }

    [Fact]
    public async Task PrepareDownloadAsync_SavesPollsAndMintsTheOwnDriveUrl()
    {
        var dl = await _resolver.PrepareDownloadAsync(Share);

        dl.FileName.Should().Be("mod_v2.7z");
        dl.DownloadUrl.Should().Be("https://cdn.quark/x.7z");
        dl.SavedFids.Should().BeEquivalentTo(new[] { "SAVED1" }, "the saved copy is tracked for cleanup");
        dl.Headers.Should().ContainKey("Cookie");
        // The save (转存) call must have fired.
        _download.Verify(d => d.PostJsonAsync(It.Is<string>(u => u.Contains("/share/sharepage/save")),
            It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrepareDownloadAsync_SizeLimit_CleansUpAndThrowsQuarkSizeLimit()
    {
        _postOverrides["/file/download"] = """{"code":23018,"message":"download file size limit"}""";

        var ex = await Assert.ThrowsAsync<OperationException>(() => _resolver.PrepareDownloadAsync(Share));
        ex.Code.Should().Be("REMOTE_QUARK_SIZE_LIMIT");
        // The saved copy must be deleted even though the download was quota-blocked.
        _download.Verify(d => d.PostJsonAsync(It.Is<string>(u => u.Contains("/file/delete")),
            It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Auth_NotLoggedIn_ThrowsQuarkNotLoggedIn()
    {
        _accounts.Setup(a => a.Get("quark")).Returns((OnlineStorageAccount?)null);

        var ex = await Assert.ThrowsAsync<OperationException>(() => _resolver.ResolveAsync(Share));
        ex.Code.Should().Be("QUARK_NOT_LOGGED_IN");
    }

    [Fact]
    public async Task Api401_InvalidatesTheAccountAndThrowsNotLoggedIn()
    {
        _postOverrides["/share/sharepage/token"] = """{"code":401,"message":"unauthorized"}""";

        var ex = await Assert.ThrowsAsync<OperationException>(() => _resolver.ResolveAsync(Share));
        ex.Code.Should().Be("QUARK_NOT_LOGGED_IN");
        _accounts.Verify(a => a.Remove("quark"), Times.Once, "a 401 must invalidate the stored cookie");
    }

    [Fact]
    public async Task CleanupAsync_DeletesSavedCopies()
    {
        await _resolver.CleanupAsync(new[] { "SAVED1" });

        _download.Verify(d => d.PostJsonAsync(It.Is<string>(u => u.Contains("/file/delete")),
            It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
