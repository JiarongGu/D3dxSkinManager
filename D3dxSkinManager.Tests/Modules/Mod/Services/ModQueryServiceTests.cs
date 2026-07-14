using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// ModQueryService subscribes to MOD/CACHE_CHANGED in its ctor to invalidate its active-mods cache.
/// ProfileEventBus registers that handler on the GLOBAL event bus, so a profile-scoped service that
/// never unsubscribes leaks the handler (and itself) into the global bus on every profile switch.
/// These lock in that Dispose unsubscribes exactly once (idempotent).
/// </summary>
public class ModQueryServiceTests
{
    private static Mock<IProfileEventBus> BusReturning(string subscriptionId)
    {
        var bus = new Mock<IProfileEventBus>();
        bus.Setup(b => b.Subscribe(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<EventMessage, Task>>()))
            .Returns(subscriptionId);
        return bus;
    }

    private static ModQueryService Create(IProfileEventBus bus) => new(
        Mock.Of<IModRepository>(),
        Mock.Of<ICategoryRepository>(),
        Mock.Of<IModEnrichmentService>(),
        Mock.Of<IProfilePathService>(),
        Mock.Of<IMemoryCache>(),
        bus,
        Mock.Of<IProfileContext>());

    [Fact]
    public void Ctor_SubscribesToCacheChanged()
    {
        var bus = BusReturning("sub-1");

        _ = Create(bus.Object);

        bus.Verify(b => b.Subscribe("MOD", "CACHE_CHANGED", It.IsAny<Func<EventMessage, Task>>()), Times.Once);
    }

    [Fact]
    public void Dispose_UnsubscribesTheCacheChangedHandler()
    {
        var bus = BusReturning("sub-1");
        var service = Create(bus.Object);

        service.Dispose();

        bus.Verify(b => b.Unsubscribe("sub-1"), Times.Once);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var bus = BusReturning("sub-1");
        var service = Create(bus.Object);

        service.Dispose();
        service.Dispose();

        bus.Verify(b => b.Unsubscribe("sub-1"), Times.Once);
    }
}
