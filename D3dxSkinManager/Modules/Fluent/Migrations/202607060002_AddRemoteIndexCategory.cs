using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607060002)]
public class _202607060002_AddRemoteIndexCategory : FluentMigrator.Migration
{
    public override void Up()
    {
        // The site's own category for each indexed mod (e.g. GameBanana root category "Skins").
        // Captured during crawl; powers the remote-library category filter + per-site i18n labels.
        Alter.Table("RemoteIndexEntries")
            .AddColumn("Category").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("Category").FromTable("RemoteIndexEntries");
    }
}
