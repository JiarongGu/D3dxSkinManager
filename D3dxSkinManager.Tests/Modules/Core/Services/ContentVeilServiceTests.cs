using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace D3dxSkinManager.Tests.Modules.Core.Services;

/// <summary>
/// The content-veil algorithm (v3 — skin + body-shape + explicit-POINT detection) — pins the
/// verdict CONTRACT on synthetic images. The bar: explicit features veil; bare skin AMOUNT alone
/// (an outfit's arms/legs) does NOT:
/// - a shaded skin mass with a PAIR of dark-red enclosed points (nipple-like) → sensitive
/// - the same mass WITHOUT points → safe (an outfit, not nudity)
/// - a full-frame shaded skin mass (close-up body) → sensitive via mass-exposure
/// - a FLAT full-frame skin tone (tan backdrop/wall) → safe (background rejection)
/// - scattered per-pixel skin (texture) → safe
/// Plus the app:// url resolution (?t= strip), the (path, mtime) cache, and InspectAsync metrics.
/// </summary>
public class ContentVeilServiceTests : IDisposable
{
    private static readonly Rgba32 Skin = new(224, 172, 140);
    private static readonly Rgba32 Blue = new(40, 80, 200);
    // Fails both skin rules (too dark for YCbCr luma, r<95 for the RGB rule) but is REDDER
    // (higher Cr) and darker than the surrounding skin — a nipple/areola-like tone.
    private static readonly Rgba32 PointTone = new(90, 25, 45);

    private readonly string _dir;
    private readonly ContentVeilService _service;

    public ContentVeilServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-veil-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.BaseDataPath).Returns(_dir);
        // Real analyzer with both verification styles — the service under test runs the actual
        // detection pipeline (this is the behavior lock for the extraction refactor).
        var analyzer = new ContentVeilAnalyzer(
            new IContentVerifier[] { new PointAnatomyVerifier(), new ChestBandZoomVerifier() },
            Mock.Of<ILogHelper>());
        // Loose registry mock: GetPlugin returns null → no AI plugin, the CV pipeline decides.
        _service = new ContentVeilService(paths.Object, Mock.Of<IRemoteImageProxy>(), analyzer,
            Mock.Of<D3dxSkinManager.Modules.Plugin.Services.IPluginRegistry>(), Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    private string SavePng(string name, int size, Func<int, int, Rgba32> pixel)
    {
        using var image = new Image<Rgba32>(size, size);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                image[x, y] = pixel(x, y);
        var path = Path.Combine(_dir, name);
        image.SaveAsPng(path);
        return path;
    }

    /// <summary>Skin with body-like SHADING (vertical luma gradient, 40%–100% brightness) — stays
    /// inside both skin rules at every level and carries the luma deviation a real body has.</summary>
    private static Rgba32 ShadedSkin(int y, int size)
    {
        var s = 0.4 + 0.6 * y / Math.Max(1, size - 1);
        return new Rgba32((byte)(Skin.R * s), (byte)(Skin.G * s), (byte)(Skin.B * s));
    }

    private static bool InDot(int x, int y, int cx, int cy, int r) =>
        (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

    /// <summary>A 128px frame whose LEFT HALF is a shaded skin mass (~50% fg skin — below the
    /// mass-exposure bar), optionally with two enclosed point-tone dots on one horizontal band.</summary>
    private static Rgba32 TorsoPixel(int x, int y, bool withPoints)
    {
        if (x >= 64) return Blue;
        if (withPoints && (InDot(x, y, 30, 50, 4) || InDot(x, y, 52, 50, 4))) return PointTone;
        return ShadedSkin(y, 128);
    }

    private async Task<string> VerdictOf(string path) =>
        (await _service.CheckAsync(new[] { path }))[path];

    [Fact]
    public async Task SkinMassWithPairedPoints_IsSensitive_ButTheSameMassWithout_IsSafe()
    {
        var explicit_ = SavePng("torso-points.png", 128, (x, y) => TorsoPixel(x, y, withPoints: true));
        var outfit = SavePng("torso-plain.png", 128, (x, y) => TorsoPixel(x, y, withPoints: false));

        (await VerdictOf(explicit_)).Should().Be(ContentVeilService.VerdictSensitive,
            "a pair of enclosed redder-and-darker points on a body is the explicit signal");
        (await VerdictOf(outfit)).Should().Be(ContentVeilService.VerdictSafe,
            "bare skin AMOUNT alone (an outfit's exposed arms/legs) must not veil");
    }

    [Fact]
    public async Task SkinMassAlone_IsSafe_MassRuleDisabledByDefault()
    {
        // The 2026-07-11 corpus sweep DISABLED the mass-exposure rule (0 TPs, only FPs —
        // face close-ups and texture sheets): bare skin amount never veils, only point evidence.
        var nearFull = SavePng("skin.png", 64, (x, y) => y < 8 ? Blue : ShadedSkin(y, 64));
        var sheet = SavePng("sheet.png", 64, (_, y) => ShadedSkin(y, 64));

        (await VerdictOf(nearFull)).Should().Be(ContentVeilService.VerdictSafe);
        (await VerdictOf(sheet)).Should().Be(ContentVeilService.VerdictSafe);
    }

    [Fact]
    public async Task SmallBodyWithPoints_IsCaughtByTheZoomPass()
    {
        // A 1024px frame with a small centered body (score-1.0 point pair) — at the 256 analysis
        // grid the points are ~2px (below the detector floor), so ONLY the zoom pass (crop the
        // dominant small body, re-detect at body scale) can find them.
        var withPoints = SavePng("zoom-body.png", 1024, (x, y) =>
        {
            var inBody = x is >= 392 and < 632 && y is >= 272 and < 752;
            if (!inBody) return Blue;
            if (InDot(x, y, 488, 400, 4) || InDot(x, y, 536, 400, 4)) return PointTone;
            return ShadedSkin(y, 1024);
        });
        var plain = SavePng("zoom-plain.png", 1024, (x, y) =>
            x is >= 392 and < 632 && y is >= 272 and < 752 ? ShadedSkin(y, 1024) : Blue);

        var metrics = await _service.InspectAsync(new[] { withPoints, plain });

        var m = metrics[withPoints]!;
        m.ZoomApplied.Should().BeTrue("one dominant small-in-frame body triggers the zoom pass");
        m.Verdict.Should().Be(ContentVeilService.VerdictSensitive);

        var p = metrics[plain]!;
        p.ZoomApplied.Should().BeTrue();
        p.Verdict.Should().Be(ContentVeilService.VerdictSafe, "the same body without points is an outfit/figure");
    }

    [Fact]
    public async Task FlatSkinToneBackdrop_IsSafe()
    {
        // A solid tan/beige frame-filling area is a WALL/BACKDROP, not a body (border-dominant +
        // tonally flat → background-rejected).
        var path = SavePng("backdrop.png", 64, (_, _) => Skin);
        (await VerdictOf(path)).Should().Be(ContentVeilService.VerdictSafe);
    }

    [Fact]
    public async Task NonSkinImage_IsSafe()
    {
        var path = SavePng("blue.png", 64, (_, _) => Blue);
        (await VerdictOf(path)).Should().Be(ContentVeilService.VerdictSafe);
    }

    [Fact]
    public async Task ScatteredSkinTexture_IsSafe()
    {
        // ~50% skin as a per-pixel checkerboard: every skin pixel is 4-connectivity ISOLATED, so
        // no region clears the speckle floor — noise/texture, not a body.
        var path = SavePng("scattered.png", 64, (x, y) => (x + y) % 2 == 0 ? Skin : Blue);
        (await VerdictOf(path)).Should().Be(ContentVeilService.VerdictSafe);
    }

    // --- Image-review plugin INTERCEPTOR chain (contract v2: the reviewer returns a bool VERDICT
    // and owns its OWN threshold; the host holds no confidence cutoff). ---

    private ContentVeilService BuildServiceWithReviewer(bool? verdict)
    {
        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.BaseDataPath).Returns(_dir);
        var analyzer = new ContentVeilAnalyzer(
            new IContentVerifier[] { new PointAnatomyVerifier(), new ChestBandZoomVerifier() },
            Mock.Of<ILogHelper>());
        var reviewer = new Mock<IImageReviewPlugin>();
        reviewer.Setup(r => r.ReviewImageAsync(It.IsAny<ImageReviewContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(verdict);
        var registry = new Mock<IPluginRegistry>();
        registry.Setup(r => r.GetPlugins<IImageReviewPlugin>()).Returns(new[] { reviewer.Object });
        return new ContentVeilService(paths.Object, Mock.Of<IRemoteImageProxy>(), analyzer,
            registry.Object, Mock.Of<ILogHelper>());
    }

    [Fact]
    public async Task ReviewPlugin_SensitiveVerdict_Veils_EvenWhenCvWouldBeSafe()
    {
        // A flat non-skin frame the CV pipeline calls SAFE — the plugin's SENSITIVE verdict decides.
        var path = SavePng("plugin-true.png", 64, (_, _) => Blue);
        var svc = BuildServiceWithReviewer(verdict: true);

        var m = (await svc.InspectAsync(new[] { path }))[path]!;
        m.Verdict.Should().Be(ContentVeilService.VerdictSensitive, "a reviewer's SENSITIVE verdict replaces the CV verdict");
        m.PluginVerdict.Should().Be(true);
        m.VerdictRule.Should().Be("plugin");
    }

    [Fact]
    public async Task ReviewPlugin_SafeVerdict_DoesNotVeil()
    {
        var path = SavePng("plugin-false.png", 128, (x, y) => TorsoPixel(x, y, withPoints: true));
        var svc = BuildServiceWithReviewer(verdict: false);

        var m = (await svc.InspectAsync(new[] { path }))[path]!;
        m.Verdict.Should().Be(ContentVeilService.VerdictSafe, "a reviewer's SAFE verdict decides alone");
        m.PluginVerdict.Should().Be(false);
    }

    [Fact]
    public async Task ReviewPlugin_Abstains_FallsBackToCvVerdict()
    {
        // null = abstain → the host's CV verdict stands and no plugin verdict is recorded.
        var path = SavePng("plugin-abstain.png", 64, (_, _) => Blue);
        var svc = BuildServiceWithReviewer(verdict: null);

        var m = (await svc.InspectAsync(new[] { path }))[path]!;
        m.PluginVerdict.Should().BeNull("an abstaining reviewer leaves the verdict to the CV pipeline");
        m.Verdict.Should().Be(ContentVeilService.VerdictSafe);
    }

    [Fact]
    public async Task UnresolvableUrl_IsUnknown()
    {
        var missing = Path.Combine(_dir, "missing.png");
        (await VerdictOf(missing)).Should().Be(ContentVeilService.VerdictUnknown);
    }

    [Fact]
    public async Task AppUrl_ResolvesAgainstDataDir_AndStripsCacheBuster()
    {
        SavePng("previews.png", 128, (x, y) => TorsoPixel(x, y, withPoints: true));
        var url = "app://" + Uri.EscapeDataString("previews.png") + "?t=12345";

        var verdicts = await _service.CheckAsync(new[] { url });

        verdicts[url].Should().Be(ContentVeilService.VerdictSensitive);
    }

    [Fact]
    public async Task Verdicts_AreCached_ByPathAndMtime()
    {
        var path = SavePng("cached.png", 128, (x, y) => TorsoPixel(x, y, withPoints: true));
        (await VerdictOf(path)).Should().Be(ContentVeilService.VerdictSensitive);

        // Same mtime → cached verdict even if the bytes were swapped underneath.
        var mtime = File.GetLastWriteTimeUtc(path);
        SavePng("cached.png", 64, (_, _) => Blue);
        File.SetLastWriteTimeUtc(path, mtime);
        (await VerdictOf(path)).Should().Be(ContentVeilService.VerdictSensitive, "the (path, mtime) cache short-circuits");

        // Bumped mtime → re-analyzed.
        File.SetLastWriteTimeUtc(path, mtime.AddSeconds(5));
        (await VerdictOf(path)).Should().Be(ContentVeilService.VerdictSafe);
    }

    [Fact]
    public async Task LabeledImageCorpus_FalsePositives_StayUnderTheRegressionCeiling()
    {
        // devtools/fixtures/veil/{positive|negative}/ — the LOCAL (untracked) user-labeled corpus
        // (see .claude/knowledge/content-veil.md). Absent on CI/fresh clones → test is a no-op.
        // The corpus GROWS during tuning, so this is a REGRESSION ceiling (a code change that
        // explodes false positives goes red), not a hard zero — the operating point is tuned with
        // `node devtools/dev.mjs veil sweep`, which reports the exact confusion.
        var root = FindRepoRoot();
        var negDir = root == null ? null : Path.Combine(root, "devtools", "fixtures", "veil", "negative");
        if (negDir == null || !Directory.Exists(negDir)) return;

        var files = Directory.GetFiles(negDir);
        if (files.Length == 0) return;
        var verdicts = await _service.CheckAsync(files);

        var falsePositives = files.Where(f => verdicts[f] == ContentVeilService.VerdictSensitive).ToList();
        var ratio = (double)falsePositives.Count / files.Length;
        ratio.Should().BeLessThanOrEqualTo(0.25,
            $"labeled-safe images veiling regressed: {string.Join(", ", falsePositives.Select(Path.GetFileName))}");
    }

    private static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir != null; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "devtools"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    [Fact]
    public async Task Inspect_ReturnsMetricsBehindTheVerdict()
    {
        var explicit_ = SavePng("m-points.png", 128, (x, y) => TorsoPixel(x, y, withPoints: true));
        var backdrop = SavePng("m-backdrop.png", 64, (_, _) => Skin);

        var metrics = await _service.InspectAsync(new[] { explicit_, backdrop });

        var m = metrics[explicit_]!;
        m.Verdict.Should().Be(ContentVeilService.VerdictSensitive);
        m.PointCount.Should().BeGreaterThanOrEqualTo(2);
        m.PairedPoints.Should().BeTrue();
        m.FgSkinRatio.Should().BeInRange(0.3, 0.62, "the torso is half the frame — below mass exposure");

        var b = metrics[backdrop]!;
        b.Verdict.Should().Be(ContentVeilService.VerdictSafe);
        b.SkinRatio.Should().BeGreaterThan(0.9, "the naive skin-count signal saturates on a tan backdrop");
        b.FgSkinRatio.Should().Be(0, "background rejection removes the flat border-dominant region");
        b.PointCount.Should().Be(0);
    }
}
