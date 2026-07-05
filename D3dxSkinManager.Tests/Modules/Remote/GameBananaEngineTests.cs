using System.Linq;
using FluentAssertions;
using Xunit;
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
        { "_idRow": 1, "_sModelName": "Mod", "_sName": "Vivian Vampire", "_sProfileUrl": "https://gamebanana.com/mods/1",
          "_aPreviewMedia": { "_aImages": [ { "_sType": "screenshot", "_sBaseUrl": "https://images.gamebanana.com/img/ss/mods", "_sFile": "raw.jpg", "_sFile530": "530-90_raw.jpg", "_sFile220": "220-90_raw.jpg" } ] } },
        { "_idRow": 2, "_sModelName": "Sound", "_sName": "Not a mod", "_sProfileUrl": "https://gamebanana.com/sounds/2" },
        { "_idRow": 3, "_sModelName": "Mod", "_sName": "", "_sProfileUrl": "https://gamebanana.com/mods/3" }
      ]
    }
    """;

    private const string ProfilePage = """
    {
      "_idRow": 529138,
      "_sName": "Vivian Vampire",
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

        // Only the one valid Mod record (Sound filtered; empty-name mod dropped).
        result.Cards.Should().HaveCount(1);
        var card = result.Cards[0];
        card.Title.Should().Be("Vivian Vampire");
        card.DetailUrl.Should().Be("https://gamebanana.com/mods/1");
        card.ImageUrl.Should().Be("https://images.gamebanana.com/img/ss/mods/530-90_raw.jpg", "cards use the 530px variant");
        result.TotalPages.Should().Be(2, "ceil(30 / 15)");
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
    }

    [Theory]
    [InlineData("https://gamebanana.com/mods/529138", "529138")]
    [InlineData("https://gamebanana.com/mods/1?foo=bar", "1")]
    [InlineData("https://gamebanana.com/members/3382476", null)]
    public void ExtractModId_ReadsTheModId(string url, string? expected)
    {
        GameBananaEngine.ExtractModId(url).Should().Be(expected);
    }

    [Fact]
    public void BuildUrls_MatchApiV11Shape()
    {
        GameBananaEngine.BuildSubfeedUrl("https://gamebanana.com", "8552", 2)
            .Should().Be("https://gamebanana.com/apiv11/Game/8552/Subfeed?_nPage=2&_sSort=new");
        GameBananaEngine.BuildProfilePageUrl("https://gamebanana.com/", "529138")
            .Should().Be("https://gamebanana.com/apiv11/Mod/529138/ProfilePage");
    }
}
