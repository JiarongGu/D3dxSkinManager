using System.Linq;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Parsing of the GameBanana apiv11 JSON (engine="gamebanana"). Shapes mirror the live API verified
/// 2026-07-06 (subfeed _aRecords/_aMetadata; ProfilePage _aFiles/_aPreviewMedia). See remote-library.md.
/// </summary>
public class GameBananaEngineTests
{
    private const string Subfeed = """
    {
      "_aMetadata": { "_nRecordCount": 30, "_nPerpage": 15, "_bIsComplete": false },
      "_aRecords": [
        { "_idRow": 1, "_sModelName": "Mod", "_sName": "Vivian Vampire", "_sProfileUrl": "https://gamebanana.com/mods/1", "_tsDateAdded": 1700000000, "_sInitialVisibility": "show",
          "_aRootCategory": { "_sName": "Skins", "_sProfileUrl": "https://gamebanana.com/mods/cats/1" },
          "_aPreviewMedia": { "_aImages": [ { "_sType": "screenshot", "_sBaseUrl": "https://images.gamebanana.com/img/ss/mods", "_sFile": "raw.jpg", "_sFile530": "530-90_raw.jpg", "_sFile220": "220-90_raw.jpg" } ] } },
        { "_idRow": 2, "_sModelName": "Sound", "_sName": "Not a mod", "_sProfileUrl": "https://gamebanana.com/sounds/2" },
        { "_idRow": 3, "_sModelName": "Mod", "_sName": "", "_sProfileUrl": "https://gamebanana.com/mods/3" },
        { "_idRow": 4, "_sModelName": "Mod", "_sName": "Newer Mod", "_sProfileUrl": "https://gamebanana.com/mods/4", "_tsDateAdded": 1800000000, "_sInitialVisibility": "warn" }
      ]
    }
    """;

    private const string ProfilePage = """
    {
      "_idRow": 529138,
      "_sName": "Vivian Vampire",
      "_aCategory": { "_idRow": 12526, "_sName": "Hu Tao", "_sModelName": "ModCategory" },
      "_aPreviewMedia": { "_aImages": [
        { "_sBaseUrl": "https://images.gamebanana.com/img/ss/mods", "_sFile": "a.jpg", "_sFile530": "530-90_a.jpg" },
        { "_sBaseUrl": "https://images.gamebanana.com/img/ss/mods", "_sFile": "b.jpg" }
      ] },
      "_sDownloadUrl": "https://gamebanana.com/mods/download/529138",
      "_aFiles": [
        { "_idRow": 111, "_sFile": "vivian_1.0.7z", "_nFilesize": 456, "_sDownloadUrl": "https://gamebanana.com/dl/111" }
      ]
    }
    """;

    [Fact]
    public void ParseSubfeed_ExtractsMods_SkipsNonModsAndEmpty_ComputesPages()
    {
        var result = GameBananaEngine.ParseSubfeed(Subfeed, "https://gamebanana.com", 1);

        // Two valid Mods (Sound filtered; empty-name mod dropped), in the API's ORIGINAL order —
        // the default Subfeed order IS the site's page order (verified in Chrome 2026-07-06); a
        // client-side re-sort by dateAdded buried freshly-updated old mods and lost the first mod.
        result.Cards.Should().HaveCount(2);
        result.Cards.Select(c => c.Title).Should().ContainInOrder("Vivian Vampire", "Newer Mod");

        var card = result.Cards.Single(c => c.Title == "Vivian Vampire");
        card.DetailUrl.Should().Be("https://gamebanana.com/mods/1");
        card.ImageUrl.Should().Be("https://images.gamebanana.com/img/ss/mods/530-90_raw.jpg", "cards use the 530px variant");
        card.Tags.Should().BeEquivalentTo(new[] { "Skins" }, "the super category is captured as a TAG");
        card.DateHint.Should().Be("2023-11-14", "unix _tsDateAdded → yyyy-MM-dd");
        result.TotalPages.Should().Be(2, "ceil(30 / 15)");

        // Content rating → the content-veil sensitivity flag: "show" = safe, "warn"/"hide" = sensitive.
        card.Sensitive.Should().BeFalse();
        result.Cards.Single(c => c.Title == "Newer Mod").Sensitive.Should().BeTrue();
    }

    [Fact]
    public void ParseProfilePage_ExtractsTitle_Gallery_DirectDownloads()
    {
        var detail = GameBananaEngine.ParseProfilePage(ProfilePage, "https://gamebanana.com/mods/529138");

        detail.Title.Should().Be("Vivian Vampire");
        detail.Images.Should().BeEquivalentTo(new[]
        {
            "https://images.gamebanana.com/img/ss/mods/a.jpg",   // gallery prefers the original (_sFile)
            "https://images.gamebanana.com/img/ss/mods/b.jpg",
        });
        detail.Downloads.Should().HaveCount(1);
        detail.Downloads[0].Url.Should().Be("https://gamebanana.com/dl/111");
        detail.Downloads[0].Type.Should().Be("direct", "gamebanana files are direct downloads");
        detail.Downloads[0].Name.Should().Be("vivian_1.0.7z");
        detail.Downloads[0].SizeBytes.Should().Be(456, "the file size comes from _nFilesize");
        detail.Tags.Should().BeEquivalentTo(new[] { "Hu Tao" }, "the SUB category comes from the ProfilePage as a tag");
    }

    [Theory]
    [InlineData("https://gamebanana.com/mods/529138", "529138")]
    [InlineData("https://gamebanana.com/mods/1?foo=bar", "1")]
    [InlineData("https://gamebanana.com/members/3382476", null)]
    public void ExtractModId_ReadsTheModId(string url, string? expected)
    {
        GameBananaEngine.ExtractModId(url).Should().Be(expected);
    }

    [Theory]
    [InlineData("We couldn't find that page.")] // GameBanana's plain-text for a removed mod → "'W' is an invalid start of a value"
    [InlineData("<!DOCTYPE html><html><body>Not Found</body></html>")]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData("{ not really: json ")] // starts with '{' but is malformed → JsonException path
    public void ParseProfilePage_NonJsonResponse_ThrowsRemoteDetailNotJson(string body)
    {
        // A removed / private / blocked mod (e.g. mods/686817) returns HTML/plain text, not apiv11 JSON.
        // It must surface as a TYPED signal so the enrichment backfill skips just that entry.
        var act = () => GameBananaEngine.ParseProfilePage(body, "https://gamebanana.com/mods/686817");
        act.Should().Throw<OperationException>().Which.Code.Should().Be("REMOTE_DETAIL_NOT_JSON");
    }

    [Fact]
    public void BuildUrls_MatchApiV11Shape()
    {
        GameBananaEngine.BuildSubfeedUrl("https://gamebanana.com", "8552", 2)
            .Should().Be("https://gamebanana.com/apiv11/Game/8552/Subfeed?_nPage=2",
                "no _sSort — the default Subfeed order matches the site page");
        GameBananaEngine.BuildProfilePageUrl("https://gamebanana.com/", "529138")
            .Should().Be("https://gamebanana.com/apiv11/Mod/529138/ProfilePage");
    }
}
