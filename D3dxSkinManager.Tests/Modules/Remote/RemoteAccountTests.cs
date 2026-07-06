using System;
using System.Collections.Generic;
using System.IO;
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
/// Online-storage account store + Quark share resolver. The store round-trips to a temp JSON;
/// the resolver's token→detail(recurse)→download flow runs against a fake IDownloadService with
/// canned Quark JSON (the live API is geo-blocked from CI, so the shape is pinned here).
/// </summary>
public class RemoteAccountTests : IDisposable
{
    private readonly string _dir;

    public RemoteAccountTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-acct-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    private OnlineAccountStore NewStore()
    {
        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.GlobalSettingsDirectory).Returns(_dir);
        return new OnlineAccountStore(paths.Object, Mock.Of<ILogHelper>());
    }

    [Fact]
    public void Store_SaveGetRemove_RoundTrips_AndListHidesCookie()
    {
        var store = NewStore();
        store.Get("quark").Should().BeNull();

        store.Save(new OnlineStorageAccount { Provider = "QUARK", DisplayName = "夸克网盘", Cookie = "__puus=abc; kps=xyz" });

        var got = store.Get("quark");
        got.Should().NotBeNull();
        got!.Cookie.Should().Be("__puus=abc; kps=xyz");
        got.Provider.Should().Be("quark", "the key is normalized to lowercase");

        var list = store.List();
        list.Should().ContainSingle();
        list[0].LoggedIn.Should().BeTrue();
        list[0].DisplayName.Should().Be("夸克网盘");
        // The info view carries no cookie field at all (type has none) — nothing to leak.

        // Persisted across a fresh instance (same dir).
        NewStore().Get("quark").Should().NotBeNull();

        store.Remove("quark");
        store.Get("quark").Should().BeNull();
        NewStore().List().Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://pan.quark.cn/s/71e1b2593cde", "71e1b2593cde")]
    [InlineData("https://pan.quark.cn/s/abc123/", "abc123")]
    public void Quark_ParsePwdId_ExtractsShareKey(string url, string expected)
    {
        QuarkShareResolver.ParsePwdId(url).Should().Be(expected);
    }

    [Fact]
    public void Quark_ParsePwdId_RejectsNonShareUrl()
    {
        var act = () => QuarkShareResolver.ParsePwdId("https://pan.quark.cn/list/all");
        act.Should().Throw<OperationException>().Which.Code.Should().Be("REMOTE_RESOLVE_FAILED");
    }

    [Fact]
    public async Task Quark_Resolve_NoAccount_Throws_NotLoggedIn()
    {
        var store = NewStore();
        var resolver = new QuarkShareResolver(Mock.Of<IDownloadService>(), store, Mock.Of<ILogHelper>());

        var act = () => resolver.ResolveAsync("https://pan.quark.cn/s/71e1b2593cde");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("QUARK_NOT_LOGGED_IN");
    }

    [Fact]
    public async Task Quark_Resolve_RecursesFolder_PicksLargestArchive_ReturnsCookiedHeaders()
    {
        var store = NewStore();
        store.Save(new OnlineStorageAccount { Provider = "quark", Cookie = "__puus=sess" });

        // Fake the 3-step API: token → detail(root=folder) → detail(sub=files) → download.
        var download = new Mock<IDownloadService>();
        download.Setup(d => d.PostJsonAsync(It.Is<string>(u => u.Contains("sharepage/token")), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"code":0,"data":{"stoken":"ST0KEN"}}""");
        download.Setup(d => d.GetStringAsync(It.Is<string>(u => u.Contains("pdir_fid=0")), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"code":0,"data":{"list":[{"fid":"DIR1","file_name":"mod","dir":true,"share_fid_token":"DT"}]}}""");
        download.Setup(d => d.GetStringAsync(It.Is<string>(u => u.Contains("pdir_fid=DIR1")), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                {"code":0,"data":{"list":[
                  {"fid":"F_TXT","file_name":"readme.txt","dir":false,"size":10,"share_fid_token":"T1"},
                  {"fid":"F_ZIP","file_name":"skin.7z","dir":false,"size":9000,"share_fid_token":"T2"}
                ]}}
                """);
        download.Setup(d => d.PostJsonAsync(It.Is<string>(u => u.Contains("sharepage/download")), It.Is<string>(b => b.Contains("F_ZIP")), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"code":0,"data":[{"download_url":"https://cdn.quark.cn/dl/skin.7z?sign=x"}]}""");

        var resolver = new QuarkShareResolver(download.Object, store, Mock.Of<ILogHelper>());
        var result = await resolver.ResolveAsync("https://pan.quark.cn/s/71e1b2593cde");

        result.FileName.Should().Be("skin.7z", "the archive is preferred over the .txt");
        result.Size.Should().Be(9000);
        result.DownloadUrl.Should().Be("https://cdn.quark.cn/dl/skin.7z?sign=x");
        result.DownloadHeaders.Should().ContainKey("Cookie");
        result.DownloadHeaders!["Cookie"].Should().Contain("__puus=sess", "the CDN GET needs the session cookie");
    }

    [Fact]
    public async Task Quark_Resolve_ExpiredCookie_NoDownloadUrl_Throws()
    {
        var store = NewStore();
        store.Save(new OnlineStorageAccount { Provider = "quark", Cookie = "__puus=stale" });
        var download = new Mock<IDownloadService>();
        download.Setup(d => d.PostJsonAsync(It.Is<string>(u => u.Contains("token")), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"code":0,"data":{"stoken":"ST"}}""");
        download.Setup(d => d.GetStringAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"code":0,"data":{"list":[{"fid":"F","file_name":"a.7z","dir":false,"size":5,"share_fid_token":"T"}]}}""");
        download.Setup(d => d.PostJsonAsync(It.Is<string>(u => u.Contains("download")), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"code":0,"data":[]}""");

        var resolver = new QuarkShareResolver(download.Object, store, Mock.Of<ILogHelper>());
        var act = () => resolver.ResolveAsync("https://pan.quark.cn/s/x");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("REMOTE_RESOLVE_FAILED");
    }
}
