using System.Collections.Concurrent;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Plugin.Interfaces;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Decides which preview images the CONTENT VEIL covers. This service is pure ORCHESTRATION — url
/// resolution, the (path, mtime, reviewer) verdict cache, batch parallelism, and the image-review
/// plugin INTERCEPTOR chain. The actual image analysis (a pure-CPU, zero-dependency skin/body/point
/// algorithm) lives in the standalone <see cref="IContentVeilAnalyzer"/>, which composes the
/// verification STYLES (<see cref="IContentVerifier"/>) — see ContentVeilAnalyzer.cs.
///
/// The bar the analyzer implements: EXPLICIT content veils; suggestive outfits (swimsuits, bunny
/// suits, bodysuits) do NOT. Tune with the veil-eval harness (devtools/dev.mjs veil), never by feel.
///
/// Verdicts (with their metrics) are cached in-memory keyed by (path, mtime, reviewer-presence).
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

    // ---- Secondary verification style: large-body CHEST ZOOM (2026-07-12) ------------------------
    // Covers full-in-frame bodies the whole-body zoom skips (big > ZoomMaxRegion 0.35) whose areolas
    // are sub-grid at frame scale. Crops the chest BAND ([ChestZoomBandTop, ChestZoomBandBottom] of the
    // dominant region's bbox height, full width) and re-scans at analysis scale. See ChestBandZoomVerifier.

    /// <summary>Min largest-region fraction that activates the chest-zoom style. Defaults to the
    /// whole-body zoom's upper bound (0.35) so the two styles tile the size axis. ≥1.0 = style off.</summary>
    public double ChestZoomMinRegion { get; set; } = 0.35;

    /// <summary>Chest-zoom crop: top of the band as a fraction of the body region's bbox height
    /// (0.08 skips the head/hair — the sweep's best recall point).</summary>
    public double ChestZoomBandTop { get; set; } = 0.08;

    /// <summary>Chest-zoom crop: bottom of the band as a fraction of the body region's bbox height
    /// (the chest sits in the upper torso, so the default keeps the top ~55%).</summary>
    public double ChestZoomBandBottom { get; set; } = 0.55;

    // The plugin sensitivity threshold moved INTO the plugin (contract v2, 2026-07-13): a review
    // plugin returns a bool VERDICT, owning its own cutoff. The host holds no confidence threshold
    // — a detector is tuned/retrained in the PLUGIN repo, not here. See .claude/knowledge/content-veil.md.
}

/// <summary>Raw analysis features behind a verdict (serialized camelCase for the eval harness).</summary>
public class ContentVeilMetrics
{
    public string Verdict { get; set; } = ContentVeilService.VerdictUnknown;

    /// <summary>Which rule produced a SENSITIVE verdict ("pair" | "exposedBody" | "mass",
    /// prefixed "zoom:" / "zoom-multi:" / "chestzoom:" when a zoom style decided). Null for
    /// safe/unknown — tuning telemetry.</summary>
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

    /// <summary>The AI plugin's VERDICT (true=sensitive, false=safe) when a review plugin judged the
    /// image; null when no plugin reviewed / it abstained. Contract v2 — the plugin owns its threshold,
    /// so the host records the verdict, not a confidence.</summary>
    public bool? PluginVerdict { get; set; }

    /// <summary>A ZOOM style ran (a body bbox / chest band was cropped from the original and point
    /// detection re-ran at body scale; its point evidence decides).</summary>
    public bool ZoomApplied { get; set; }

    /// <summary>Point features found by the zoom style(s) (when one ran).</summary>
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

    private const string AppScheme = "app://";
    private const string ProxyImagePrefix = "proxy://image/?u=";

    private readonly IGlobalPathService _globalPaths;
    private readonly IRemoteImageProxy _remoteImages;
    private readonly IContentVeilAnalyzer _analyzer;
    private readonly ILogHelper _logger;

    // (path|mtimeTicks|reviewer) → metrics. Unbounded is fine: entries are tiny and a library has a
    // few thousand previews at most.
    private readonly ConcurrentDictionary<string, ContentVeilMetrics> _cache = new();

    // Optional IMAGE-REVIEW plugins (generic capability — the host knows nothing about the
    // implementations; e.g. the AI detection pack is just one IImageReviewPlugin dropped into
    // {profile}/plugins/). The strongest provider opinion becomes the plugin confidence.
    private readonly Plugin.Services.IPluginRegistry _plugins;

    public ContentVeilService(
        IGlobalPathService globalPaths,
        IRemoteImageProxy remoteImages,
        IContentVeilAnalyzer analyzer,
        Plugin.Services.IPluginRegistry plugins,
        ILogHelper logger)
    {
        _globalPaths = globalPaths;
        _remoteImages = remoteImages;
        _analyzer = analyzer;
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

    /// <summary>Run the image-review INTERCEPTOR chain over the CV result: each registered reviewer
    /// gets the context (path + the CV pass's focus regions + current verdict) and returns a VERDICT
    /// (true=sensitive, false=safe, null=abstain) — the plugin owns its OWN threshold. Any reviewer's
    /// SENSITIVE verdict wins; a verdict, once given, REPLACES the CV verdict (measured: the CV rules
    /// add only false positives on top of the detector). All abstain / none installed → the CV verdict
    /// stands untouched.</summary>
    private async Task ApplyReviewChainAsync(AnalysisResult analysis)
    {
        var metrics = analysis.Metrics;
        ImageReviewContext? context = null;
        bool? verdict = null; // null until a reviewer judges; a SENSITIVE verdict wins over a SAFE one
        foreach (var reviewer in _plugins.GetPlugins<IImageReviewPlugin>())
        {
            context ??= new ImageReviewContext(analysis.Path, metrics.Verdict, analysis.FocusRegions);
            try
            {
                var opinion = await reviewer.ReviewImageAsync(context).ConfigureAwait(false);
                if (opinion == null) continue;                  // abstain — leave the verdict to others / CV
                verdict = (verdict ?? false) || opinion.Value;  // any reviewer's SENSITIVE wins
            }
            catch (Exception ex)
            {
                _logger.Verbose($"[ContentVeil] Image-review plugin '{reviewer.Id}' failed for {analysis.Path}: {ex.Message}", "ContentVeil");
            }
        }
        if (verdict == null) return;

        metrics.PluginVerdict = verdict;
        metrics.VerdictRule = verdict.Value ? "plugin" : null;
        metrics.Verdict = verdict.Value ? VerdictSensitive : VerdictSafe;
    }

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
        // When a review plugin is present it DECIDES the verdict — so tell the analyzer to run only
        // the cheap region scan (stages 1-2) to supply the plugin's focus regions and skip the
        // verification styles. The full CV pipeline runs only as the no-plugin fallback.
        var reviewerPresent = _plugins.GetPlugins<IImageReviewPlugin>().Any();

        if (tuning != null)
        {
            // Tuned (grid-search) analyses never touch the calibrated-verdict cache.
            var tuned = await Task.Run(() => _analyzer.Analyze(path, tuning, reviewerPresent)).ConfigureAwait(false);
            await ApplyReviewChainAsync(tuned).ConfigureAwait(false);
            return tuned.Metrics;
        }

        // Key on reviewer-presence too: a verdict computed by the CV FALLBACK before the AI plugin
        // finished loading (cold-start race — the ~24MB ONNX model loads at profile init) must NOT be
        // served once the plugin is ready. Different key → the plugin re-decides; the stale CV entry is
        // simply never read again.
        var key = $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}|{(reviewerPresent ? "p" : "c")}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var analysis = await Task.Run(() => _analyzer.Analyze(path, null, reviewerPresent)).ConfigureAwait(false);
        await ApplyReviewChainAsync(analysis).ConfigureAwait(false);
        _cache[key] = analysis.Metrics;
        return analysis.Metrics;
    }
}
