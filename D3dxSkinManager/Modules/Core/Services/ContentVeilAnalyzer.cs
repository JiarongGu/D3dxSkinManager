using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>One image's analysis output: the metrics plus the focus regions the CV pass found
/// (fractional coords — handed to the interceptor chain for detail re-review).</summary>
public sealed record AnalysisResult(string Path, ContentVeilMetrics Metrics, IReadOnlyList<ImageRegion> FocusRegions);

/// <summary>
/// The STANDALONE content-veil detection engine — the pure-CPU, zero-dependency image algorithm,
/// lifted out of <see cref="ContentVeilService"/> so the service is only orchestration (url resolve,
/// cache, batching, plugin interceptor chain) and the vision logic is one testable, composable unit.
///
/// It decodes the image, runs stages 1-2 (skin mask → connected body regions), then hands the frame
/// to an ORDERED list of <see cref="IContentVerifier"/> verification STYLES. Each style is a distinct
/// way to spot explicit content (the primary point-anatomy detector, the large-body chest zoom, …);
/// the first to fire decides, a style may also claim an authoritative SAFE, and un-fired styles fall
/// through to the next — so coverage is the UNION of the styles. Adding a new detection style is a new
/// <see cref="IContentVerifier"/> registration, nothing here changes.
/// </summary>
public interface IContentVeilAnalyzer
{
    /// <summary>Decode + analyze one file. <paramref name="regionsOnly"/> stops after stage 2 (a
    /// review plugin will decide the verdict and only needs the focus regions — the point/zoom
    /// verification styles are skipped as dead weight). Never throws — a decode failure returns an
    /// <see cref="ContentVeilService.VerdictUnknown"/> result.</summary>
    AnalysisResult Analyze(string path, ContentVeilTuning? tuning, bool regionsOnly);
}

public sealed class ContentVeilAnalyzer : IContentVeilAnalyzer
{
    private readonly IReadOnlyList<IContentVerifier> _verifiers;
    private readonly ILogHelper _logger;

    public ContentVeilAnalyzer(IEnumerable<IContentVerifier> verifiers, ILogHelper logger)
    {
        // Deterministic order: the precise primary style runs first; broader-coverage styles only
        // get a look when the earlier ones neither fired nor claimed an authoritative SAFE.
        _verifiers = verifiers.OrderBy(v => v.Order).ToList();
        _logger = logger;
    }

    public AnalysisResult Analyze(string path, ContentVeilTuning? tuning, bool regionsOnly)
    {
        var t = tuning ?? ContentVeilTuning.Default;
        try
        {
            // Decode capped at 1024px (JPEG IDCT scaling — a fraction of a full-res decode for big
            // local previews; card thumbnails ≤530px are unaffected). The zoom styles crop from this
            // "original", so they keep 4× the analysis grid's detail even for huge sources.
            // Fully-qualified: WinForms' global System.Drawing usings make bare Image/Size ambiguous.
            var decoderOptions = new SixLabors.ImageSharp.Formats.DecoderOptions
            {
                TargetSize = new SixLabors.ImageSharp.Size(1024, 1024),
            };
            using var original = SixLabors.ImageSharp.Image.Load<Rgba32>(decoderOptions, path);
            using var resized = original.Clone(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new SixLabors.ImageSharp.Size(VeilVision.AnalysisSize, VeilVision.AnalysisSize),
            }));

            var grid = new VeilVision.Grid(resized);
            var metrics = new ContentVeilMetrics();
            var fgRegions = VeilVision.FillMetrics(grid, metrics, allowInRegionPairs: false, t, regionsOnly);
            var focusRegions = VeilVision.CollectFocusRegions(grid, fgRegions, t);

            // regionsOnly: a review plugin will decide, so skip the verification styles and just hand
            // over the focus regions (metrics carry the cheap stage-1/2 features only).
            if (regionsOnly)
            {
                metrics.Verdict = ContentVeilService.VerdictSafe;
                return new AnalysisResult(path, metrics, focusRegions);
            }

            var frame = new VeilFrame(original, grid, fgRegions, metrics, t);
            foreach (var verifier in _verifiers)
            {
                var r = verifier.Verify(frame);
                if (r.Outcome == VerifyOutcome.Sensitive)
                {
                    metrics.VerdictRule = r.Rule;
                    metrics.Verdict = ContentVeilService.VerdictSensitive;
                    return new AnalysisResult(path, metrics, focusRegions);
                }
                if (r.Outcome == VerifyOutcome.Safe)
                {
                    // The style is authoritative for this image (e.g. the dominant-body zoom replaced
                    // pass-1 point evidence and found nothing) — later styles don't get to override.
                    metrics.VerdictRule = null;
                    metrics.Verdict = ContentVeilService.VerdictSafe;
                    return new AnalysisResult(path, metrics, focusRegions);
                }
                // NotApplicable → the next (broader) style gets a look.
            }

            metrics.VerdictRule = null;
            metrics.Verdict = ContentVeilService.VerdictSafe;
            return new AnalysisResult(path, metrics, focusRegions);
        }
        catch (Exception ex)
        {
            _logger.Verbose($"[ContentVeil] Could not analyze {path}: {ex.Message}", "ContentVeil");
            return new AnalysisResult(path, new ContentVeilMetrics { Verdict = ContentVeilService.VerdictUnknown }, Array.Empty<ImageRegion>());
        }
    }
}

/// <summary>One decoded image, ready for the verification styles: the ORIGINAL (crop source for the
/// zoom styles), the 256px analysis <see cref="Grid"/>, the foreground (body) regions largest-first,
/// the metrics being filled, and the active tuning. Verifiers read the features and may record zoom
/// telemetry onto <see cref="Metrics"/>.</summary>
public sealed class VeilFrame
{
    public VeilFrame(SixLabors.ImageSharp.Image<Rgba32> original, VeilVision.Grid grid,
        List<VeilVision.Region> fgRegions, ContentVeilMetrics metrics, ContentVeilTuning tuning)
    {
        Original = original;
        Grid = grid;
        FgRegions = fgRegions;
        Metrics = metrics;
        Tuning = tuning;
    }

    public SixLabors.ImageSharp.Image<Rgba32> Original { get; }
    public VeilVision.Grid Grid { get; }
    public List<VeilVision.Region> FgRegions { get; }
    public ContentVeilMetrics Metrics { get; }
    public ContentVeilTuning Tuning { get; }
}

/// <summary>
/// The pure image-vision primitives shared by every verification style — decode-independent, static,
/// side-effect-free. Stages: skin mask (RGB Kovac ∪ YCbCr), connected body regions with per-region
/// stats + backdrop rejection, explicit-point detection (hole + in-region anomaly), the pairing test,
/// the point-evidence rule, and the zoom crops. Lifted verbatim from the original ContentVeilService.
/// </summary>
public static class VeilVision
{
    // Analysis grid. 256 keeps nipple-scale features visible on 530px card thumbnails (a ~10px
    // feature is ~5px here) while the full scan stays a few ms.
    public const int AnalysisSize = 256;

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

    // ---- Verdict thresholds (calibrated on the user-labeled regression set —
    //      devtools/fixtures/veil/{positive,negative}, untracked. The per-request swept knobs
    //      live in ContentVeilTuning; only structural bounds remain consts.)
    // A PAIR's body must be substantial (icon GRIDS pair tiny blobs across mini-portraits —
    // labeled FP at big=0.09).
    public const double PairMinRegion = 0.10;
    // Exposed large body: a few STRONG points on a big skin mass. Upper big cap: a frame that is
    // ~entirely one skin region is a skin-TEXTURE SHEET, not a photo (labeled FP at big=0.98).
    public const int ExposedBodyMaxPoints = 5;
    public const double ExposedBodyMaxRegion = 0.90;
    public const double MassExposureLargestRegion = 0.70; // face close-ups — raised past them
    public const double MassExposureMaxRegion = 0.90;   // …but ~all-skin = texture sheet, not a body

    // ---- Zoom: ONE dominant small-in-frame body → re-detect points at body scale -----------------
    // (a standing figure's nipples are 1-2px at 256 grid — below the detector floor; labeled FN).
    public const double ZoomMinRegion = 0.03;
    public const double ZoomMaxRegion = 0.35;
    public const double ZoomMargin = 0.10; // bbox margin, fraction of the box size

    /// <summary>Per-pixel planes extracted in one pass.</summary>
    public sealed class Grid
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

    public sealed class Region
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

    /// <summary>Focus regions for the interceptor chain: the body-sized foreground regions (small
    /// figures, collage panels) as FRACTIONAL bboxes with a 10% margin — reviewers re-examine
    /// these at detail scale. Capped at 3, largest first.</summary>
    public static IReadOnlyList<ImageRegion> CollectFocusRegions(Grid grid, List<Region> fgRegions, ContentVeilTuning t)
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

    public static void RecordZoom(ContentVeilMetrics metrics, ContentVeilMetrics zoom)
    {
        metrics.ZoomApplied = true;
        metrics.ZoomPointCount = Math.Max(metrics.ZoomPointCount, zoom.PointCount);
        metrics.ZoomInRegionPointCount = Math.Max(metrics.ZoomInRegionPointCount, zoom.InRegionPointCount);
        metrics.ZoomPaired = metrics.ZoomPaired || zoom.PairedPoints;
        metrics.ZoomMaxPointScore = Math.Max(metrics.ZoomMaxPointScore, zoom.MaxPointScore);
    }

    /// <summary>Re-run stages 1-3 on one body region's crop (10% margin) at full analysis size.
    /// Returns the crop's features, or null when the crop is degenerate.</summary>
    public static ContentVeilMetrics? ZoomPass(SixLabors.ImageSharp.Image<Rgba32> original, Grid grid, Region largest, ContentVeilTuning t)
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
        return CropAndScan(original, x0, y0, x1, y1, t);
    }

    /// <summary>Zoom a VERTICAL BAND of a body region — the chest band (a fraction of the region
    /// bbox height, full region width). Used by the large-body chest-zoom style: a full-in-frame
    /// nude is too big for the whole-body <see cref="ZoomPass"/> (no scale gain), but cropping just
    /// its chest band and re-scaling to 256 resolves the areolas that were sub-grid at frame scale.
    /// Returns the band crop's features, or null when the crop is degenerate.</summary>
    public static ContentVeilMetrics? ChestBandZoom(SixLabors.ImageSharp.Image<Rgba32> original, Grid grid, Region body, ContentVeilTuning t)
    {
        var scaleX = (double)original.Width / grid.Width;
        var scaleY = (double)original.Height / grid.Height;
        var regW = body.MaxX - body.MinX + 1;
        var regH = body.MaxY - body.MinY + 1;
        var mx = (int)(regW * ZoomMargin * scaleX);
        var bandTopG = body.MinY + regH * t.ChestZoomBandTop;
        var bandBotG = body.MinY + regH * t.ChestZoomBandBottom;
        var x0 = Math.Max(0, (int)(body.MinX * scaleX) - mx);
        var x1 = Math.Min(original.Width, (int)((body.MaxX + 1) * scaleX) + mx);
        var y0 = Math.Max(0, (int)(bandTopG * scaleY));
        var y1 = Math.Min(original.Height, (int)(bandBotG * scaleY));
        if (x1 - x0 < 8 || y1 - y0 < 8) return null;
        return CropAndScan(original, x0, y0, x1, y1, t);
    }

    /// <summary>Crop the original to [x0,y0)-(x1,y1), rescale to the analysis grid, and run stages
    /// 1-3 (in-region pairs allowed — at body scale their geometry is trustworthy).</summary>
    private static ContentVeilMetrics CropAndScan(SixLabors.ImageSharp.Image<Rgba32> original, int x0, int y0, int x1, int y1, ContentVeilTuning t)
    {
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

    /// <summary>Point-based explicit evidence (shared by every verification style — the mass-exposure
    /// rule is deliberately NOT part of this: zoomed faces would trip it). Returns the matched rule
    /// name, or null.</summary>
    public static string? PointEvidence(ContentVeilMetrics m, ContentVeilTuning t, bool isZoom = false)
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

    /// <summary>Pass-1 (non-zoomed) point/mass verdict rule — the matched rule name, or null.</summary>
    public static string? SensitiveRule(ContentVeilMetrics m, ContentVeilTuning t)
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

    /// <summary>Run stages 1-3 over a grid, filling the metrics FEATURES (no verdict). Returns the
    /// foreground (non-background) regions, largest first — the zoom styles crop from these.
    /// <paramref name="regionsOnly"/> stops after stage 2 (skin regions) — used when a review
    /// plugin will decide and only the focus regions are needed (skips the costly point stage).</summary>
    public static List<Region> FillMetrics(Grid g, ContentVeilMetrics metrics, bool allowInRegionPairs, ContentVeilTuning t, bool regionsOnly = false)
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
        // Pass-1 pair evidence = HOLE pairs only; the zoom passes may additionally pair in-region
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
