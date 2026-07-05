using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607060004)]
public class _202607060004_AddRemoteIndexEnriched : FluentMigrator.Migration
{
    public override void Up()
    {
        // When this entry's DETAIL page was processed during sync (tag enrichment — e.g. GameBanana's
        // sub category only exists on the detail page). NULL = not yet enriched; the sync's
        // enrichment phase works through NULL rows newest-first.
        Alter.Table("RemoteIndexEntries")
            .AddColumn("EnrichedUtc").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Column("EnrichedUtc").FromTable("RemoteIndexEntries");
    }
}
