using Microsoft.Web.WebView2.Core;

namespace D3dxSkinManager.Modules.Core.WebView;

/// <summary>
/// Pre-creates the (process-global) WebView2 <see cref="CoreWebView2Environment"/> as early as possible
/// at startup, so the expensive browser-process spawn + user-data-folder init overlaps the rest of app
/// startup (DI build, window-state load, form creation, eager loading) instead of running serially when
/// the WebView session starts.
///
/// Cold-start cost of <see cref="CoreWebView2Environment.CreateAsync(string,string,CoreWebView2EnvironmentOptions)"/>
/// is the dominant chunk of WebView2 init (~1-2s). It does NOT need the WinForms control or message loop,
/// so we kick it off from <c>ApplicationBootstrapper.Run</c> right after WinForms init. By the time the
/// WebView session calls <see cref="GetAsync"/>, the task is usually already complete → near-zero wait.
///
/// One environment per process (MS guidance: share a single CoreWebView2Environment across all controls).
/// </summary>
public static class WebView2EnvironmentPrewarmer
{
    private static readonly object _lock = new();
    private static Task<CoreWebView2Environment>? _task;

    /// <summary>Kick off environment creation early (fire-and-forget). Idempotent.</summary>
    public static void Begin(string baseDirectory) => _ = GetAsync(baseDirectory);

    /// <summary>
    /// Return the prewarmed environment task, starting it on first call. The WebView initializer awaits
    /// this instead of calling CreateAsync itself, so it pays only the remaining (often zero) time.
    /// </summary>
    public static Task<CoreWebView2Environment> GetAsync(string baseDirectory)
    {
        lock (_lock)
        {
            return _task ??= CreateAsync(baseDirectory);
        }
    }

    /// <summary>
    /// Build the Chromium command-line passed via <see cref="CoreWebView2EnvironmentOptions.AdditionalBrowserArguments"/>.
    ///
    /// IMPORTANT: <c>--enable-features</c> / <c>--disable-features</c> are each given EXACTLY ONCE with a
    /// comma-separated list. Passing the same switch multiple times is ambiguous (Chromium keeps only the
    /// last occurrence of a switch), which previously silently dropped IsolatedCodeCache (the V8 code-cache
    /// feature this app relies on for fast subsequent loads) and the draggable-regions feature.
    ///
    /// Also removed the old <c>--js-flags=--no-lazy --always-opt --serialize-eager</c>: <c>--no-lazy</c>
    /// forces over-eager V8 compilation of every function at load which REGRESSES startup, and
    /// <c>--always-opt</c> was removed from V8. Code caching is provided by the IsolatedCodeCache feature,
    /// not those flags.
    /// </summary>
    public static string BuildBrowserArguments(bool isDevelopment, string? devExtraArgs)
    {
        // Single enable-features / disable-features lists (comma-separated, each switch used once).
        const string enableFeatures = "msWebView2EnableDraggableRegions,IsolatedCodeCache,ScriptStreaming";
        const string disableFeatures = "msSmartScreenProtection,TranslateUI";

        var args =
            $"--enable-features={enableFeatures} " +
            $"--disable-features={disableFeatures} " +
            "--enable-gpu-rasterization " +
            "--enable-zero-copy " +
            "--enable-accelerated-2d-canvas " +
            "--enable-hardware-overlays " +
            "--force-color-profile=srgb " +
            "--disable-background-timer-throttling " +
            "--disable-renderer-backgrounding " +
            "--disable-ipc-flooding-protection " +
            "--disable-gpu-driver-bug-workarounds " +
            "--disable-component-update " +
            "--disable-default-apps " +
            "--disable-domain-reliability " +
            "--disable-sync " +
            "--no-first-run " +
            "--no-default-browser-check " +
            "--disable-background-networking " +
            "--disable-breakpad";

        // DEV ONLY: because we set AdditionalBrowserArguments, WebView2 IGNORES the
        // WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS env var. The devtools toolkit sets it to
        // "--remote-debugging-port=<port>" so CDP can attach; append it here in dev mode. Never in prod.
        if (isDevelopment && !string.IsNullOrWhiteSpace(devExtraArgs))
        {
            args += " " + devExtraArgs.Trim();
        }

        return args;
    }

    private static async Task<CoreWebView2Environment> CreateAsync(string baseDirectory)
    {
        Console.WriteLine("[WebView2] Prewarming environment (early browser-process spawn)...");

        var userDataFolder = Path.Combine(baseDirectory, "data", "webview2");
        Directory.CreateDirectory(userDataFolder);

        var isDevelopment = IsDevelopmentMode(baseDirectory);
        var devExtraArgs = isDevelopment
            ? Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS")
            : null;

        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = BuildBrowserArguments(isDevelopment, devExtraArgs)
        };

        var sw = global::System.Diagnostics.Stopwatch.StartNew();
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder,
            options: options);
        sw.Stop();

        Console.WriteLine($"[WebView2] Environment prewarm completed (CreateAsync took {sw.ElapsedMilliseconds}ms)");
        return environment;
    }

    // Mirrors WebViewInitializer.IsDevelopmentMode (kept identical so prewarm + nav agree on the mode).
    private static bool IsDevelopmentMode(string baseDirectory) =>
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ||
        File.Exists(Path.Combine(baseDirectory, ".dev"));
}
