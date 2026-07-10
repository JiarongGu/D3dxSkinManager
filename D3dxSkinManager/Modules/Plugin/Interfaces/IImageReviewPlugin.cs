namespace D3dxSkinManager.Modules.Plugin.Interfaces;

/// <summary>A region of an image in FRACTIONAL coordinates (0-1 of width/height) — decode- and
/// resolution-independent.</summary>
public readonly record struct ImageRegion(double X, double Y, double Width, double Height);

/// <summary>
/// What the host's own analysis knew when it handed the image to the interceptor chain.
/// </summary>
/// <param name="Path">Absolute path of the image file.</param>
/// <param name="CurrentVerdict">The verdict so far ("sensitive" | "safe") — the host's built-in
/// analysis seeds it; earlier interceptors may have refined it.</param>
/// <param name="FocusRegions">Body/subject region candidates the host's analysis found (small
/// figures, collage panels) — reviewers can re-examine these at detail scale. May be empty.</param>
public sealed record ImageReviewContext(
    string Path,
    string CurrentVerdict,
    IReadOnlyList<ImageRegion> FocusRegions);

/// <summary>
/// CAPABILITY interface: an INTERCEPTOR on the content-veil review flow. The host runs its own
/// (pure-CV) analysis first, then hands each registered reviewer the context; the strongest
/// returned confidence decides the final verdict. The host is implementation-agnostic — it
/// neither knows nor cares HOW a plugin judges (ONNX model, remote API, anything). Discovered via
/// <c>IPluginRegistry.GetPlugins&lt;IImageReviewPlugin&gt;()</c>.
/// </summary>
public interface IImageReviewPlugin : IPlugin
{
    /// <summary>
    /// Sensitivity confidence 0-1 for the image (1 = certainly explicit), or null to ABSTAIN
    /// (unreadable image, unsupported format, model unavailable…). When a reviewer abstains the
    /// host's own verdict stands. Called concurrently — implementations must be thread-safe.
    /// </summary>
    Task<double?> ReviewImageAsync(ImageReviewContext context, CancellationToken cancellationToken = default);
}
