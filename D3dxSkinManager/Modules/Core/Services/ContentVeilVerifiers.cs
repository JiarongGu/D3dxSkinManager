namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>What a verification style concluded about one image.</summary>
public enum VerifyOutcome
{
    /// <summary>This style does not apply to this image — try the next style.</summary>
    NotApplicable,
    /// <summary>Explicit content found — veil (the winning <see cref="VerifyResult.Rule"/> is recorded).</summary>
    Sensitive,
    /// <summary>This style is AUTHORITATIVE that the image is safe — later styles do NOT override
    /// (e.g. the dominant-body zoom replaced the pass-1 point evidence and found nothing).</summary>
    Safe,
}

/// <summary>A verification style's conclusion + the matched rule name (telemetry) when it fired.</summary>
public readonly record struct VerifyResult(VerifyOutcome Outcome, string? Rule)
{
    public static readonly VerifyResult NotApplicable = new(VerifyOutcome.NotApplicable, null);
    public static readonly VerifyResult Safe = new(VerifyOutcome.Safe, null);
    public static VerifyResult Sensitive(string rule) => new(VerifyOutcome.Sensitive, rule);
}

/// <summary>
/// A CONTENT-VERIFICATION STYLE — one self-contained way to decide whether an analyzed image is
/// explicit. <see cref="ContentVeilAnalyzer"/> runs the registered styles in <see cref="Order"/>:
/// the first to return <see cref="VerifyOutcome.Sensitive"/> or <see cref="VerifyOutcome.Safe"/>
/// decides; <see cref="VerifyOutcome.NotApplicable"/> defers to the next style, so overall coverage
/// is the UNION of the styles. Add a new style = a new implementation + a DI registration; nothing in
/// the analyzer changes. This is the "more range of verification" seam the content veil is tuned on.
/// </summary>
public interface IContentVerifier
{
    /// <summary>Lower runs first. The precise primary detector is 0; broader-coverage styles are higher
    /// so they only see images the precise styles didn't already decide.</summary>
    int Order { get; }

    /// <summary>A short stable name (telemetry / tests).</summary>
    string Name { get; }

    /// <summary>Judge the analyzed frame. May record zoom telemetry onto <c>frame.Metrics</c>.</summary>
    VerifyResult Verify(VeilFrame frame);
}

/// <summary>
/// PRIMARY style — the point-anatomy detector: paired areola points / a few strong points on an
/// exposed body / the mass rule, plus the dominant-body and fragmented-collage ZOOM passes. This
/// reproduces the original inline verdict exactly (dominant-body zoom REPLACES pass-1 evidence and is
/// authoritative; else the pass-1 rule; else the multi-region collage zoom). When it neither fires
/// nor claims safe (a body with no anatomy signal at these scales), it returns NotApplicable so a
/// broader style can try.
/// </summary>
public sealed class PointAnatomyVerifier : IContentVerifier
{
    public int Order => 0;
    public string Name => "pointAnatomy";

    public VerifyResult Verify(VeilFrame frame)
    {
        var m = frame.Metrics;
        var t = frame.Tuning;
        var g = frame.Grid;

        var largest = frame.FgRegions.FirstOrDefault();
        var contiguity = m.FgSkinRatio > 0 ? m.LargestFgRegion / m.FgSkinRatio : 0;
        if (largest != null &&
            m.LargestFgRegion is >= VeilVision.ZoomMinRegion and <= VeilVision.ZoomMaxRegion &&
            contiguity >= t.ZoomMinContiguity)
        {
            // ONE dominant small-in-frame body: pass-1 points are unreliable at this scale (anatomy
            // below the detector floor; menu panels collide with nude signatures). Crop the body from
            // the ORIGINAL and let body-scale point evidence decide — REPLACING pass-1 evidence, and
            // authoritatively (tried making it additive in the 2026-07-12 recall push — more FP than TP).
            var zoom = VeilVision.ZoomPass(frame.Original, g, largest, t);
            if (zoom != null)
            {
                VeilVision.RecordZoom(m, zoom);
                var zoomRule = VeilVision.PointEvidence(zoom, t, isZoom: true);
                return zoomRule != null ? VerifyResult.Sensitive("zoom:" + zoomRule) : VerifyResult.Safe;
            }
            // Degenerate crop → fall through to the pass-1 rule (matches the original control flow).
        }

        var rule = VeilVision.SensitiveRule(m, t);
        if (rule != null) return VerifyResult.Sensitive(rule);

        if (t.MultiRegionZoomCount > 0 && frame.FgRegions.Count >= 2)
        {
            // FRAGMENTED image (UI collage of mini-portraits): zoom each body-sized region; any crop
            // with point evidence veils. ADDITIVE only — a safe pass-1 verdict stands unless a crop
            // shows anatomy.
            var opaque = Math.Max(1, g.OpaqueCount);
            foreach (var region in frame.FgRegions
                         .Where(r => (double)r.Size / opaque is >= VeilVision.ZoomMinRegion and <= VeilVision.ZoomMaxRegion)
                         .Take(t.MultiRegionZoomCount))
            {
                var zoom = VeilVision.ZoomPass(frame.Original, g, region, t);
                if (zoom == null) continue;
                VeilVision.RecordZoom(m, zoom);
                var r = VeilVision.PointEvidence(zoom, t, isZoom: true);
                if (r != null) return VerifyResult.Sensitive("zoom-multi:" + r);
            }
        }

        return VerifyResult.NotApplicable;
    }
}

/// <summary>
/// SECONDARY style — the large-body CHEST ZOOM. Covers a class the primary structurally cannot reach:
/// a body that FILLS the frame (largest region &gt; <see cref="VeilVision.ZoomMaxRegion"/>, so the
/// dominant-body zoom is skipped for no scale gain) whose areolas are sub-grid at frame scale → the
/// primary sees pts=0 and returns NotApplicable. This style crops just the CHEST BAND of the dominant
/// body and re-scans it at analysis scale, resolving the areolas the frame-scale pass missed. It only
/// ADDS coverage (runs after the primary defers).
///
/// It fires ONLY on a PAIR — the bilateral-symmetry signal — never on the exposedBody (skin-mass)
/// rule. Measured 2026-07-12: on a frame-filling body the exposedBody rule tripped on a suggestive
/// bunny-suit (lots of chest skin → strong points, an FP), while the pair rule recovered a genuine
/// full-frame nude with NO false positive. Pair-only nets a clean +1 recall (38%→41%) at unchanged
/// negatives (83%). See .claude/knowledge/content-veil.md.
/// </summary>
public sealed class ChestBandZoomVerifier : IContentVerifier
{
    public int Order => 100;
    public string Name => "chestZoom";

    public VerifyResult Verify(VeilFrame frame)
    {
        var t = frame.Tuning;
        if (t.ChestZoomMinRegion >= 1.0) return VerifyResult.NotApplicable; // style disabled

        var body = frame.FgRegions.FirstOrDefault();
        if (body == null) return VerifyResult.NotApplicable;

        var opaque = Math.Max(1, frame.Grid.OpaqueCount);
        var frac = (double)body.Size / opaque;
        // Only the LARGE-body class the whole-body zoom skips (frame-filling, no scale gain there). No
        // upper cap: a full-frame nude sits at big≈0.99 — the crop's pair rule (a flat texture sheet
        // yields no enclosed redder points) is what keeps it safe, not a skin-amount ceiling.
        if (frac < t.ChestZoomMinRegion) return VerifyResult.NotApplicable;

        var zoom = VeilVision.ChestBandZoom(frame.Original, frame.Grid, body, t);
        if (zoom == null) return VerifyResult.NotApplicable;
        VeilVision.RecordZoom(frame.Metrics, zoom);
        // PAIR only — the bilateral signal is specific; exposedBody (skin mass) false-positives on
        // frame-filling suggestive bodies (the measured bunny-suit FP).
        var rule = VeilVision.PointEvidence(zoom, t, isZoom: true);
        return rule == "pair" ? VerifyResult.Sensitive("chestzoom:pair") : VerifyResult.NotApplicable;
    }
}
