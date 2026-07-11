using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace D3dxSkinManager.Tests.Modules.Core.Services;

/// <summary>
/// The standalone <see cref="ContentVeilAnalyzer"/> + <see cref="IContentVerifier"/> composition —
/// the seam the content-veil detection was extracted onto so verification STYLES can be composed and
/// swept independently. Locks:
/// - the analyzer runs verifiers in <see cref="IContentVerifier.Order"/>, first-fire-wins, an
///   authoritative SAFE stops later styles, all-NotApplicable → safe (fake verifiers);
/// - the secondary <see cref="ChestBandZoomVerifier"/> gating (off knob / small body → deferred) and
///   its positive path (a bilateral pair in the chest band of a large-in-frame body → chestzoom:pair).
/// </summary>
public class ContentVeilAnalyzerTests : IDisposable
{
    private static readonly Rgba32 Skin = new(224, 172, 140);
    private static readonly Rgba32 Blue = new(40, 80, 200);
    private static readonly Rgba32 PointTone = new(90, 25, 45);
    // A strong non-skin areola tone (fails both skin rules; markedly redder + darker than skin).
    private static readonly Rgba32 RedPoint = new(100, 20, 50);

    private readonly string _dir;

    public ContentVeilAnalyzerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-veil-an-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    private static Rgba32 ShadedSkin(int y, int size)
    {
        var s = 0.4 + 0.6 * y / Math.Max(1, size - 1);
        return new Rgba32((byte)(Skin.R * s), (byte)(Skin.G * s), (byte)(Skin.B * s));
    }

    /// <summary>Skin with high LOCAL luma variance (alternating 8-row brightness blocks, both still
    /// inside the skin rules) — a real shaded torso. Unlike a smooth gradient, a cropped CHEST BAND of
    /// this still clears the backdrop rejection (border-dominant + tonally FLAT), so it exercises the
    /// chest-zoom path rather than tripping background rejection on the crop.</summary>
    private static Rgba32 VariedSkin(int y)
    {
        var s = (y / 8) % 2 == 0 ? 0.55 : 1.0;
        return new Rgba32((byte)(Skin.R * s), (byte)(Skin.G * s), (byte)(Skin.B * s));
    }

    private static bool InDot(int x, int y, int cx, int cy, int r) =>
        (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

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

    /// <summary>A canned verification style for testing the analyzer's composition — ignores the
    /// frame and returns a fixed outcome.</summary>
    private sealed class FakeVerifier : IContentVerifier
    {
        public int Order { get; init; }
        public string Name { get; init; } = "fake";
        public VerifyResult Result { get; init; }
        public int Calls { get; private set; }
        public VerifyResult Verify(VeilFrame frame) { Calls++; return Result; }
    }

    private ContentVeilAnalyzer AnalyzerWith(params IContentVerifier[] verifiers) =>
        new(verifiers, Mock.Of<ILogHelper>());

    // A decodable image any composition test can point at (content is irrelevant — the fakes ignore it).
    private string AnyImage() => SavePng("any.png", 32, (_, y) => ShadedSkin(y, 32));

    [Fact]
    public void Verifiers_RunInOrder_FirstSensitiveWins()
    {
        var first = new FakeVerifier { Order = 0, Result = VerifyResult.Sensitive("a") };
        var second = new FakeVerifier { Order = 1, Result = VerifyResult.Sensitive("b") };
        // Registered out of order → the analyzer sorts by Order, so "a" (order 0) decides.
        var analyzer = AnalyzerWith(second, first);

        var r = analyzer.Analyze(AnyImage(), null, regionsOnly: false);

        r.Metrics.Verdict.Should().Be(ContentVeilService.VerdictSensitive);
        r.Metrics.VerdictRule.Should().Be("a");
        second.Calls.Should().Be(0, "a decision short-circuits later styles");
    }

    [Fact]
    public void NotApplicable_FallsThroughToNextStyle()
    {
        var primary = new FakeVerifier { Order = 0, Result = VerifyResult.NotApplicable };
        var secondary = new FakeVerifier { Order = 1, Result = VerifyResult.Sensitive("chestzoom:pair") };
        var analyzer = AnalyzerWith(primary, secondary);

        var r = analyzer.Analyze(AnyImage(), null, regionsOnly: false);

        primary.Calls.Should().Be(1);
        secondary.Calls.Should().Be(1, "the broader style gets a look when the primary defers");
        r.Metrics.VerdictRule.Should().Be("chestzoom:pair");
    }

    [Fact]
    public void AuthoritativeSafe_StopsLaterStyles()
    {
        // The dominant-body zoom replacing pass-1 with nothing returns Safe — a later broad style must
        // NOT override it (that authority is why the zoom's "replace" behavior is safe).
        var primary = new FakeVerifier { Order = 0, Result = VerifyResult.Safe };
        var secondary = new FakeVerifier { Order = 1, Result = VerifyResult.Sensitive("chestzoom:pair") };
        var analyzer = AnalyzerWith(primary, secondary);

        var r = analyzer.Analyze(AnyImage(), null, regionsOnly: false);

        r.Metrics.Verdict.Should().Be(ContentVeilService.VerdictSafe);
        r.Metrics.VerdictRule.Should().BeNull();
        secondary.Calls.Should().Be(0, "an authoritative safe is final");
    }

    [Fact]
    public void AllNotApplicable_IsSafe()
    {
        var analyzer = AnalyzerWith(
            new FakeVerifier { Order = 0, Result = VerifyResult.NotApplicable },
            new FakeVerifier { Order = 1, Result = VerifyResult.NotApplicable });

        var r = analyzer.Analyze(AnyImage(), null, regionsOnly: false);

        r.Metrics.Verdict.Should().Be(ContentVeilService.VerdictSafe);
        r.Metrics.VerdictRule.Should().BeNull();
    }

    [Fact]
    public void RegionsOnly_SkipsVerifiersEntirely()
    {
        // When a review plugin will decide, the analyzer runs stages 1-2 only — no verification style
        // is consulted (they are dead weight; the plugin re-decides).
        var style = new FakeVerifier { Order = 0, Result = VerifyResult.Sensitive("x") };
        var analyzer = AnalyzerWith(style);

        var r = analyzer.Analyze(AnyImage(), null, regionsOnly: true);

        style.Calls.Should().Be(0);
        r.Metrics.Verdict.Should().Be(ContentVeilService.VerdictSafe);
    }

    [Fact]
    public void UndecodableFile_IsUnknown_NeverThrows()
    {
        var analyzer = AnalyzerWith(new FakeVerifier { Order = 0, Result = VerifyResult.Sensitive("x") });
        var bad = Path.Combine(_dir, "not-an-image.png");
        File.WriteAllText(bad, "garbage");

        var r = analyzer.Analyze(bad, null, regionsOnly: false);

        r.Metrics.Verdict.Should().Be(ContentVeilService.VerdictUnknown);
    }

    // ---- ChestBandZoomVerifier (the secondary style) --------------------------------------------

    /// <summary>Build a VeilFrame from an in-memory image (stages 1-2), as the analyzer does.</summary>
    private static VeilFrame FrameFor(Image<Rgba32> image, ContentVeilTuning t)
    {
        var grid = new VeilVision.Grid(image);
        var metrics = new ContentVeilMetrics();
        var fg = VeilVision.FillMetrics(grid, metrics, allowInRegionPairs: false, t);
        return new VeilFrame(image, grid, fg, metrics, t);
    }

    private static Image<Rgba32> Make(int size, Func<int, int, Rgba32> pixel)
    {
        var image = new Image<Rgba32>(size, size);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                image[x, y] = pixel(x, y);
        return image;
    }

    [Fact]
    public void ChestZoom_LargeBodyWithChestPair_FiresPair()
    {
        // A body that FILLS the frame (big≈1.0 > ZoomMaxRegion, so the whole-body zoom is skipped) with
        // a bilateral point pair in the chest band → the chest-zoom recovers it as chestzoom:pair.
        using var img = Make(256, (x, y) =>
            (InDot(x, y, 104, 60, 4) || InDot(x, y, 152, 60, 4)) ? RedPoint : VariedSkin(y));
        var frame = FrameFor(img, ContentVeilTuning.Default);

        var r = new ChestBandZoomVerifier().Verify(frame);

        r.Outcome.Should().Be(VerifyOutcome.Sensitive);
        r.Rule.Should().Be("chestzoom:pair");
    }

    [Fact]
    public void ChestZoom_LargeBodyWithoutPair_Defers()
    {
        // The same frame-filling body with NO enclosed points is an outfit/figure — the pair-only
        // chest-zoom must defer (this is what keeps a frame-filling swimsuit/bunny from false-firing).
        using var img = Make(256, (_, y) => VariedSkin(y));
        var frame = FrameFor(img, ContentVeilTuning.Default);

        new ChestBandZoomVerifier().Verify(frame).Outcome.Should().Be(VerifyOutcome.NotApplicable);
    }

    [Fact]
    public void ChestZoom_Disabled_Defers()
    {
        using var img = Make(256, (x, y) =>
            (InDot(x, y, 104, 60, 4) || InDot(x, y, 152, 60, 4)) ? RedPoint : VariedSkin(y));
        var t = new ContentVeilTuning { ChestZoomMinRegion = 1.0 }; // style off
        var frame = FrameFor(img, t);

        new ChestBandZoomVerifier().Verify(frame).Outcome.Should().Be(VerifyOutcome.NotApplicable);
    }

    [Fact]
    public void ChestZoom_SmallBody_Defers()
    {
        // A small-in-frame body (big well under ChestZoomMinRegion) belongs to the primary's whole-body
        // zoom, not the large-body chest zoom — this style must defer so it never double-counts.
        using var img = Make(256, (x, y) =>
            x is >= 100 and < 140 && y is >= 100 and < 160 ? ShadedSkin(y, 256) : Blue);
        var frame = FrameFor(img, ContentVeilTuning.Default);
        // Sanity: the body really is below the activation floor.
        var big = frame.FgRegions.Count > 0 ? frame.Metrics.LargestFgRegion : 0;
        big.Should().BeLessThan(ContentVeilTuning.Default.ChestZoomMinRegion);

        new ChestBandZoomVerifier().Verify(frame).Outcome.Should().Be(VerifyOutcome.NotApplicable);
    }
}
