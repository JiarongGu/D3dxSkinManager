using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Tests the kodbox host auto-detector: the share-shaped pre-filter (no probe for ad/social links), the
/// fingerprint check, per-host caching (one probe per origin), and graceful failure. No network.
/// </summary>
public class KodboxHostDetectorTests
{
    private const string KodboxHtml =
        "<html><head><title>kodbox - Powered by kodbox</title>" +
        "<meta name=\"generator\" content=\"kodbox 1.62\"></head><body></body></html>";

    private sealed class CountingFetcher : IRemotePageFetcher
    {
        public readonly Dictionary<string, string> Pages = new();
        public int Calls;
        public string FetcherId => "http";

        public Task<string> GetStringAsync(string url, CancellationToken ct = default)
        {
            Calls++;
            return Pages.TryGetValue(url, out var html) ? Task.FromResult(html) : throw new InvalidOperationException("unreachable " + url);
        }

        public Task<string> PostJsonAsync(string url, string body, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private readonly CountingFetcher _fetcher = new();
    private readonly KodboxHostDetector _detector;

    public KodboxHostDetectorTests()
    {
        _detector = new KodboxHostDetector(_fetcher, Mock.Of<ILogHelper>());
    }

    [Fact]
    public async Task IsKodbox_ShareShapedKodboxHost_ProbesRootAndReturnsTrue()
    {
        _fetcher.Pages["http://1.2.3.4/"] = KodboxHtml;

        (await _detector.IsKodboxAsync("http://1.2.3.4/s/abc")).Should().BeTrue();
        _fetcher.Calls.Should().Be(1);
    }

    [Fact]
    public async Task IsKodbox_NonShareUrl_NeverProbes()
    {
        // Ad/social links (register/signup/#/…) must NOT trigger a network probe.
        (await _detector.IsKodboxAsync("https://ads.xyz/#/register?code=1")).Should().BeFalse();
        (await _detector.IsKodboxAsync("https://t.me/+abcDEF")).Should().BeFalse();
        _fetcher.Calls.Should().Be(0);
    }

    [Fact]
    public async Task IsKodbox_NonKodboxHost_ReturnsFalse()
    {
        _fetcher.Pages["http://9.9.9.9/"] = "<html><head><title>Some Other Site</title></head></html>";

        (await _detector.IsKodboxAsync("http://9.9.9.9/s/key")).Should().BeFalse();
    }

    [Fact]
    public async Task IsKodbox_CachesPerHost_ProbesOnce()
    {
        _fetcher.Pages["http://1.2.3.4/"] = KodboxHtml;

        (await _detector.IsKodboxAsync("http://1.2.3.4/s/aaa")).Should().BeTrue();
        (await _detector.IsKodboxAsync("http://1.2.3.4/s/bbb")).Should().BeTrue();

        _fetcher.Calls.Should().Be(1); // second call served from the per-origin cache
    }

    [Fact]
    public async Task IsKodbox_FetchThrows_ReturnsFalseGracefully()
    {
        // Host not in Pages → fetcher throws → detector swallows and reports not-kodbox.
        (await _detector.IsKodboxAsync("http://unreachable.host/s/key")).Should().BeFalse();
    }
}
