using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Setting.Services;

namespace D3dxSkinManager.Tests.Modules.Setting.Services;

/// <summary>
/// The main window persists its size/position in LOGICAL (96-DPI) px; WinForms window coordinates are
/// device px at the current monitor DPI and are NOT auto-scaled from a logical baseline, so
/// <see cref="WindowStateService.ToPhysicalState"/> must × the current DPI when applying. Locks the fix
/// for "startup window too small on high-DPI" (1280×800 was applied as device px → ~853×533 logical on
/// 150%) and the cross-DPI-restore behavior. At 100% it must be the identity (no regression on 96-DPI).
/// </summary>
public class WindowStateServiceTests
{
    [Fact]
    public void Default_At100_IsIdentity()
    {
        WindowStateService.ToPhysicalState(null, null, null, null, false, 1.0)
            .Should().Be((1280, 800, (int?)null, (int?)null, false));
    }

    [Fact]
    public void Default_At150_ScalesUp_TheFix()
    {
        // The bug: the 1280×800 default rendered at device px → tiny window on 150%. Now it scales to
        // 1920×1200 physical = a full 1280×800 LOGICAL window.
        WindowStateService.ToPhysicalState(null, null, null, null, false, 1.5)
            .Should().Be((1920, 1200, (int?)null, (int?)null, false));
    }

    [Theory]
    [InlineData(1280, 800, 1.0, 1280, 800)]
    [InlineData(1280, 800, 1.25, 1600, 1000)]
    [InlineData(1280, 800, 2.0, 2560, 1600)]
    [InlineData(1600, 900, 1.5, 2400, 1350)]
    public void LogicalSize_ScaledByCurrentDpi(int logW, int logH, double dpi, int expW, int expH)
    {
        var (w, h, _, _, _) = WindowStateService.ToPhysicalState(logW, logH, null, null, false, dpi);
        (w, h).Should().Be((expW, expH));
    }

    [Fact]
    public void MinimumSize_ClampedInLogicalSpace_ThenScaled()
    {
        // 400×300 is below the 800×600 logical minimum → clamp to 800×600 logical, then × 2.0 = 1600×1200.
        WindowStateService.ToPhysicalState(400, 300, null, null, false, 2.0)
            .Should().Be((1600, 1200, (int?)null, (int?)null, false));
    }

    [Fact]
    public void Position_ScaledByCurrentDpi()
    {
        WindowStateService.ToPhysicalState(1280, 800, 100, 50, false, 1.5)
            .Should().Be((1920, 1200, (int?)150, (int?)75, false));
    }

    [Fact]
    public void Maximized_IsPreserved()
    {
        var (_, _, _, _, maximized) = WindowStateService.ToPhysicalState(1280, 800, null, null, true, 1.5);
        maximized.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void NonPositiveDpi_FallsBackToIdentity(double badDpi)
    {
        WindowStateService.ToPhysicalState(1280, 800, null, null, false, badDpi)
            .Should().Be((1280, 800, (int?)null, (int?)null, false));
    }

    [Fact]
    public void PartialPosition_NotScaled_WhenIncomplete()
    {
        // Only X (no Y) → position is not applied (both must be present), matching the caller's guard.
        var (_, _, x, y, _) = WindowStateService.ToPhysicalState(1280, 800, 100, null, false, 1.5);
        x.Should().BeNull();
        y.Should().BeNull();
    }
}
