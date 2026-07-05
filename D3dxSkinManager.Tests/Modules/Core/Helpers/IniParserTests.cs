using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Modules.Core.Helpers;

public class IniParserTests
{
    [Fact]
    public void Parse_SectionsAndEntries_ShouldClassifyKeysAndRawLines()
    {
        var doc = IniParser.Parse(new[]
        {
            "[TextureOverrideBody]",
            "hash = 5aeb1350",
            "if $swap == 1",
            "  vb0 = ResourceBody.0",
            "endif",
        });

        doc.Sections.Should().HaveCount(1);
        var s = doc.Sections[0];
        s.Name.Should().Be("TextureOverrideBody");
        s.GetValue("hash").Should().Be("5aeb1350");
        s.Entries.Should().HaveCount(4);
        s.Entries[1].Key.Should().BeNull("control-flow lines have no key");
        s.Entries[1].Raw.Should().Be("if $swap == 1");
        s.Entries[3].Raw.Should().Be("endif");
    }

    [Fact]
    public void Parse_FullwidthComments_AreSkippedLikeAscii()
    {
        // Real mods mix ';' and fullwidth '；' comments — both full-line and inline.
        var doc = IniParser.Parse(new[]
        {
            "; ascii comment",
            "；fullwidth comment",
            "[Constants]",
            "global $x = 1 ; inline",
            "global $y = 2 ；inline fullwidth",
            "；global $z = 3",
        });

        var s = doc.Sections[0];
        s.Entries.Should().HaveCount(2);
        s.GetValue("global $x").Should().Be("1");
        s.GetValue("global $y").Should().Be("2");
    }

    [Fact]
    public void Parse_NamespaceDirective_MustBeFirstMeaningfulLine()
    {
        var doc = IniParser.Parse(new[] { "; header", "namespace = global\\Merge\\mod0", "[Key1]", "key = j" });
        doc.Namespace.Should().Be("global\\Merge\\mod0");

        // Not first → not a namespace
        var late = IniParser.Parse(new[] { "[Constants]", "namespace = too\\late" });
        late.Namespace.Should().BeNull();
    }

    [Theory]
    [InlineData(@"DISABLEDmerged.ini", true)]
    [InlineData(@"disabled\mod.ini", true)]
    [InlineData(@"sub\DISABLED-old\mod.ini", true)]
    [InlineData(@"mod.ini", false)]
    [InlineData(@"sub\mod.ini", false)]
    [InlineData(@"MyDisabler\mod.ini", false)]
    public void IsDisabledPath_MatchesSegmentPrefixOnly(string path, bool expected)
    {
        IniParser.IsDisabledPath(path).Should().Be(expected);
    }
}
