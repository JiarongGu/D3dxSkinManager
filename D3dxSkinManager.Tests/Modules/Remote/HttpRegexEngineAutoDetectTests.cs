using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Tests HttpRegexEngine's auto-detect FALLBACK: a download link matching NO static resolver rule is,
/// when the source opts in via AutoDetect, probed and surfaced as the detected type — reusing the
/// same-type rule's Name/password. Guards that it stays OFF when AutoDetect is empty.
/// </summary>
public class HttpRegexEngineAutoDetectTests
{
    private sealed class FakeFetcher : IRemotePageFetcher
    {
        public readonly Dictionary<string, string> Pages = new();
        public string FetcherId => "http";
        public Task<string> GetStringAsync(string url, CancellationToken ct = default) =>
            Task.FromResult(Pages.TryGetValue(url, out var html) ? html : "");
        public Task<string> PostJsonAsync(string url, string body, CancellationToken ct = default) =>
            throw new System.NotSupportedException();
    }

    private sealed class FakeRouter : IRemotePageFetcherRouter
    {
        private readonly IRemotePageFetcher _f;
        public FakeRouter(IRemotePageFetcher f) => _f = f;
        public IRemotePageFetcher For(RemoteSourceConfig config) => _f;
    }

    private const string DetailUrl = "http://site.test/mod/1.html";
    // A Hui盘 mirror at a NEW host with a `/s/<key>` PATH form — the `/#s/` static rule does NOT match it.
    private const string KodboxLink = "http://5.6.7.8/s/newkey";
    private const string Html =
        "<h1>Test Mod</h1>" +
        "<a href=\"" + KodboxLink + "\">Hui盘</a>" +
        "<a href=\"https://ads.xyz/#/register?code=1\">ad</a>";

    private static RemoteSourceConfig Config(params string[] autoDetect) => new()
    {
        BaseUrl = "http://site.test",
        DetailTitlePattern = "<h1>(?<title>[^<]*)</h1>",
        DownloadLinkPattern = "<a href=\"(?<url>https?://[^\"]+)\"",
        Resolvers = new() { new RemoteResolverRule { Match = "/#s/", Type = "kodbox", Name = "Hui盘", UnzipPassword = "huihui" } },
        AutoDetect = autoDetect.ToList(),
    };

    private static HttpRegexEngine Engine(IRemotePageFetcher fetcher, IKodboxHostDetector detector) =>
        new(new FakeRouter(fetcher), detector, Mock.Of<ILogHelper>());

    [Fact]
    public async Task GetDetail_AutoDetectOn_SurfacesTheProbedKodboxHostAsAKodboxOption()
    {
        var fetcher = new FakeFetcher { Pages = { [DetailUrl] = Html } };
        var detector = new Mock<IKodboxHostDetector>();
        detector.Setup(d => d.IsKodboxAsync(KodboxLink, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var detail = await Engine(fetcher, detector.Object).GetDetailAsync(Config("kodbox"), DetailUrl, CancellationToken.None);

        detail.Downloads.Should().HaveCount(1); // the ad link is still dropped
        var opt = detail.Downloads[0];
        opt.Type.Should().Be("kodbox");
        opt.Url.Should().Be(KodboxLink);
        opt.Name.Should().Be("Hui盘");          // borrowed from the same-type static rule
        opt.UnzipPassword.Should().Be("huihui"); // ditto
    }

    [Fact]
    public async Task GetDetail_AutoDetectOff_DropsTheUnmatchedLink_AndNeverProbes()
    {
        var fetcher = new FakeFetcher { Pages = { [DetailUrl] = Html } };
        var detector = new Mock<IKodboxHostDetector>();

        var detail = await Engine(fetcher, detector.Object).GetDetailAsync(Config(/* empty */), DetailUrl, CancellationToken.None);

        detail.Downloads.Should().BeEmpty();
        detector.Verify(d => d.IsKodboxAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ShippedHuihuiSeed_OptsIntoKodboxAutoDetect()
    {
        // Config-level guard: the real shipped json deserializes the new field.
        RemoteBrowseServiceTests.LoadHuihuiSeed().AutoDetect.Should().Contain("kodbox");
    }
}
