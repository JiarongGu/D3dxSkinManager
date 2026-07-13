using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Plugin.Services;

namespace D3dxSkinManager.Tests.Modules.Plugin;

/// <summary>
/// Locks the plugin loader's "reuse an already-loaded assembly instead of re-loading it" decision —
/// the fix for the multi-profile crash where two profiles both have the same pack installed and the
/// second load throws <c>FileLoadException: "Assembly with same name is already loaded"</c>
/// (<c>Assembly.LoadFrom</c> can't load the same identity from a different path into the default ALC).
/// </summary>
public class PluginLoaderTests
{
    [Fact]
    public void FindAlreadyLoaded_MatchesBySimpleName_CaseInsensitive()
    {
        var self = typeof(PluginLoader).Assembly;
        var simpleName = self.GetName().Name!;

        PluginLoader.FindAlreadyLoaded(simpleName, new[] { self })
            .Should().BeSameAs(self, "an already-loaded assembly is reused, not re-loaded");

        PluginLoader.FindAlreadyLoaded(simpleName.ToUpperInvariant(), new[] { self })
            .Should().BeSameAs(self, "assembly simple names match case-insensitively");
    }

    [Fact]
    public void FindAlreadyLoaded_NoMatch_ReturnsNull_SoTheAssemblyIsLoadedFresh()
    {
        var self = typeof(PluginLoader).Assembly;

        PluginLoader.FindAlreadyLoaded("D3dxSkinManager.Plugins.NotLoadedPack", new[] { self })
            .Should().BeNull("an assembly that isn't loaded yet must fall through to a fresh load");
    }

    [Fact]
    public void FindAlreadyLoaded_SkipsDynamicAssemblies()
    {
        var dynamic = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("SomeDynamicPluginAsm"), AssemblyBuilderAccess.Run);

        // A dynamic/in-memory assembly has no file backing; it must never be handed back as a
        // "reuse" candidate for a disk plugin dll.
        PluginLoader.FindAlreadyLoaded("SomeDynamicPluginAsm", new Assembly[] { dynamic })
            .Should().BeNull();
    }

    [Fact]
    public void FindAlreadyLoaded_EmptyOrNullName_ReturnsNull()
    {
        var loaded = new List<Assembly> { typeof(PluginLoader).Assembly };

        PluginLoader.FindAlreadyLoaded("", loaded).Should().BeNull();
        PluginLoader.FindAlreadyLoaded(null!, loaded).Should().BeNull();
    }
}
