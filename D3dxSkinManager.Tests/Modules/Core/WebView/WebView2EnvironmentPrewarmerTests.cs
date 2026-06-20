using System.Linq;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.WebView;

namespace D3dxSkinManager.Tests.Modules.Core.WebView;

/// <summary>
/// Guards the Chromium command-line built for the WebView2 environment. The regression these tests lock
/// in: --enable-features / --disable-features must each appear EXACTLY ONCE (Chromium keeps only the last
/// occurrence of a repeated switch, which previously silently dropped IsolatedCodeCache + draggable
/// regions), and the startup-regressing --js-flags (--no-lazy / --always-opt) must be gone.
/// </summary>
public class WebView2EnvironmentPrewarmerTests
{
    private static int Count(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }

    [Fact]
    public void BuildBrowserArguments_EnableFeatures_AppearsExactlyOnce()
    {
        var args = WebView2EnvironmentPrewarmer.BuildBrowserArguments(isDevelopment: false, devExtraArgs: null);

        Count(args, "--enable-features=").Should().Be(1, "a repeated --enable-features switch drops earlier feature lists");
        Count(args, "--disable-features=").Should().Be(1, "a repeated --disable-features switch drops earlier feature lists");
    }

    [Fact]
    public void BuildBrowserArguments_KeepsCriticalFeatures()
    {
        var args = WebView2EnvironmentPrewarmer.BuildBrowserArguments(isDevelopment: false, devExtraArgs: null);

        // All three must survive in the single merged enable-features list.
        args.Should().Contain("IsolatedCodeCache");          // V8 code cache → fast subsequent loads
        args.Should().Contain("msWebView2EnableDraggableRegions");
        args.Should().Contain("ScriptStreaming");
        args.Should().Contain("msSmartScreenProtection");    // in the merged disable list
        args.Should().Contain("TranslateUI");
    }

    [Fact]
    public void BuildBrowserArguments_DropsStartupRegressingJsFlags()
    {
        var args = WebView2EnvironmentPrewarmer.BuildBrowserArguments(isDevelopment: false, devExtraArgs: null);

        args.Should().NotContain("--no-lazy", "over-eager V8 compilation regresses startup");
        args.Should().NotContain("--always-opt", "removed from V8");
        args.Should().NotContain("--js-flags");
    }

    [Fact]
    public void BuildBrowserArguments_AppendsDevArgs_OnlyInDevelopment()
    {
        const string cdp = "--remote-debugging-port=9321";

        WebView2EnvironmentPrewarmer.BuildBrowserArguments(isDevelopment: true, devExtraArgs: cdp)
            .Should().EndWith(cdp);

        WebView2EnvironmentPrewarmer.BuildBrowserArguments(isDevelopment: false, devExtraArgs: cdp)
            .Should().NotContain(cdp, "CDP debugging args must never be added in production");
    }

    [Fact]
    public void BuildBrowserArguments_IgnoresBlankDevArgs()
    {
        WebView2EnvironmentPrewarmer.BuildBrowserArguments(isDevelopment: true, devExtraArgs: "   ")
            .Should().NotEndWith(" ");
    }
}
