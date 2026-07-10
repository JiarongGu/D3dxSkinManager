using System;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Tool.Services;

namespace D3dxSkinManager.Tests.Modules.Tool.Services;

/// <summary>
/// ModFixService.WriteFixMetadata — the metadata stamp that powers the "may need re-fix" flag. Must
/// round-trip lastFixedUtc, preserve other fields (e.g. the remote import identity), and survive
/// null/garbage input. See needs-refix design in the git history + modFixRef.ts on the frontend.
/// </summary>
public class ModFixMetadataTests
{
    [Fact]
    public void WriteFixMetadata_StampsLastFixedUtc_OnEmptyMetadata()
    {
        var utc = new DateTime(2026, 7, 11, 8, 30, 0, DateTimeKind.Utc);

        var json = ModFixService.WriteFixMetadata(null, utc);

        var fix = JsonNode.Parse(json)!["fix"]!;
        DateTime.Parse(fix["lastFixedUtc"]!.GetValue<string>()).ToUniversalTime()
            .Should().Be(utc);
    }

    [Fact]
    public void WriteFixMetadata_PreservesOtherFields()
    {
        // A mod imported from a remote library already carries metadata.remote — the fix stamp must not drop it.
        var existing = """{"remote":{"sourceId":"gamebanana","detailUrl":"https://x/mods/1"}}""";

        var json = ModFixService.WriteFixMetadata(existing, DateTime.UtcNow);

        var root = JsonNode.Parse(json)!;
        root["remote"]!["sourceId"]!.GetValue<string>().Should().Be("gamebanana");
        root["fix"]!["lastFixedUtc"].Should().NotBeNull();
    }

    [Fact]
    public void WriteFixMetadata_OverwritesPriorFixStamp()
    {
        var first = ModFixService.WriteFixMetadata(null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var second = ModFixService.WriteFixMetadata(first, new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc));

        var stamped = DateTime.Parse(JsonNode.Parse(second)!["fix"]!["lastFixedUtc"]!.GetValue<string>()).ToUniversalTime();
        stamped.Year.Should().Be(2026);
        stamped.Month.Should().Be(7);
    }

    [Fact]
    public void WriteFixMetadata_RecoversFromGarbage()
    {
        var json = ModFixService.WriteFixMetadata("not valid json {", DateTime.UtcNow);

        JsonNode.Parse(json)!["fix"]!["lastFixedUtc"].Should().NotBeNull();
    }
}
