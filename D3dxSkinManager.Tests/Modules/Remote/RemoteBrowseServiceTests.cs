using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Tests the browse/parse layer against fixture HTML shaped like the real huihui168.org markup
/// (verified live 2026-07-05 — see .claude/rules/remote-library.md). The fetcher is faked, so no
/// network. The seed config's own regexes are exercised directly.
/// </summary>
public class RemoteBrowseServiceTests
{
    private sealed class FakeFetcher : IRemotePageFetcher
    {
        public readonly Dictionary<string, string> Pages = new();
        public readonly List<string> Requested = new();

        public Task<string> GetStringAsync(string url, CancellationToken ct = default)
        {
            Requested.Add(url);
            return Pages.TryGetValue(url, out var html)
                ? Task.FromResult(html)
                : throw new OperationException("DOWNLOAD_FAILED", "url", url);
        }

        public Task<string> PostJsonAsync(string url, string body, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    /// <summary>The SHIPPED huihui adapter (csproj Content → test output) — tests run the real seed's regexes.</summary>
    internal static RemoteSourceConfig LoadHuihuiSeed()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "resources", "remote-sources", "huihui.json");
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        return System.Text.Json.JsonSerializer.Deserialize<RemoteSourceConfig>(System.IO.File.ReadAllText(path), options)!;
    }

    private readonly FakeFetcher _fetcher = new();
    private readonly RemoteBrowseService _service;
    private readonly RemoteSourceConfig _config = LoadHuihuiSeed();

    public RemoteBrowseServiceTests()
    {
        var store = new Mock<IRemoteSourceStore>();
        store.Setup(s => s.GetAll()).Returns(new List<RemoteSourceConfig> { _config });
        store.Setup(s => s.GetById("huihui")).Returns(_config);
        store.Setup(s => s.GetById(It.Is<string>(id => id != "huihui")))
            .Throws(new OperationException("REMOTE_SOURCE_NOT_FOUND", "id", "x"));
        _service = new RemoteBrowseService(store.Object, _fetcher, Mock.Of<ILogHelper>());
    }

    private const string ListHtml = """
        <div class="swiper-slide"><div class="group">
          <a href="/?news_12/2845.html" target="_blank"><img src="/static/upload/image/a.jpg" alt="反虚化3.0" class="w-full"></a>
          <div class="p-3"><h3 class="font-semibold truncate">反虚化3.0</h3></div>
        </div></div>
        <div><a href="/?news_12/2841.html"><img src="/static/upload/image/b.jpg" alt="战斗结算色彩v3.0"></a></div>
        <div><a href="/?news_12/2845.html"><img src="/static/upload/image/a2.jpg" alt=""></a></div>
        <a href="/?list_2/">首页</a><a href="/?list_2_2/">2</a><a href="/?list_2_146/">尾页</a>
        """;

    private const string DetailHtml = """
        <div class="rounded-lg"><h1 class="text-xl font-bold mb-2">反虚化3.0</h1>
        <p><span>Hui盘</span><a href="https://cloudreve.huihui123.org/s/qLzLs9" target="_blank"><span>点击下载</span></a></p>
        <p><span>夸克</span><a href="https://pan.quark.cn/s/e8f5ca9f3add" target="_blank"><span>点击下载</span></a></p>
        <a href="https://wwww.qumianq.xyz/#/register?code=x">VPN广告</a>
        <img src="/static/upload/image/20260621/preview1.jpg"><img src="/static/upload/image/20260621/preview1.jpg">
        <img src="/static/upload/image/20260621/preview2.gif"></div>
        """;

    [Fact]
    public async Task Browse_ExtractsCards_DedupsByUrl_AndFindsTotalPages()
    {
        _fetcher.Pages["https://huihui168.org/?list_2/"] = ListHtml;

        var result = await _service.BrowseAsync("huihui", "2", 1);

        result.Cards.Should().HaveCount(2, "the repeated news_12/2845 anchor is deduped");
        result.Cards[0].Title.Should().Be("反虚化3.0");
        result.Cards[0].DetailUrl.Should().Be("https://huihui168.org/?news_12/2845.html");
        result.Cards[0].ImageUrl.Should().Be("https://huihui168.org/static/upload/image/a.jpg");
        result.TotalPages.Should().Be(146, "the 尾页 anchor holds the last page number");
    }

    [Fact]
    public async Task Browse_PageTwo_UsesThePagedUrlTemplate()
    {
        _fetcher.Pages["https://huihui168.org/?list_2_2/"] = ListHtml;

        var result = await _service.BrowseAsync("huihui", "2", 2);

        result.Page.Should().Be(2);
        _fetcher.Requested.Should().ContainSingle().Which.Should().Be("https://huihui168.org/?list_2_2/");
    }

    [Fact]
    public async Task Search_EncodesTheQuery()
    {
        _fetcher.Pages[$"https://huihui168.org/?keyword={Uri.EscapeDataString("维琳娜")}"] = ListHtml;

        var result = await _service.SearchAsync("huihui", "维琳娜");

        result.Cards.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetDetail_ExtractsTitle_Images_AndOnlyResolverMatchedDownloads()
    {
        _fetcher.Pages["https://huihui168.org/?news_12/2845.html"] = DetailHtml;

        var detail = await _service.GetDetailAsync("huihui", "/?news_12/2845.html");

        detail.Title.Should().Be("反虚化3.0");
        detail.Images.Should().HaveCount(2, "duplicate image URLs are deduped");
        detail.Images[0].Should().StartWith("https://huihui168.org/static/upload/");
        detail.Downloads.Should().HaveCount(2, "the VPN ad anchor matches no resolver rule");
        detail.Downloads[0].Should().BeEquivalentTo(new RemoteDownloadOption
        {
            Name = "Hui盘",
            Url = "https://cloudreve.huihui123.org/s/qLzLs9",
            Type = "cloudreve",
        });
        detail.Downloads[1].Type.Should().Be("external");
    }

    [Fact]
    public async Task GetDetail_RejectsForeignUrls()
    {
        var act = () => _service.GetDetailAsync("huihui", "https://evil.example.com/page");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("REMOTE_FETCH_FAILED");
    }

    [Fact]
    public async Task Browse_FetchFailure_SurfacesRemoteFetchFailed()
    {
        var act = () => _service.BrowseAsync("huihui", "2", 1); // no page registered → fetcher throws
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("REMOTE_FETCH_FAILED");
    }

    [Fact]
    public async Task GetSources_ReportsSearchCapability()
    {
        var sources = await _service.GetSourcesAsync();

        sources.Should().ContainSingle();
        sources[0].Id.Should().Be("huihui");
        sources[0].HasSearch.Should().BeTrue();
        sources[0].Lists.Should().Contain(l => l.Name == "绝区零");
    }
}
