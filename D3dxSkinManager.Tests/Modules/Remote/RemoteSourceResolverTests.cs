using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// RemoteSourceResolver — the pure 3-tier resolution (res ← sparse local ← library params). Locks the
/// two load-bearing invariants: a sparse overlay overrides ONLY its own keys (res updates to untouched
/// fields flow through), and {param.key} placeholders substitute from the library's values over the
/// source's declared defaults. See remote-library-redesign.md.
/// </summary>
public class RemoteSourceResolverTests
{
    private readonly RemoteSourceResolver _resolver = new();

    private static RemoteSourceConfig Base() => new()
    {
        Id = "gamebanana",
        Name = "GameBanana",
        BaseUrl = "https://gamebanana.com",
        Engine = "gamebanana",
        Lists = new() { new() { Id = "8552", Name = "Genshin Impact" } },
        ListUrlTemplate = "{param.host}/list/{list}?page={page}",
        Params = new() { new() { Key = "host", Label = "Host", Type = "input", Default = "https://gamebanana.com" } },
    };

    [Fact]
    public void Resolve_NoOverlay_NoParamValues_UsesBaseAndParamDefaults()
    {
        var r = _resolver.Resolve(Base(), null, null);

        r.Name.Should().Be("GameBanana");
        r.Lists.Should().ContainSingle().Which.Id.Should().Be("8552");
        r.ListUrlTemplate.Should().Be("https://gamebanana.com/list/{list}?page={page}", "the param default fills {param.host}");
    }

    [Fact]
    public void Resolve_SparseOverlay_OverridesOnlyItsKeys_RestInheritRes()
    {
        // A real sparse overlay carries ONLY the overridden key.
        var r = _resolver.Resolve(Base(), """{"id":"gamebanana","baseUrl":"https://mirror.example"}""", null);

        r.BaseUrl.Should().Be("https://mirror.example", "the overlay overrides baseUrl");
        r.Name.Should().Be("GameBanana", "an unset overlay field inherits res");
        r.Engine.Should().Be("gamebanana");
        r.Lists.Should().ContainSingle().Which.Id.Should().Be("8552", "overlay omits lists → res lists inherited (new games flow through)");
    }

    [Fact]
    public void Resolve_LibraryParamValue_WinsOverDefault_AndSubstitutesEverywhere()
    {
        var r = _resolver.Resolve(Base(), null, new Dictionary<string, string> { ["host"] = "https://b.example" });

        r.ListUrlTemplate.Should().Be("https://b.example/list/{list}?page={page}", "the library value beats the param default");
    }

    [Fact]
    public void Diff_ProducesSparseOverlay_OfOnlyChangedKeys()
    {
        var edited = Base();
        edited.BaseUrl = "https://mirror.example";
        edited.Lists.Add(new RemoteListConfig { Id = "21842", Name = "Arknights: Endfield" });

        var diffJson = _resolver.Diff(Base(), edited);

        diffJson.Should().Contain("mirror.example").And.Contain("21842");
        diffJson.Should().NotContain("gamebanana.com", "unchanged fields (baseUrl base, host default) are not in the sparse diff");
    }

    [Fact]
    public void Diff_ThenResolve_ReproducesEdits_AndStillInheritsLaterResChanges()
    {
        var edited = Base();
        edited.BaseUrl = "https://mirror.example";
        var diffJson = _resolver.Diff(Base(), edited);

        // Resolving the diff over the base reproduces the override.
        _resolver.Resolve(Base(), diffJson, null).BaseUrl.Should().Be("https://mirror.example");

        // A LATER res change to an UNTOUCHED field (Name) flows through the same sparse overlay.
        var newerRes = Base();
        newerRes.Name = "GameBanana (renamed)";
        var resolved = _resolver.Resolve(newerRes, diffJson, null);
        resolved.Name.Should().Be("GameBanana (renamed)", "sparse overlay didn't touch Name → the res update flows through");
        resolved.BaseUrl.Should().Be("https://mirror.example", "the overlay's override still applies");
    }
}
