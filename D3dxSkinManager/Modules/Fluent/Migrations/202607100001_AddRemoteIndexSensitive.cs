using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607100001)]
public class _202607100001_AddRemoteIndexSensitive : FluentMigrator.Migration
{
    public override void Up()
    {
        // The SITE's content rating for the entry (GameBanana _sInitialVisibility):
        // 1 = site-rated sensitive (content veil always covers it), 0 = site-rated safe (never
        // veiled), NULL = the site doesn't say — the image analysis decides. Populated by sync;
        // pre-existing rows stay NULL (image-analysis fallback) until a sync re-sees them.
        Alter.Table("RemoteIndexEntries")
            .AddColumn("Sensitive").AsBoolean().Nullable();
    }

    public override void Down()
    {
        Delete.Column("Sensitive").FromTable("RemoteIndexEntries");
    }
}
