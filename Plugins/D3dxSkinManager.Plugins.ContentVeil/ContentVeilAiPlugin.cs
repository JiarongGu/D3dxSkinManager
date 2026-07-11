using System.Reflection;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Services;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace D3dxSkinManager.Plugins.ContentVeil;

/// <summary>
/// OPTIONAL AI detection plugin for the content veil — implements the host's GENERIC
/// <see cref="IImageReviewPlugin"/> capability (the host knows nothing about this implementation).
///
/// Detection = an anime censor-point DETECTOR (deepghs/anime_censor_detection, MIT, YOLOv8-style;
/// labels nipple_f / penis / pussy). A part detector matches the veil's product bar exactly
/// (visible explicit anatomy only) — whole-image NSFW classifiers were evaluated and rejected
/// (they merge "suggestive" into "explicit"; see .claude/knowledge/content-veil.md).
/// Variant = v1.0_n (nano, 11.5MB): A/B-tested against v1.0_s (42.5MB) on the labeled corpus
/// 2026-07-11 — nano scored BETTER (95.8% vs 94.4% acc) at 1.8x the speed and 1/4 the size.
///
/// Deployment: SINGLE dll — the model, the managed ONNX Runtime wrapper and the native
/// onnxruntime dlls are all embedded (PluginBootstrap serves the managed wrapper via
/// AssemblyResolve; InitAsync extracts + pre-loads the natives). Drop the one dll into
/// {profile}/plugins/ (or install in-app). The base app ships no ONNX bytes at all.
/// </summary>
public sealed class ContentVeilAiPlugin : IImageReviewPlugin
{
    private const int InputSize = 640;
    private const string ModelResource = "ContentVeilPlugin.censor-detect.onnx";

    private IPluginContext? _context;
    private readonly object _sessionLock = new();
    private InferenceSession? _session;
    private string? _inputName;
    // False when the native ONNX runtime failed to load at init — the reviewer then ABSTAINS (returns
    // null) so the host falls back to its built-in heuristic instead of crashing once per image.
    private volatile bool _nativeReady;

    public string Id => "d3dx.content-veil-ai";
    public string Name => "Content Veil AI Detection";
    public string Version => "1.1";
    public string Description => "Anime censor-point detector (ONNX) for the content veil";
    public string Author => "D3dxSkinManager";

    public Task InitAsync(IPluginContext context)
    {
        _context = context;

        // Single-dll pack: extract + pre-load the embedded NATIVE onnxruntime dlls into this
        // plugin's data dir BEFORE the first session (the managed wrapper is served from embedded
        // resources by PluginBootstrap's AssemblyResolve hook). If that fails, report a clear reason
        // and DISABLE inference — the host keeps working on its built-in heuristic (we abstain).
        try
        {
            PluginBootstrap.EnsureNativeLibraries(context.GetPluginDataPath(Id));
            _nativeReady = true;
        }
        catch (Exception ex)
        {
            _nativeReady = false;
            context.Log(Modules.Core.Helpers.LogLevel.Warn,
                $"[ContentVeilAI] AI detection is unavailable — {ex.Message}. The content veil will use its built-in heuristic instead.", ex);
        }
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetHandledMessageTypes() => Array.Empty<string>();

    public Task<IpcResponse> HandleMessageAsync(IpcRequest request) =>
        Task.FromResult(new IpcResponse
        {
            Id = request.Id,
            Success = false,
            Error = "This plugin exposes the IImageReviewPlugin capability, not IPC messages",
        });

    // The detector's full-image confidence is decisive at the extremes; in the ambiguous band the
    // host's FOCUS REGIONS (small figures, collage panels) are re-examined at detail scale and
    // the strongest pass wins. Band bounds are implementation detail of THIS reviewer.
    private const double RegionPassBandLow = 0.03;
    private const double RegionPassBandHigh = 0.60;
    private const int MaxRegionPasses = 3;

    /// <summary>Sensitivity confidence = the detector's max explicit-part class confidence over
    /// the full image and (when ambiguous) the host's focus regions. Decodes the file ONCE (capped)
    /// and crops regions IN-MEMORY — region passes used to re-decode full-res per region, the main
    /// cost. 1280px keeps enough detail for the 640 detector even after a region crop.</summary>
    public async Task<double?> ReviewImageAsync(ImageReviewContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_nativeReady) return null; // native runtime failed to load at init — abstain (host heuristic)
            if (!File.Exists(context.Path)) return null;

            return await Task.Run(() =>
            {
                var decoderOptions = new SixLabors.ImageSharp.Formats.DecoderOptions
                {
                    TargetSize = new SixLabors.ImageSharp.Size(1280, 1280),
                };
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(decoderOptions, context.Path);

                var best = MaxExplicitConfidence(image);

                // Region-TTA only in the ambiguous band — clearly-safe / clearly-explicit images
                // never pay for extra passes.
                if (best is >= RegionPassBandLow and < RegionPassBandHigh)
                {
                    foreach (var region in context.FocusRegions.Take(MaxRegionPasses))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var x = Math.Clamp((int)(region.X * image.Width), 0, Math.Max(0, image.Width - 1));
                        var y = Math.Clamp((int)(region.Y * image.Height), 0, Math.Max(0, image.Height - 1));
                        var w = Math.Clamp((int)(region.Width * image.Width), 1, image.Width - x);
                        var h = Math.Clamp((int)(region.Height * image.Height), 1, image.Height - y);
                        if (w < 8 || h < 8) continue;
                        using var crop = image.Clone(c => c.Crop(new SixLabors.ImageSharp.Rectangle(x, y, w, h)));
                        var conf = MaxExplicitConfidence(crop);
                        if (conf > best) best = conf;
                        if (best >= RegionPassBandHigh) break; // decisive — stop paying for more passes
                    }
                }
                return (double?)best;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _context?.Log(Modules.Core.Helpers.LogLevel.Debug, $"[ContentVeilAI] review failed for {context.Path}: {ex.Message}");
            return null;
        }
    }

    private InferenceSession GetSession()
    {
        if (_session != null) return _session;
        lock (_sessionLock)
        {
            if (_session != null) return _session;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ModelResource)
                ?? throw new InvalidOperationException($"embedded model missing: {ModelResource}");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            // The host analyzes BATCHES concurrently. Give each Run a few intra-op threads so the
            // box's cores are actually used (a single shared session serves all concurrent Runs);
            // 3 threads × the host's parallelism fills a many-core machine without wild
            // oversubscription. IntraOp=1 left 20 of 22 cores idle (83ms/img).
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 4, 2, 6),
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            };
            _session = new InferenceSession(ms.ToArray(), options);
            _inputName = _session.InputMetadata.Keys.First();
            return _session;
        }
    }

    /// <summary>YOLO conf-only decode: letterbox 640 (gray 114), RGB 0-1 CHW; output
    /// [1, 4+classes, anchors] with sigmoided class scores — the veil only needs the max.</summary>
    private double MaxExplicitConfidence(Image<Rgba32> source)
    {
        var session = GetSession();
        var scale = Math.Min((double)InputSize / source.Width, (double)InputSize / source.Height);
        var w = Math.Max(1, (int)Math.Round(source.Width * scale));
        var h = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var resized = source.Clone(x => x.Resize(w, h));

        // Fill a plain float[] by plane offset (planar CHW) — the DenseTensor 4D indexer recomputes
        // strides per element (~5M calls/inference), a measurable chunk of per-image cost. A managed
        // array (not a Span) can be captured by the ProcessPixelRows lambda.
        var plane = InputSize * InputSize;
        var data = new float[3 * plane];
        Array.Fill(data, 114f / 255f);
        var dx = (InputSize - w) / 2;
        var dy = (InputSize - h) / 2;
        resized.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var baseIdx = (dy + y) * InputSize + dx;
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    var idx = baseIdx + x;
                    data[idx] = p.R / 255f;
                    data[plane + idx] = p.G / 255f;
                    data[2 * plane + idx] = p.B / 255f;
                }
            }
        });
        var tensor = new DenseTensor<float>(data, new[] { 1, 3, InputSize, InputSize });

        using var results = session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName!, tensor) });
        var output = results.First().AsTensor<float>();
        var dims = output.Dimensions;
        if (dims.Length != 3) return 0;
        var channels = dims[1];
        var anchors = dims[2];
        // Class rows are 4..channels, laid out contiguously [1, channels, anchors] — scan the flat
        // backing buffer (rows 0-3 = box coords, skipped) instead of the 3D indexer.
        var flat = output.ToArray();
        var max = 0f;
        for (var i = 4 * anchors; i < channels * anchors; i++)
            if (flat[i] > max) max = flat[i];
        return max;
    }

    public ValueTask DisposeAsync()
    {
        _session?.Dispose();
        _session = null;
        return ValueTask.CompletedTask;
    }
}
