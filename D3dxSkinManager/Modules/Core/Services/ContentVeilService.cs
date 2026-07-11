using System.Collections.Concurrent;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Decides which preview images the CONTENT VEIL covers — a pure-CPU, zero-dependency image
/// algorithm (no ML model, no GPU, nothing added to the bundle; user decision 2026-07-10 after
/// ONNX classifiers either merged "suggestive" into "explicit" or fell apart on 3D game renders).
///
/// The bar it implements: EXPLICIT content veils; suggestive outfits (swimsuits, bunny suits,
/// bodysuits) do NOT. Three stages:
///
/// 1. SKIN detection — per-pixel mask (classic RGB + YCbCr rules).
/// 2. BODY-SHAPE analysis — connected skin regions with per-region features; border-dominant,
///    tonally FLAT regions are rejected as backdrops (tan walls/floors); what remains are body
///    candidates (foreground skin ratio + largest region).
/// 3. EXPLICIT-POINT detection — nipple/areola-like features: small compact blobs ENCLOSED by a
///    large skin region that are REDDER (higher Cr) and not brighter than the surrounding skin.
///    A same-region PAIR of similar points on one horizontal band is the strong signal (eyes are
///    excluded by the redness test — eye whites/pupils pull Cr down; mouths by aspect ratio).
///
/// Verdict: a paired point, a point on a mostly-exposed body, or overwhelming exposed-skin mass.
/// Bare skin AMOUNT alone (arms/legs/midriff of an outfit) never veils — that was the old
/// false-positive class. Tune with the veil-eval harness (devtools/dev.mjs veil), never by feel.
///
/// Verdicts (with their metrics) are cached in-memory keyed by (path, mtime).
/// </summary>
public interface IContentVeilService
{
    /// <summary>
    /// Batch verdicts keyed by the REQUEST url: "sensitive" | "safe" | "unknown" (unresolvable /
    /// undecodable — the UI treats unknown as safe rather than veiling forever).
    /// Accepts the frontend's image urls: app://&lt;encoded-relative-path&gt; and
    /// proxy://image/?u=&lt;encoded-remote-url&gt; (resolved via the remote-image cache).
    /// </summary>
    Task<Dictionary<string, string>> CheckAsync(IReadOnlyList<string> urls);

    /// <summary>Verdicts WITH the raw analysis features. <paramref name="tuning"/> overrides the
    /// calibrated thresholds for THIS call only (grid-search from the veil-eval harness — tuned
    /// calls bypass the verdict cache). Null metrics = unresolvable url.</summary>
    Task<Dictionary<string, ContentVeilMetrics?>> InspectAsync(IReadOnlyList<string> urls, ContentVeilTuning? tuning = null);
}

/// <summary>Tunable detection thresholds (defaults = the calibrated values). The veil-eval
/// harness grid-searches these per-request over the labeled corpus — no rebuild per candidate.
/// Deserialized camelCase from the CONTENT_VEIL_INSPECT payload's optional "tuning".</summary>
public sealed class ContentVeilTuning
{
    public static readonly ContentVeilTuning Default = new();

    /// <summary>In-region anomaly: Cr above the region mean.</summary>
    public double InRegionCrDelta { get; set; } = 8.0;

    /// <summary>In-region anomaly: luma below the region mean (blush is redder but NOT darker).</summary>
    public double InRegionDarkerMargin { get; set; } = 0.0;

    // ---- Anatomical chest-band gate (2026-07-12, from the "shape + ideal proportion" nipple-detection
    // literature) — an areola point only counts if it sits in the CHEST BAND of its body region: the
    // vertical range [ChestBandTop, ChestBandBottom] of the region's bbox. Rejects navels (too low),
    // head/lip reds (too high) and off-position decorations, which is what makes the raw point signal
    // fire as strongly on negatives as positives. Full band (0..1) = gate off.
    // Trained 2026-07-12: a TOP gate (reject reds in the top 8% of the body — head/hair/lips) removed
    // ~3 FP with no recall cost, which is what let the exposedBody thresholds loosen below. A bottom gate
    // hurt (real chest points sit low when the region bbox is a full figure), so it stays open at 1.0.
    public double ChestBandTop { get; set; } = 0.08;
    public double ChestBandBottom { get; set; } = 1.0;

    /// <summary>Both pair members must score at least this.</summary>
    public double PairMinScore { get; set; } = 0.50;

    /// <summary>Pass-1 pair evidence: max HOLE points in the image.</summary>
    public int PairMaxPoints { get; set; } = 5;

    /// <summary>Zoom pair evidence: max HOLE points in the crop (detail surfaces more).</summary>
    public int ZoomPairMaxPoints { get; set; } = 6;

    /// <summary>Only the strongest N in-region anomalies per region may pair.</summary>
    public int TopInRegionPairCandidates { get; set; } = 2;

    /// <summary>Strict (in-region) pair geometry: max size ratio between members.</summary>
    public double StrictPairSizeRatio { get; set; } = 1.3;

    /// <summary>Strict (in-region) pair geometry: max vertical offset in diameters.</summary>
    public double StrictPairDy { get; set; } = 0.6;

    /// <summary>Max horizontal pair separation in diameters.</summary>
    public double PairMaxDxDiameters { get; set; } = 8.0;

    /// <summary>Exposed-body rule: min hole points. Lowered 3→2 (recall sweep 2026-07-12): a strong
    /// nipple pair on a small-in-frame body often surfaces only 2 hole points.</summary>
    public int ExposedBodyMinPoints { get; set; } = 2;

    /// <summary>Exposed-body rule: min hole-point score. Lowered 0.90→0.75→0.60 (the chest-band gate
    /// rejects the off-position FP points, so a lower score bar recovers recall without the FP).</summary>
    public double ExposedBodyMinScore { get; set; } = 0.60;

    /// <summary>Exposed-body rule: min largest-region fraction. Lowered 0.50→0.15→0.12 (chest-band gate
    /// makes it safe): strong-point positives on small-in-frame bodies (big 0.12-0.47) were the biggest
    /// recoverable FN cluster.</summary>
    public double ExposedBodyMinRegion { get; set; } = 0.12;

    /// <summary>Mass-exposure rule: min fg skin. DISABLED by default (2.0) — the 2026-07-11 sweep
    /// found it contributed 0 true positives and only false positives on the labeled corpus;
    /// point evidence carries the verdict. Kept sweepable in case the corpus changes that.</summary>
    public double MassExposureMinFg { get; set; } = 2.0;

    /// <summary>Min contiguity (largest/fg) for the single-body zoom pass.</summary>
    public double ZoomMinContiguity { get; set; } = 0.70;

    /// <summary>FRAGMENTED images (UI collages of mini-portraits): additionally zoom the top-N
    /// body-sized regions and veil if ANY crop shows point evidence (2026-07-11 sweep: +recall,
    /// no FP cost). 0 = off.</summary>
    public int MultiRegionZoomCount { get; set; } = 2;

    /// <summary>Image-review plugin: veil when the max provider confidence reaches this. When a
    /// plugin CAN judge an image it decides ALONE (the 2026-07-11 corpus sweep measured the CV
    /// rules adding only false positives on top of a 100%-recall detector); the CV pipeline is
    /// the fallback for no-plugin installs and images the plugin can't read. Swept 2026-07-11
    /// (72-image corpus, post region-TTA): 0.6 = negatives 95.1% + recall 93.5% — the best fit of
    /// the "100% positives / >95% negatives" target; the residual misses are images the detector
    /// is blind to (already-mosaic'd explicit art scores ~0.09 by design).</summary>
    public double PluginMinConfidence { get; set; } = 0.60;
}

/// <summary>Raw analysis features behind a verdict (serialized camelCase for the eval harness).</summary>
public class ContentVeilMetrics
{
    public string Verdict { get; set; } = ContentVeilService.VerdictUnknown;

    /// <summary>Which rule produced a SENSITIVE verdict ("pair" | "exposedBody" | "mass",
    /// prefixed "zoom:" when the zoom pass decided). Null for safe/unknown — tuning telemetry.</summary>
    public string? VerdictRule { get; set; }

    /// <summary>All skin-toned pixels / opaque pixels (the naive v1 signal, kept for comparison).</summary>
    public double SkinRatio { get; set; }

    /// <summary>Skin ratio after background regions are rejected.</summary>
    public double FgSkinRatio { get; set; }

    /// <summary>Largest non-background connected skin region / opaque pixels (body evidence).</summary>
    public double LargestFgRegion { get; set; }

    /// <summary>Skin ratio rejected as background (border-dominant + tonally flat regions).</summary>
    public double BgSkinRatio { get; set; }

    /// <summary>Connected skin regions ≥ the minimum size (speckle excluded).</summary>
    public int RegionCount { get; set; }

    /// <summary>Explicit-point candidates found as HOLES in the skin mask (non-skin blobs enclosed
    /// by a body region). The verdict's count caps apply to THESE — hole points are rare and
    /// strong. In-region anomaly points are counted separately.</summary>
    public int PointCount { get; set; }

    /// <summary>IN-REGION anomaly points (redder patches whose pixels still pass the skin rules —
    /// subtle areolas). Noisier than holes: they only participate in pair formation.</summary>
    public int InRegionPointCount { get; set; }

    /// <summary>A same-region PAIR of similar points on one horizontal band was found (strong).</summary>
    public bool PairedPoints { get; set; }

    /// <summary>Best point confidence 0-1 (normalized Cr contrast against its skin region).</summary>
    public double MaxPointScore { get; set; }

    /// <summary>The AI plugin's max explicit-part confidence, when the plugin pack is installed.</summary>
    public double? PluginConfidence { get; set; }

    /// <summary>The ZOOM pass ran (one dominant small-in-frame body → the body bbox was cropped
    /// from the original and point detection re-ran at body scale; its point evidence decides).</summary>
    public bool ZoomApplied { get; set; }

    /// <summary>Point features found by the zoom pass (when it ran).</summary>
    public int ZoomPointCount { get; set; }
    public int ZoomInRegionPointCount { get; set; }
    public bool ZoomPaired { get; set; }
    public double ZoomMaxPointScore { get; set; }
}

public class ContentVeilService : IContentVeilService
{
    public const string VerdictSensitive = "sensitive";
    public const string VerdictSafe = "safe";
    public const string VerdictUnknown = "unknown";

    // Analysis grid. 256 keeps nipple-scale features visible on 530px card thumbnails (a ~10px
    // feature is ~5px here) while the full scan stays a few ms.
    private const int AnalysisSize = 256;

    // ---- Stage 2: body-shape thresholds ----------------------------------------------------------
    // Background rejection: a region owning ≥ this share of the image BORDER cells…
    private const double BgBorderFraction = 0.25;
    // …whose luma standard deviation is under this (0-255 scale — tonally flat backdrop).
    private const double BgMaxLumaStdDev = 26.0;
    // Regions smaller than this fraction of opaque pixels are speckle — ignored entirely.
    private const double MinRegionFraction = 0.005;
    // Point detection only runs inside regions at least this large (a body, not a hand).
    private const double MinBodyRegionFraction = 0.04;

    // ---- Stage 3: explicit-point thresholds ------------------------------------------------------
    private const int PointMinPixels = 4;              // smaller = noise at 256px
    private const double PointMaxRegionFraction = 0.035; // anime eyes are LARGER relative to a face
    private const double PointMinFill = 0.45;           // compactness: size / bbox area
    private const double PointMaxAspect = 2.5;          // mouths are wide — excluded
    private const double PointMinCrDelta = 2.0;         // redder than the surrounding skin
    private const double PointMaxLumaDelta = 5.0;       // not brighter than the skin (kills speculars)
    private const double PointEnclosure = 0.70;         // fraction of the blob's border touching ONE skin region
    // ---- Verdict thresholds (calibrated 2026-07-10 on the user-labeled regression set —
    //      devtools/fixtures/veil/{positive,negative}, untracked. The per-request swept knobs
    //      live in ContentVeilTuning; only structural bounds remain consts.)
    // A PAIR's body must be substantial (icon GRIDS pair tiny blobs across mini-portraits —
    // labeled FP at big=0.09).
    private const double PairMinRegion = 0.10;
    // Exposed large body: a few STRONG points on a big skin mass. Upper big cap: a frame that is
    // ~entirely one skin region is a skin-TEXTURE SHEET, not a photo (labeled FP at big=0.98).
    private const int ExposedBodyMinPoints = 2;
    private const int ExposedBodyMaxPoints = 5;
    private const double ExposedBodyMaxRegion = 0.90;
    private const double MassExposureFgSkin = 0.85;     // overwhelming exposed skin… (0.62 veiled
    private const double MassExposureLargestRegion = 0.70; // face close-ups — raised past them)
    private const double MassExposureMaxRegion = 0.90;   // …but ~all-skin = texture sheet, not a body

    // ---- Zoom pass: ONE dominant small-in-frame body → re-detect points at body scale ------------
    // (a standing figure's nipples are 1-2px at 256 grid — below the detector floor; labeled FN).
    // The zoom verdict REPLACES pass-1 point evidence for these images: at body scale true anatomy
    // keeps its geometry while pass-1 pair collisions (menu panels) get re-judged with real detail.
    private const double ZoomMinRegion = 0.03;
    private const double ZoomMaxRegion = 0.35;
    private const double ZoomMinContiguity = 0.70;
    private const double ZoomMargin = 0.10; // bbox margin, fraction of the box size

    private const string AppScheme = "app://";
    private const string ProxyImagePrefix = "proxy://image/?u=";

    private readonly IGlobalPathService _globalPaths;
    private readonly IRemoteImageProxy _remoteImages;
    private readonly ILogHelper _logger;

    // (path|mtimeTicks) → metrics. Unbounded is fine: entries are tiny and a library has a few
    // thousand previews at most.
    private readonly ConcurrentDictionary<string, ContentVeilMetrics> _cache = new();

    // Optional IMAGE-REVIEW plugins (generic capability — the host knows nothing about the
    // implementations; e.g. the AI detection pack is just one IImageReviewPlugin dropped into
    // {profile}/plugins/). The strongest provider opinion becomes the plugin confidence.
    private readonly Plugin.Services.IPluginRegistry _plugins;

    public ContentVeilService(
        IGlobalPathService globalPaths,
        IRemoteImageProxy remoteImages,
        Plugin.Services.IPluginRegistry plugins,
        ILogHelper logger)
    {
        _globalPaths = globalPaths;
        _remoteImages = remoteImages;
        _plugins = plugins;
        _logger = logger;

        // Toggling an image-review plugin on/off flips the verdict logic (plugin decides vs the CV
        // fallback), so drop every cached verdict when the active-plugin set changes — next check
        // recomputes under the new logic. (Both are singletons for the app lifetime; no unsubscribe.)
        _plugins.EnabledChanged += () =>
        {
            _cache.Clear();
            _logger.Info("[ContentVeil] Plugin enabled-change → verdict cache cleared", "ContentVeil");
        };
    }

    /// <summary>Run the image-review INTERCEPTOR chain over the CV result: each registered
    /// reviewer gets the context (path + the CV pass's focus regions + current verdict); the
    /// strongest returned confidence, if any, decides against <c>PluginMinConfidence</c>. All
    /// abstain / none installed → the CV verdict stands untouched.</summary>
    private async Task ApplyReviewChainAsync(AnalysisResult analysis, ContentVeilTuning t)
    {
        var metrics = analysis.Metrics;
        ImageReviewContext? context = null;
        double? best = null;
        foreach (var reviewer in _plugins.GetPlugins<Plugin.Interfaces.IImageReviewPlugin>())
        {
            context ??= new ImageReviewContext(analysis.Path, metrics.Verdict, analysis.FocusRegions);
            try
            {
                var confidence = await reviewer.ReviewImageAsync(context).ConfigureAwait(false);
                if (confidence != null && (best == null || confidence > best)) best = confidence;
            }
            catch (Exception ex)
            {
                _logger.Verbose($"[ContentVeil] Image-review plugin '{reviewer.Id}' failed for {analysis.Path}: {ex.Message}", "ContentVeil");
            }
        }
        if (best == null) return;

        // A reviewer judged the image — measured on the labeled corpus, the CV rules add only
        // false positives on top of a high-recall reviewer, so the chain's opinion REPLACES the
        // CV verdict (CV features stay in the metrics as telemetry).
        metrics.PluginConfidence = best;
        var sensitive = best >= t.PluginMinConfidence;
        metrics.VerdictRule = sensitive ? "plugin" : null;
        metrics.Verdict = sensitive ? VerdictSensitive : VerdictSafe;
    }

    /// <summary>One image's analysis output: the metrics plus the focus regions the CV pass found
    /// (fractional coords — handed to the interceptor chain for detail re-review).</summary>
    private sealed record AnalysisResult(string Path, ContentVeilMetrics Metrics, IReadOnlyList<ImageRegion> FocusRegions);

    public async Task<Dictionary<string, string>> CheckAsync(IReadOnlyList<string> urls)
    {
        var metrics = await InspectAsync(urls).ConfigureAwait(false);
        return metrics.ToDictionary(kv => kv.Key, kv => kv.Value?.Verdict ?? VerdictUnknown);
    }

    // Batch analysis parallelism: the work is CPU-bound decode+scan per image — a page of 40+
    // cards analyzed sequentially took seconds; bounded parallelism cuts it ~6-8×.
    private static readonly int MaxParallelAnalysis = Math.Clamp(Environment.ProcessorCount - 1, 2, 8);

    public async Task<Dictionary<string, ContentVeilMetrics?>> InspectAsync(IReadOnlyList<string> urls, ContentVeilTuning? tuning = null)
    {
        var distinct = urls.Distinct().ToList();
        var result = new ConcurrentDictionary<string, ContentVeilMetrics?>();
        using var gate = new SemaphoreSlim(MaxParallelAnalysis);
        await Task.WhenAll(distinct.Select(async url =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var path = await ResolveToFileAsync(url).ConfigureAwait(false);
                result[url] = path == null ? null : await AnalyzeFileAsync(path, tuning).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Verbose($"[ContentVeil] Check failed for {url}: {ex.Message}", "ContentVeil");
                result[url] = null;
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);
        return new Dictionary<string, ContentVeilMetrics?>(result);
    }

    /// <summary>Map a frontend image url to a local file (mirrors CustomSchemeHandler's resolution).</summary>
    private async Task<string?> ResolveToFileAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (url.StartsWith(ProxyImagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Remote image via the on-demand proxy cache; by the time the UI asks for a verdict the
            // image has rendered, so this is a cache hit (fetch-on-miss is safe + gated regardless).
            var remoteUrl = Uri.UnescapeDataString(url.Substring(ProxyImagePrefix.Length));
            return await _remoteImages.GetOrFetchAsync(remoteUrl).ConfigureAwait(false);
        }

        if (url.StartsWith(AppScheme, StringComparison.OrdinalIgnoreCase))
        {
            var encoded = url.Substring(AppScheme.Length);
            var query = encoded.IndexOf('?');
            if (query >= 0) encoded = encoded.Substring(0, query); // strip ?t= cache-buster
            var relPath = Uri.UnescapeDataString(encoded);
            var resolved = Path.GetFullPath(Path.IsPathRooted(relPath)
                ? relPath
                : Path.Combine(_globalPaths.BaseDataPath, relPath));
            return File.Exists(resolved) ? resolved : null;
        }

        // Bare local path (defensive — callers normally send app:// / proxy:// urls).
        return File.Exists(url) ? url : null;
    }

    private async Task<ContentVeilMetrics?> AnalyzeFileAsync(string path, ContentVeilTuning? tuning = null)
    {
        var t = tuning ?? ContentVeilTuning.Default;
        // When a review plugin is present it DECIDES the verdict — so skip the expensive CV
        // point/zoom passes and run only the cheap region scan (stages 1-2) to supply the plugin's
        // focus regions. The full CV pipeline runs only as the no-plugin fallback.
        var reviewerPresent = _plugins.GetPlugins<Plugin.Interfaces.IImageReviewPlugin>().Any();

        if (tuning != null)
        {
            // Tuned (grid-search) analyses never touch the calibrated-verdict cache.
            var tuned = await Task.Run(() => Analyze(path, tuning, reviewerPresent)).ConfigureAwait(false);
            await ApplyReviewChainAsync(tuned, t).ConfigureAwait(false);
            return tuned.Metrics;
        }

        // Key on reviewer-presence too: a verdict computed by the CV FALLBACK before the AI plugin
        // finished loading (cold-start race — the ~24MB ONNX model loads at profile init) must NOT be
        // served once the plugin is ready. Different key → the plugin re-decides; the stale CV entry is
        // simply never read again.
        var key = $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}|{(reviewerPresent ? "p" : "c")}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var analysis = await Task.Run(() => Analyze(path, null, reviewerPresent)).ConfigureAwait(false);
        await ApplyReviewChainAsync(analysis, t).ConfigureAwait(false);
        _cache[key] = analysis.Metrics;
        return analysis.Metrics;
    }

    private AnalysisResult Analyze(string path, ContentVeilTuning? tuning, bool regionsOnly = false)
    {
        var t = tuning ?? ContentVeilTuning.Default;
        try
        {
            // Decode capped at 1024px (JPEG IDCT scaling — a fraction of a full-res decode for big
            // local previews; card thumbnails ≤530px are unaffected). The zoom pass crops from this
            // "original", so it keeps 4× the analysis grid's detail even for huge sources.
            // Fully-qualified: WinForms' global System.Drawing usings make bare Image/Size ambiguous.
            var decoderOptions = new SixLabors.ImageSharp.Formats.DecoderOptions
            {
                TargetSize = new SixLabors.ImageSharp.Size(1024, 1024),
            };
            using var original = SixLabors.ImageSharp.Image.Load<Rgba32>(decoderOptions, path);
            using var resized = original.Clone(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new SixLabors.ImageSharp.Size(AnalysisSize, AnalysisSize),
            }));

            var grid = new Grid(resized);
            var metrics = new ContentVeilMetrics();
            var fgRegions = FillMetrics(grid, metrics, allowInRegionPairs: false, t, regionsOnly);
            var focusRegions = CollectFocusRegions(grid, fgRegions, t);

            // regionsOnly: a review plugin will decide, so skip the CV point/zoom verdict work and
            // just hand over the focus regions (metrics carry the cheap stage-1/2 features only).
            if (regionsOnly)
            {
                metrics.Verdict = VerdictSafe;
                return new AnalysisResult(path, metrics, focusRegions);
            }

            var largest = fgRegions.FirstOrDefault();
            var contiguity = metrics.FgSkinRatio > 0 ? metrics.LargestFgRegion / metrics.FgSkinRatio : 0;
            if (largest != null &&
                metrics.LargestFgRegion is >= ZoomMinRegion and <= ZoomMaxRegion &&
                contiguity >= t.ZoomMinContiguity)
            {
                // ONE dominant small-in-frame body: pass-1 points are unreliable at this scale
                // (anatomy below the detector floor; menu panels collide with nude signatures).
                // Crop the body from the ORIGINAL and let body-scale point evidence decide.
                var zoom = ZoomPass(original, grid, largest, t);
                if (zoom != null)
                {
                    RecordZoom(metrics, zoom);
                    // Zoom REPLACES pass-1 point evidence for a single dominant body (pass-1 points are
                    // unreliable at that scale). Tried making it additive (veil on pass-1 OR zoom) in the
                    // 2026-07-12 recall push — it added more FP than TP (negatives dropped below 80%), so
                    // the replace behavior stands.
                    var zoomRule = PointEvidence(zoom, t, isZoom: true);
                    metrics.VerdictRule = zoomRule == null ? null : "zoom:" + zoomRule;
                    metrics.Verdict = zoomRule != null ? VerdictSensitive : VerdictSafe;
                    return new AnalysisResult(path, metrics, focusRegions);
                }
            }

            metrics.VerdictRule = SensitiveRule(metrics, t);

            if (metrics.VerdictRule == null && t.MultiRegionZoomCount > 0 && fgRegions.Count >= 2)
            {
                // FRAGMENTED image (UI collage of mini-portraits): zoom each body-sized region;
                // any crop with point evidence veils. ADDITIVE only — a safe pass-1 verdict stands
                // unless a crop shows anatomy.
                var opaque = Math.Max(1, grid.OpaqueCount);
                foreach (var region in fgRegions
                             .Where(r => (double)r.Size / opaque is >= ZoomMinRegion and <= ZoomMaxRegion)
                             .Take(t.MultiRegionZoomCount))
                {
                    var zoom = ZoomPass(original, grid, region, t);
                    if (zoom == null) continue;
                    RecordZoom(metrics, zoom);
                    var rule = PointEvidence(zoom, t, isZoom: true);
                    if (rule != null)
                    {
                        metrics.VerdictRule = "zoom-multi:" + rule;
                        break;
                    }
                }
            }

            metrics.Verdict = metrics.VerdictRule != null ? VerdictSensitive : VerdictSafe;
            return new AnalysisResult(path, metrics, focusRegions);
        }
        catch (Exception ex)
        {
            _logger.Verbose($"[ContentVeil] Could not analyze {path}: {ex.Message}", "ContentVeil");
            return new AnalysisResult(path, new ContentVeilMetrics { Verdict = VerdictUnknown }, Array.Empty<ImageRegion>());
        }
    }

    /// <summary>Focus regions for the interceptor chain: the body-sized foreground regions (small
    /// figures, collage panels) as FRACTIONAL bboxes with a 10% margin — reviewers re-examine
    /// these at detail scale. Capped at 3, largest first.</summary>
    private static IReadOnlyList<ImageRegion> CollectFocusRegions(Grid grid, List<Region> fgRegions, ContentVeilTuning t)
    {
        var opaque = Math.Max(1, grid.OpaqueCount);
        var regions = new List<ImageRegion>();
        foreach (var r in fgRegions)
        {
            if ((double)r.Size / opaque is < ZoomMinRegion or > ZoomMaxRegion) continue;
            var mx = (r.MaxX - r.MinX + 1) * ZoomMargin;
            var my = (r.MaxY - r.MinY + 1) * ZoomMargin;
            var x0 = Math.Max(0, r.MinX - mx);
            var y0 = Math.Max(0, r.MinY - my);
            var x1 = Math.Min(grid.Width, r.MaxX + 1 + mx);
            var y1 = Math.Min(grid.Height, r.MaxY + 1 + my);
            regions.Add(new ImageRegion(x0 / grid.Width, y0 / grid.Height, (x1 - x0) / grid.Width, (y1 - y0) / grid.Height));
            if (regions.Count >= 3) break;
        }
        return regions;
    }

    private static void RecordZoom(ContentVeilMetrics metrics, ContentVeilMetrics zoom)
    {
        metrics.ZoomApplied = true;
        metrics.ZoomPointCount = Math.Max(metrics.ZoomPointCount, zoom.PointCount);
        metrics.ZoomInRegionPointCount = Math.Max(metrics.ZoomInRegionPointCount, zoom.InRegionPointCount);
        metrics.ZoomPaired = metrics.ZoomPaired || zoom.PairedPoints;
        metrics.ZoomMaxPointScore = Math.Max(metrics.ZoomMaxPointScore, zoom.MaxPointScore);
    }

    /// <summary>Re-run stages 1-3 on one body region's crop (10% margin) at full analysis size.
    /// Returns the crop's features, or null when the crop is degenerate.</summary>
    private static ContentVeilMetrics? ZoomPass(SixLabors.ImageSharp.Image<Rgba32> original, Grid grid, Region largest, ContentVeilTuning t)
    {
        var scaleX = (double)original.Width / grid.Width;
        var scaleY = (double)original.Height / grid.Height;
        var mx = (int)((largest.MaxX - largest.MinX + 1) * ZoomMargin * scaleX);
        var my = (int)((largest.MaxY - largest.MinY + 1) * ZoomMargin * scaleY);
        var x0 = Math.Max(0, (int)(largest.MinX * scaleX) - mx);
        var y0 = Math.Max(0, (int)(largest.MinY * scaleY) - my);
        var x1 = Math.Min(original.Width, (int)((largest.MaxX + 1) * scaleX) + mx);
        var y1 = Math.Min(original.Height, (int)((largest.MaxY + 1) * scaleY) + my);
        if (x1 - x0 < 8 || y1 - y0 < 8) return null;

        using var crop = original.Clone(c => c
            .Crop(new SixLabors.ImageSharp.Rectangle(x0, y0, x1 - x0, y1 - y0))
            .Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new SixLabors.ImageSharp.Size(AnalysisSize, AnalysisSize),
            }));

        var zoomMetrics = new ContentVeilMetrics();
        FillMetrics(new Grid(crop), zoomMetrics, allowInRegionPairs: true, t);
        return zoomMetrics;
    }

    /// <summary>Point-based explicit evidence (shared by the pass-1 verdict and the zoom pass —
    /// the mass-exposure rule is deliberately NOT part of this: zoomed faces would trip it).
    /// Returns the matched rule name, or null.</summary>
    private static string? PointEvidence(ContentVeilMetrics m, ContentVeilTuning t, bool isZoom = false)
    {
        // PAIR on a substantial body, few points overall (many = decorated outfit / icon grid;
        // the zoom's extra detail surfaces one more incidental point on real bodies).
        var pairCap = isZoom ? t.ZoomPairMaxPoints : t.PairMaxPoints;
        if (m.PairedPoints && m.PointCount <= pairCap && m.LargestFgRegion >= PairMinRegion)
            return "pair";

        // A few STRONG points on an exposed large body (not a whole-frame texture sheet).
        if (m.PointCount >= t.ExposedBodyMinPoints && m.PointCount <= ExposedBodyMaxPoints &&
            m.MaxPointScore >= t.ExposedBodyMinScore &&
            m.LargestFgRegion >= t.ExposedBodyMinRegion && m.LargestFgRegion <= ExposedBodyMaxRegion)
        {
            return "exposedBody";
        }
        return null;
    }

    /// <summary>Pure function features → verdict for a NON-zoomed image.</summary>
    public static string ComputeVerdict(ContentVeilMetrics m) =>
        SensitiveRule(m, ContentVeilTuning.Default) != null ? VerdictSensitive : VerdictSafe;

    private static string? SensitiveRule(ContentVeilMetrics m, ContentVeilTuning t)
    {
        var rule = PointEvidence(m, t);
        if (rule != null) return rule;

        if (m.FgSkinRatio >= t.MassExposureMinFg &&
            m.LargestFgRegion >= MassExposureLargestRegion && m.LargestFgRegion <= MassExposureMaxRegion)
        {
            return "mass";
        }
        return null;
    }

    // ================================================================================================
    // The analysis pipeline
    // ================================================================================================

    /// <summary>Per-pixel planes extracted in one pass.</summary>
    private sealed class Grid
    {
        public readonly int Width;
        public readonly int Height;
        public readonly bool[] Opaque;
        public readonly bool[] Skin;
        public readonly byte[] Luma;
        public readonly float[] Cr;
        public int OpaqueCount;
        public int SkinCount;

        public Grid(SixLabors.ImageSharp.Image<Rgba32> image)
        {
            Width = image.Width;
            Height = image.Height;
            var n = Width * Height;
            Opaque = new bool[n];
            Skin = new bool[n];
            Luma = new byte[n];
            Cr = new float[n];

            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        var p = row[x];
                        if (p.A < 32) continue; // transparent padding never counts
                        var idx = y * Width + x;
                        Opaque[idx] = true;
                        OpaqueCount++;
                        Luma[idx] = (byte)(0.299 * p.R + 0.587 * p.G + 0.114 * p.B);
                        Cr[idx] = (float)(128 + 0.5 * p.R - 0.418688 * p.G - 0.081312 * p.B);
                        if (IsSkin(p))
                        {
                            Skin[idx] = true;
                            SkinCount++;
                        }
                    }
                }
            });
        }
    }

    private sealed class Region
    {
        public int Id;
        public int Size;
        public int BorderCells;
        public int MinX = int.MaxValue, MaxX = int.MinValue, MinY = int.MaxValue, MaxY = int.MinValue;
        public double LumaSum, LumaSqSum, CrSum;
        public bool Background;
        public double MeanLuma => LumaSum / Size;
        public double MeanCr => CrSum / Size;
        public double LumaStdDev => Math.Sqrt(Math.Max(0, LumaSqSum / Size - MeanLuma * MeanLuma));
    }

    private sealed class PointCandidate
    {
        public int RegionId;
        public double CenterX, CenterY;
        public double Diameter;
        public double Score;
        /// <summary>Found by the in-region anomaly scan (noisier than a hole point).</summary>
        public bool InRegion;
    }

    /// <summary>Run stages 1-3 over a grid, filling the metrics FEATURES (no verdict). Returns the
    /// foreground (non-background) regions, largest first — the zoom passes crop from these.
    /// <paramref name="regionsOnly"/> stops after stage 2 (skin regions) — used when a review
    /// plugin will decide and only the focus regions are needed (skips the costly point stage).</summary>
    private static List<Region> FillMetrics(Grid g, ContentVeilMetrics metrics, bool allowInRegionPairs, ContentVeilTuning t, bool regionsOnly = false)
    {
        if (g.OpaqueCount == 0) return new List<Region>();
        metrics.SkinRatio = (double)g.SkinCount / g.OpaqueCount;

        // ---- Stage 2: label skin regions + classify background vs body --------------------------
        var regionId = new int[g.Width * g.Height]; // 0 = none; ≥1 = skin region id
        var regions = LabelSkinRegions(g, regionId);

        var totalBorderCells = Math.Max(1, 2 * (g.Width + g.Height) - 4);
        var minRegionSize = Math.Max(2, (int)(g.OpaqueCount * MinRegionFraction));
        var fgSkin = 0; var bgSkin = 0;
        var fgRegions = new List<Region>();
        foreach (var r in regions)
        {
            if (r.Size < minRegionSize) continue;
            var borderFraction = (double)r.BorderCells / totalBorderCells;
            // A backdrop owns a big slice of the image frame and is tonally flat. A body/torso is
            // shaded (high luma deviation) even when it touches the border, so it survives.
            r.Background = borderFraction >= BgBorderFraction && r.LumaStdDev <= BgMaxLumaStdDev;
            if (r.Background) { bgSkin += r.Size; continue; }
            metrics.RegionCount++;
            fgSkin += r.Size;
            fgRegions.Add(r);
        }
        fgRegions.Sort((a, b) => b.Size.CompareTo(a.Size));
        metrics.FgSkinRatio = (double)fgSkin / g.OpaqueCount;
        metrics.BgSkinRatio = (double)bgSkin / g.OpaqueCount;
        metrics.LargestFgRegion = (double)(fgRegions.Count > 0 ? fgRegions[0].Size : 0) / g.OpaqueCount;

        if (regionsOnly) return fgRegions; // a plugin decides — the point stage is dead weight here

        // ---- Stage 3: explicit points inside body regions ----------------------------------------
        var bodyMinSize = (int)(g.OpaqueCount * MinBodyRegionFraction);
        var byId = regions.ToDictionary(r => r.Id);
        var points = FindExplicitPoints(g, regionId, byId, bodyMinSize, t);
        metrics.PointCount = points.Count(p => !p.InRegion);
        metrics.InRegionPointCount = points.Count(p => p.InRegion);
        metrics.MaxPointScore = points.Count > 0 ? points.Where(p => !p.InRegion).Select(p => p.Score).DefaultIfEmpty(0).Max() : 0;
        // Pass-1 pair evidence = HOLE pairs only; the zoom pass may additionally pair in-region
        // points (allowInRegionPairs) — at body scale their geometry is trustworthy.
        metrics.PairedPoints = HasPair(points.Where(p => !p.InRegion).ToList(), t)
                               || (allowInRegionPairs && HasPair(points, t));
        return fgRegions;
    }

    /// <summary>
    /// Skin classifier — the union of the classic RGB rule (Kovac) and the YCbCr chroma box,
    /// with the Cb/Cr box widened toward pale/pink anime skin. Both are standard pure-pixel rules.
    /// </summary>
    private static bool IsSkin(Rgba32 p)
    {
        int r = p.R, g = p.G, b = p.B;

        // RGB rule: warm, red-dominant, not gray.
        var rgbRule = r > 95 && g > 40 && b > 20 &&
                      (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b))) > 15 &&
                      Math.Abs(r - g) > 15 && r > g && r > b;

        // YCbCr chroma box (luma-independent — catches shaded and highlighted skin).
        var y = 0.299 * r + 0.587 * g + 0.114 * b;
        var cb = 128 - 0.168736 * r - 0.331264 * g + 0.5 * b;
        var cr = 128 + 0.5 * r - 0.418688 * g - 0.081312 * b;
        var ycbcrRule = y > 60 && cb is >= 80 and <= 135 && cr is >= 133 and <= 177;

        return rgbRule || ycbcrRule;
    }

    /// <summary>Label 4-connected skin regions (iterative BFS) with per-region stats.</summary>
    private static List<Region> LabelSkinRegions(Grid g, int[] regionId)
    {
        var regions = new List<Region>();
        var queue = new Queue<int>();
        for (var start = 0; start < g.Skin.Length; start++)
        {
            if (!g.Skin[start] || regionId[start] != 0) continue;
            var region = new Region { Id = regions.Count + 1 };
            regionId[start] = region.Id;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var idx = queue.Dequeue();
                region.Size++;
                var x = idx % g.Width;
                var y = idx / g.Width;
                region.MinX = Math.Min(region.MinX, x); region.MaxX = Math.Max(region.MaxX, x);
                region.MinY = Math.Min(region.MinY, y); region.MaxY = Math.Max(region.MaxY, y);
                if (x == 0 || y == 0 || x == g.Width - 1 || y == g.Height - 1) region.BorderCells++;
                double l = g.Luma[idx];
                region.LumaSum += l;
                region.LumaSqSum += l * l;
                region.CrSum += g.Cr[idx];
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);

                void Visit(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= g.Width || ny >= g.Height) return;
                    var n = ny * g.Width + nx;
                    if (!g.Skin[n] || regionId[n] != 0) return;
                    regionId[n] = region.Id;
                    queue.Enqueue(n);
                }
            }
            regions.Add(region);
        }
        return regions;
    }

    /// <summary>The point's vertical position within its body region's bbox falls in the CHEST BAND
    /// [ChestBandTop, ChestBandBottom] — the "ideal body proportion" nipple-position gate from the
    /// classical nipple-detection literature. Rejects navel/hem reds (too low) and head/lip reds (too
    /// high). A full band (0..1) disables the gate.</summary>
    private static bool InChestBand(double centerY, Region body, ContentVeilTuning t)
    {
        if (t.ChestBandTop <= 0.0 && t.ChestBandBottom >= 1.0) return true; // gate off
        var h = Math.Max(1, body.MaxY - body.MinY);
        var rel = (centerY - body.MinY) / h;
        return rel >= t.ChestBandTop && rel <= t.ChestBandBottom;
    }

    /// <summary>
    /// Nipple/areola-like candidates: 4-connected NON-skin blobs that are small, compact, mostly
    /// enclosed by ONE large (body) skin region, REDDER than that region (higher mean Cr — eye
    /// whites/pupils pull Cr toward neutral, so eyes fail this) and not brighter than it
    /// (speculars fail). Mouths fail the aspect test.
    /// </summary>
    private static List<PointCandidate> FindExplicitPoints(Grid g, int[] regionId, Dictionary<int, Region> regions, int bodyMinSize, ContentVeilTuning t)
    {
        var points = new List<PointCandidate>();
        var visited = new bool[g.Skin.Length];
        var queue = new Queue<int>();
        var blob = new List<int>();

        for (var start = 0; start < g.Skin.Length; start++)
        {
            if (visited[start] || !g.Opaque[start] || g.Skin[start]) continue;

            // Flood one non-skin blob, tracking bbox + per-neighbor skin-region contact.
            blob.Clear();
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            double lumaSum = 0, crSum = 0;
            var contact = new Dictionary<int, int>(); // skin region id → adjacent cells
            var outsideContact = 0;                    // all non-member neighbor cells
            visited[start] = true;
            queue.Enqueue(start);
            var oversize = false;
            while (queue.Count > 0)
            {
                var idx = queue.Dequeue();
                blob.Add(idx);
                var x = idx % g.Width;
                var y = idx / g.Width;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                lumaSum += g.Luma[idx];
                crSum += g.Cr[idx];
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);

                void Visit(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= g.Width || ny >= g.Height) { outsideContact++; return; }
                    var n = ny * g.Width + nx;
                    if (g.Skin[n])
                    {
                        var id = regionId[n];
                        contact[id] = contact.TryGetValue(id, out var c) ? c + 1 : 1;
                        outsideContact++;
                        return;
                    }
                    if (!g.Opaque[n]) { outsideContact++; return; }
                    if (visited[n]) return;
                    visited[n] = true;
                    // Cheap oversize cut: a huge blob can never be a point — stop growing stats,
                    // but keep flooding so it isn't revisited piecemeal.
                    queue.Enqueue(n);
                }

                if (blob.Count > 4096) oversize = true;
            }
            if (oversize || blob.Count < PointMinPixels) continue;

            // Enclosure: most of the blob's border must touch ONE skin region, and it must be a body.
            if (contact.Count == 0 || outsideContact == 0) continue;
            var dominant = contact.OrderByDescending(kv => kv.Value).First();
            if ((double)dominant.Value / outsideContact < PointEnclosure) continue;
            if (!regions.TryGetValue(dominant.Key, out var body) || body.Background || body.Size < bodyMinSize) continue;

            // Shape: small relative to its body, compact, not elongated.
            if (blob.Count > body.Size * PointMaxRegionFraction) continue;
            var bw = maxX - minX + 1;
            var bh = maxY - minY + 1;
            if ((double)blob.Count / (bw * bh) < PointMinFill) continue;
            if ((double)Math.Max(bw, bh) / Math.Max(1, Math.Min(bw, bh)) > PointMaxAspect) continue;

            // Color contrast vs the surrounding skin: redder, not brighter.
            var meanLuma = lumaSum / blob.Count;
            var meanCr = crSum / blob.Count;
            var crDelta = meanCr - body.MeanCr;
            if (crDelta < PointMinCrDelta) continue;
            if (meanLuma > body.MeanLuma + PointMaxLumaDelta) continue;
            // Anatomical position gate: an areola sits in the body's CHEST BAND, not at the navel/hem or
            // over the head — rejects the off-position reds that make negatives score as high as nudes.
            if (!InChestBand((minY + maxY) / 2.0, body, t)) continue;

            points.Add(new PointCandidate
            {
                RegionId = dominant.Key,
                CenterX = (minX + maxX) / 2.0,
                CenterY = (minY + maxY) / 2.0,
                Diameter = (bw + bh) / 2.0,
                Score = Math.Min(1.0, crDelta / 15.0),
            });
        }

        FindInRegionPoints(g, regionId, regions, bodyMinSize, points, t);
        return points;
    }

    /// <summary>
    /// IN-REGION anomaly points: areola-like patches whose pixels PASS the skin rules, so they are
    /// not holes — markedly REDDER (Cr ≥ region mean + a high bar) and not brighter than their
    /// region's mean. Same shape filters as hole points.
    /// </summary>
    private static void FindInRegionPoints(Grid g, int[] regionId, Dictionary<int, Region> regions, int bodyMinSize, List<PointCandidate> points, ContentVeilTuning t)
    {
        // Candidate mask over BODY-region skin pixels only.
        var candidate = new bool[g.Skin.Length];
        for (var i = 0; i < g.Skin.Length; i++)
        {
            if (!g.Skin[i]) continue;
            if (!regions.TryGetValue(regionId[i], out var r) || r.Background || r.Size < bodyMinSize) continue;
            // Redder AND distinctly darker: anime BLUSH is redder but not darker than the face —
            // areolas are both (blush pairs on zoomed faces were a labeled FP).
            if (g.Cr[i] >= r.MeanCr + t.InRegionCrDelta && g.Luma[i] <= r.MeanLuma - t.InRegionDarkerMargin)
                candidate[i] = true;
        }

        var visited = new bool[g.Skin.Length];
        var queue = new Queue<int>();
        for (var start = 0; start < candidate.Length; start++)
        {
            if (!candidate[start] || visited[start]) continue;

            var size = 0;
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            double crSum = 0;
            var region = regionId[start];
            visited[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var idx = queue.Dequeue();
                size++;
                var x = idx % g.Width;
                var y = idx / g.Width;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                crSum += g.Cr[idx];
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);

                void Visit(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= g.Width || ny >= g.Height) return;
                    var n = ny * g.Width + nx;
                    if (!candidate[n] || visited[n]) return;
                    visited[n] = true;
                    queue.Enqueue(n);
                }
            }

            if (size < PointMinPixels) continue;
            if (!regions.TryGetValue(region, out var body)) continue;
            if (size > body.Size * PointMaxRegionFraction) continue;
            var bw = maxX - minX + 1;
            var bh = maxY - minY + 1;
            if ((double)size / (bw * bh) < PointMinFill) continue;
            if ((double)Math.Max(bw, bh) / Math.Max(1, Math.Min(bw, bh)) > PointMaxAspect) continue;

            var crDelta = crSum / size - body.MeanCr;
            if (!InChestBand((minY + maxY) / 2.0, body, t)) continue;
            points.Add(new PointCandidate
            {
                RegionId = region,
                CenterX = (minX + maxX) / 2.0,
                CenterY = (minY + maxY) / 2.0,
                Diameter = (bw + bh) / 2.0,
                Score = Math.Min(1.0, crDelta / 15.0),
                InRegion = true,
            });
        }
    }

    /// <summary>Two similar points in the SAME body region on one horizontal band, separated by a
    /// plausible chest distance (in point diameters) — the classic paired-nipple signal.
    /// A pair involving IN-REGION points is held to stricter geometry AND both members must be
    /// among their region's STRONGEST in-region anomalies (anatomy outranks blush/shading noise;
    /// decorative patterns produce many equal-strength blobs which dilute the top ranks).</summary>
    private static bool HasPair(List<PointCandidate> points, ContentVeilTuning t)
    {
        // Per-region top-N in-region candidates by score (hole points always qualify).
        var topInRegion = points
            .Where(p => p.InRegion)
            .GroupBy(p => p.RegionId)
            .SelectMany(g => g.OrderByDescending(p => p.Score).Take(t.TopInRegionPairCandidates))
            .ToHashSet();

        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                var a = points[i];
                var b = points[j];
                if (a.RegionId != b.RegionId) continue;
                if (a.Score < t.PairMinScore || b.Score < t.PairMinScore) continue;
                if (a.InRegion && !topInRegion.Contains(a)) continue;
                if (b.InRegion && !topInRegion.Contains(b)) continue;
                var d = (a.Diameter + b.Diameter) / 2.0;
                var strict = a.InRegion || b.InRegion;
                var sizeRatio = Math.Max(a.Diameter, b.Diameter) / Math.Max(1.0, Math.Min(a.Diameter, b.Diameter));
                if (sizeRatio > (strict ? t.StrictPairSizeRatio : 2.0)) continue;
                if (Math.Abs(a.CenterY - b.CenterY) > (strict ? t.StrictPairDy : 1.5) * d) continue;
                var dx = Math.Abs(a.CenterX - b.CenterX);
                if (dx < 1.5 * d || dx > t.PairMaxDxDiameters * d) continue;
                return true;
            }
        }
        return false;
    }
}
