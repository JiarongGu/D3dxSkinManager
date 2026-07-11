using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Infrastructure;

namespace D3dxSkinManager.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="SingleInstanceGuard"/>. Covers the pure key derivation (deterministic,
/// per-install, normalized) and the OS-mutex behavior that proves a 2nd instance is detected while
/// distinct installs coexist. The window-activation broadcast is native/integration and is verified by
/// build + manual run, not here. Each test uses a unique install dir so the process-lifetime mutex the
/// guard holds never leaks across tests.
/// </summary>
public class SingleInstanceGuardTests
{
    private static string FreshInstallDir() =>
        Path.Combine(Path.GetTempPath(), "d3dx-sig-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ChannelKey_IsDeterministic_ForSameInstall()
    {
        var dir = @"C:\Games\D3dxSkinManager";
        SingleInstanceGuard.ChannelKey(dir).Should().Be(SingleInstanceGuard.ChannelKey(dir));
    }

    [Fact]
    public void ChannelKey_Differs_ForDifferentInstalls()
    {
        SingleInstanceGuard.ChannelKey(@"C:\Games\A")
            .Should().NotBe(SingleInstanceGuard.ChannelKey(@"C:\Games\B"));
    }

    [Theory]
    [InlineData(@"C:\App", @"C:\App\")]        // trailing backslash
    [InlineData(@"C:\App", @"C:\app")]          // case
    [InlineData(@"C:\App", @"C:\App/")]         // trailing forward slash
    [InlineData(@"C:\App\", @"c:\APP")]         // both
    public void ChannelKey_Normalizes_CaseAndTrailingSeparator(string a, string b)
    {
        SingleInstanceGuard.ChannelKey(a).Should().Be(SingleInstanceGuard.ChannelKey(b));
    }

    [Fact]
    public void ChannelKey_Handles_NullAndEmpty()
    {
        // Must not throw, and both collapse to the same empty-path key.
        SingleInstanceGuard.ChannelKey(null).Should().Be(SingleInstanceGuard.ChannelKey(string.Empty));
    }

    [Fact]
    public void ChannelKey_ProducesValidMutexAndMessageNames()
    {
        var key = SingleInstanceGuard.ChannelKey(@"C:\Games\D3dxSkinManager");
        key.Should().MatchRegex("^[0-9a-f]{8}$"); // hex only — safe for OS object names
        SingleInstanceGuard.MutexName(key).Should().Be($"Local\\D3dxSkinManager.instance.{key}");
        SingleInstanceGuard.MessageName(key).Should().Be($"D3dxSkinManager.activate.{key}");
    }

    [Fact]
    public void TryAcquire_FirstInstance_ReturnsTrue_AndHoldsMutex()
    {
        var dir = FreshInstallDir();

        SingleInstanceGuard.TryAcquire(dir).Should().BeTrue("this is the first instance for a fresh install");

        // A probe on the same OS mutex name now sees it already exists → a real 2nd instance would too.
        var name = "Local\\D3dxSkinManager.instance." + SingleInstanceGuard.ChannelKey(dir);
        using var probe = new Mutex(initiallyOwned: true, name, out bool createdNew);
        createdNew.Should().BeFalse("the guard is holding the mutex for this install");
    }

    [Fact]
    public void TryAcquire_SetsActivateMessageId_NonZero()
    {
        SingleInstanceGuard.TryAcquire(FreshInstallDir());
        SingleInstanceGuard.ActivateMessageId.Should().NotBe(0u,
            "RegisterWindowMessage returns a non-zero atom the running instance listens for");
    }
}
