using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607050001)]
public class _202607050001_AddIniFingerprintsColumn : FluentMigrator.Migration
{
    public override void Up()
    {
        // Per-aspect hashes of a mod's ACTIVE .ini contents, JSON: {"key","constants","logic"}.
        // Lets duplicate grouping split "exact clone" from "same assets, DIFFERENT ini" AND say
        // WHAT differs (hash-fix / keybindings / defaults / logic) — dedup taxonomy case 2.
        Alter.Table("AnalysisFindings")
            .AddColumn("IniFingerprints").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("IniFingerprints").FromTable("AnalysisFindings");
    }
}
