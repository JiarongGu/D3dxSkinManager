using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Per-profile remote tag labels/aliases, now in the profile SQLite DB (RemoteTagLabels table). Regression
/// coverage for the cross-profile leak — labels edited in one profile MUST NOT appear in another (different
/// profile = different DB). Seed-once-from-global keeps shipped defaults; legacy JSON migrates in once.
/// </summary>
public class RemoteTagLabelStoreTests : IDisposable
{
    private readonly SqliteConnection _connA;
    private readonly SqliteConnection _connB;
    private readonly string _dirA;
    private readonly string _dirB;
    private readonly RemoteTagLabelStore _a;
    private readonly RemoteTagLabelStore _b;

    public RemoteTagLabelStoreTests()
    {
        (_connA, _dirA, _a) = MakeProfile();
        (_connB, _dirB, _b) = MakeProfile();
    }

    private static (SqliteConnection, string, RemoteTagLabelStore) MakeProfile()
    {
        var connStr = $"Data Source=file:tl_{Guid.NewGuid():N}?mode=memory&cache=shared";
        var conn = new SqliteConnection(connStr);
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE RemoteTagLabels (
                SourceId TEXT NOT NULL, Lang TEXT NOT NULL, RawTag TEXT NOT NULL,
                Label TEXT NOT NULL DEFAULT '', PRIMARY KEY (SourceId, Lang, RawTag));";
            cmd.ExecuteNonQuery();
        }
        var dir = Path.Combine(Path.GetTempPath(), $"d3dx-tl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var paths = new Mock<IProfilePathService>();
        paths.Setup(p => p.ProfileDatabasePath).Returns(connStr);
        paths.Setup(p => p.ProfilePath).Returns(dir);
        var repo = new RemoteTagLabelRepository(paths.Object);
        return (conn, dir, new RemoteTagLabelStore(repo, paths.Object, Mock.Of<ILogHelper>()));
    }

    public void Dispose()
    {
        _connA.Close(); _connB.Close();
        _connA.Dispose(); _connB.Dispose();
        try { Directory.Delete(_dirA, true); } catch { }
        try { Directory.Delete(_dirB, true); } catch { }
    }

    private static Dictionary<string, Dictionary<string, string>> Global() => new()
    {
        ["cn"] = new() { ["Skins"] = "皮肤" },
        ["en"] = new() { ["Skins"] = "Skins" },
    };

    [Fact]
    public void EditInOneProfile_DoesNotLeakToAnother()
    {
        _a.SetLangLabels("huihui", "cn", new() { ["Skins"] = "A-皮肤" }, Global());

        _a.GetForSource("huihui", Global())["cn"]["Skins"].Should().Be("A-皮肤");
        _b.GetForSource("huihui", Global())["cn"]["Skins"].Should().Be("皮肤", "profile B is a separate DB, unaffected by A's edit");
    }

    [Fact]
    public void GetForSource_SeedsOnceFromGlobal_ThenIgnoresLaterGlobalChanges()
    {
        _a.GetForSource("huihui", Global())["cn"]["Skins"].Should().Be("皮肤");

        var changedGlobal = new Dictionary<string, Dictionary<string, string>> { ["cn"] = new() { ["Skins"] = "GLOBAL-CHANGED" } };
        _a.GetForSource("huihui", changedGlobal)["cn"]["Skins"].Should().Be("皮肤", "the seeded profile copy is authoritative");
    }

    [Fact]
    public void SetLangLabels_PreservesOtherLanguagesFromGlobal()
    {
        _a.SetLangLabels("huihui", "cn", new() { ["Skins"] = "A-皮肤" }, Global());

        var effective = _a.GetForSource("huihui", Global());
        effective["cn"]["Skins"].Should().Be("A-皮肤");
        effective["en"]["Skins"].Should().Be("Skins", "the untouched language keeps its seeded default");
    }

    [Fact]
    public void SetLangLabels_DropsBlankPairs()
    {
        _a.SetLangLabels("huihui", "cn", new() { ["Skins"] = "皮肤", ["  "] = "x", ["Empty"] = "  " }, null);
        var cn = _a.GetForSource("huihui", null)["cn"];
        cn.Should().ContainKey("Skins").WhoseValue.Should().Be("皮肤");
        cn.Should().HaveCount(1);
    }

    [Fact]
    public void GetForSource_NoGlobalNoProfile_ReturnsEmpty()
    {
        _a.GetForSource("unknown", null).Should().BeEmpty();
    }

    [Fact]
    public void MigratesLegacyJson_IntoSqlite_ThenDeletesJson()
    {
        File.WriteAllText(Path.Combine(_dirA, "remote-tag-labels.json"),
            """{ "huihui": { "cn": { "Skins": "旧-皮肤" } } }""");

        _a.GetForSource("huihui", Global())["cn"]["Skins"].Should().Be("旧-皮肤", "the legacy JSON value migrated in");
        File.Exists(Path.Combine(_dirA, "remote-tag-labels.json")).Should().BeFalse("the migrated JSON is removed");
    }
}
