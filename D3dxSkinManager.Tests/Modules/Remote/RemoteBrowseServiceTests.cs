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
/// (verified live 2026-07-05 — see .claude/knowledge/remote-library.md). The fetcher is faked, so no
/// network. The seed config's own regexes are exercised directly.
/// </summary>
public class RemoteBrowseServiceTests
{
    private sealed class FakeFetcher : IRemotePageFetcher
    {
        public readonly Dictionary<string, string> Pages = new();
        public readonly List<string> Requested = new();

        public string FetcherId => "http";

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

    /// <summary>Router that always serves the fake fetcher (transport selection isn't under test here).</summary>
    private sealed class FakeRouter : IRemotePageFetcherRouter
    {
        private readonly IRemotePageFetcher _fetcher;
        public FakeRouter(IRemotePageFetcher fetcher) => _fetcher = fetcher;
        public IRemotePageFetcher For(RemoteSourceConfig config) => _fetcher;
    }

    /// <summary>The SHIPPED huihui adapter (csproj Content → test output) — tests run the real seed's regexes.</summary>
    internal static RemoteSourceConfig LoadHuihuiSeed()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "res", "remote-sources", "huihui.json");
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
        // GetOrigins is non-nullable by contract (real impl always builds a fresh dict); the mock must
        // honour that or GetSourcesAsync NREs on origins.GetValueOrDefault (no defensive ?? anymore).
        store.Setup(s => s.GetOrigins()).Returns(new Dictionary<string, string>());
        var router = new FakeRouter(_fetcher);
        var labels = new Mock<IRemoteTagLabelStore>();
        labels.Setup(l => l.GetForSource(It.IsAny<string>(), It.IsAny<Dictionary<string, Dictionary<string, string>>?>()))
            .Returns((string _, Dictionary<string, Dictionary<string, string>>? d) => d ?? new());
        _service = new RemoteBrowseService(store.Object, labels.Object,
            new RemoteSourceResolver(), Mock.Of<IRemoteLibraryStore>(), new IRemoteSiteEngine[]
        {
            new HttpRegexEngine(router, Mock.Of<IKodboxHostDetector>(), Mock.Of<ILogHelper>()),
            new GameBananaEngine(router, Mock.Of<ILogHelper>()),
        });
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

    /// <summary>Shaped like the REAL detail layout (?news_14/9288.html, 2026-07-06): left content
    /// column (lg:w-3/4 — artwork + rich-text body + download anchors) and right sidebar
    /// (lg:w-1/4 — avatar, third-party ad images).</summary>
    private const string SidebarDetailHtml = """
        <div class="w-full lg:w-3/4 pr-0 lg:pr-6">
          <img src="/static/upload/image/20260519/artwork.jpg" alt="陈千语 点墨化龙 完整版" id="main-artwork">
          <h1 class="text-xl font-bold mb-2">陈千语 点墨化龙 完整版</h1>
          <p><span>推荐使用Hui盘下载哦</span></p><p><br></p>
          <p><span>Hui盘：</span><a href="https://cloudreve.huihui123.org/s/gx7F9"><span>点击下载</span></a></p>
          <p><span>夸克：</span><a href="https://pan.quark.cn/s/71e1b2593cde"><span>点击下载</span></a>（解压密码：huihui）</p>
          <p><span>切换键：左右下</span></p>
          <div class="text-xs text-gray-500"></div>
        </div>
        <div class="w-full lg:w-1/4 mt-6 lg:mt-0">
          <img src="/static/upload/image/20260608/avatar.jpg" alt="用户头像">
          <a href="https://wwww.qumianq.xyz/#/register?code=x"><img src="/static/upload/image/20260310/ad1.jpg" alt="ad"></a>
          <img src="/static/upload/other/20250419/ad2.webp" alt="ad2">
        </div>
        """;

    [Fact]
    public async Task GetDetail_ScopedPage_ExcludesSidebarImages_AndExtractsDescription()
    {
        _fetcher.Pages["https://huihui168.org/?news_14/9288.html"] = SidebarDetailHtml;

        var detail = await _service.GetDetailAsync("huihui", "/?news_14/9288.html");

        detail.Title.Should().Be("陈千语 点墨化龙 完整版");
        detail.Images.Should().ContainSingle("the avatar and ad images live in the sidebar, outside the detail scope")
            .Which.Should().Be("https://huihui168.org/static/upload/image/20260519/artwork.jpg");
        detail.Downloads.Should().HaveCount(2, "both download anchors are inside the scoped column");
        detail.Description.Should().Contain("切换键：左右下").And.Contain("解压密码");
        detail.Description.Should().NotContain("<", "tags are stripped to plain text");
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
            UnzipPassword = "huihui", // the seed's site-known 解压密码 rides along on matched options
        });
        detail.Downloads[1].Type.Should().Be("quark", "the huihui seed resolves 夸克 via the Quark share API (needs a saved login)");
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

    [Fact]
    public void DeriveTitleTag_FirstWordBeforeSpace_SkipsUnspacedTitles_AndBadPatterns()
    {
        const string pattern = @"^(?<tag>\S+)\s";
        RemoteBrowseService.DeriveTitleTag(pattern, "陈千语 点墨化龙 完整版").Should().Be("陈千语");
        RemoteBrowseService.DeriveTitleTag(pattern, "反虚化3.0").Should().BeNull("no space → no derivable tag");
        RemoteBrowseService.DeriveTitleTag("(*invalid", "a b").Should().BeNull("a bad user regex must never break browsing");
        RemoteBrowseService.DeriveTitleTag(null, "a b").Should().BeNull();
        RemoteBrowseService.DeriveTitleTag(pattern, null).Should().BeNull();
    }

    [Fact]
    public async Task GetDetail_DerivesTitleTag_FromTheSeedPattern()
    {
        _fetcher.Pages["https://huihui168.org/?news_14/9288.html"] = SidebarDetailHtml;

        var detail = await _service.GetDetailAsync("huihui", "/?news_14/9288.html");

        detail.Tags.Should().ContainSingle("the huihui seed derives the character name from the title")
            .Which.Should().Be("陈千语");
    }

    // ---- test-connection (the authoring feedback loop; reports pass/fail as DATA, never throws) --------

    [Fact]
    public async Task TestConfig_Success_ReportsCards_AndFirstCardDetail()
    {
        _fetcher.Pages["https://huihui168.org/?list_2/"] = ListHtml;
        _fetcher.Pages["https://huihui168.org/?news_12/2845.html"] = DetailHtml;

        var result = await _service.TestConfigAsync(_config, "2");

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.CardCount.Should().Be(2);
        result.SampleTitles.Should().Contain("反虚化3.0");
        result.DetailFetched.Should().BeTrue();
        result.DetailTitle.Should().Be("反虚化3.0");
        result.DetailDownloads.Should().HaveCount(2);
        result.DetailImageCount.Should().Be(2);
    }

    [Fact]
    public async Task TestConfig_ListFetchFails_ReturnsFailure_WithoutThrowing()
    {
        // No page registered → the fetcher throws → the failure is returned as data, not propagated.
        var result = await _service.TestConfigAsync(_config, "2");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.CardCount.Should().Be(0);
        result.DetailFetched.Should().BeFalse();
    }

    [Fact]
    public async Task TestConfig_NoLists_ReturnsFailure_NotThrow()
    {
        var config = new RemoteSourceConfig { Id = "x", Name = "X", BaseUrl = "https://x.example" };

        var result = await _service.TestConfigAsync(config, null);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("lists");
    }
}
