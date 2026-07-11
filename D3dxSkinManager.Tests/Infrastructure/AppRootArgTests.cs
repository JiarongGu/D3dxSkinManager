using FluentAssertions;
using Xunit;
using D3dxSkinManager.Infrastructure;

namespace D3dxSkinManager.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="AppRootArg"/> — the launcher passes the install root via
/// <c>--app-root</c> because the runtime exe now lives in <c>{install}/lib</c>. A wrong resolution
/// silently repoints every install-relative path at <c>lib/</c>, so this is locked with tests.
/// </summary>
public class AppRootArgTests
{
    private const string Fallback = @"C:\fallback";

    [Fact]
    public void Resolve_SpaceSeparated_ReturnsValue()
    {
        AppRootArg.Resolve(new[] { "--app-root", @"C:\Games\D3dx" }, Fallback)
            .Should().Be(@"C:\Games\D3dx");
    }

    [Fact]
    public void Resolve_JoinedWithEquals_ReturnsValue()
    {
        AppRootArg.Resolve(new[] { @"--app-root=C:\Games\D3dx" }, Fallback)
            .Should().Be(@"C:\Games\D3dx");
    }

    [Fact]
    public void Resolve_StripsSurroundingQuotes()
    {
        AppRootArg.Resolve(new[] { "--app-root", "\"C:\\Games\\D3dx\"" }, Fallback)
            .Should().Be(@"C:\Games\D3dx");
    }

    [Fact]
    public void Resolve_CaseInsensitiveFlag()
    {
        AppRootArg.Resolve(new[] { "--APP-ROOT", @"C:\X" }, Fallback).Should().Be(@"C:\X");
    }

    [Fact]
    public void Resolve_FlagAmongOtherArgs()
    {
        AppRootArg.Resolve(new[] { "--other", "y", "--app-root", @"C:\X", "--flag" }, Fallback)
            .Should().Be(@"C:\X");
    }

    [Fact]
    public void Resolve_MissingOrBlank_ReturnsFallback()
    {
        AppRootArg.Resolve(new string[0], Fallback).Should().Be(Fallback);                 // no args
        AppRootArg.Resolve(new[] { "--other", "value" }, Fallback).Should().Be(Fallback);  // flag absent
        AppRootArg.Resolve(new[] { "--app-root" }, Fallback).Should().Be(Fallback);         // flag, no value (at end)
        AppRootArg.Resolve(new[] { "--app-root", "   " }, Fallback).Should().Be(Fallback);  // blank value
        AppRootArg.Resolve(new[] { "--app-root=" }, Fallback).Should().Be(Fallback);        // empty joined value
    }

    [Fact]
    public void Resolve_NullArgs_ReturnsFallback()
    {
        AppRootArg.Resolve(null, Fallback).Should().Be(Fallback);
    }
}
