using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607060001)]
public class _202607060001_AddRemoteIndexRemovedColumn : FluentMigrator.Migration
{
    public override void Up()
    {
        // Soft-delete marker for the remote index. A FULL reindex re-crawls every page and marks
        // entries it no longer sees (Generation < the crawl's generation) as removed instead of
        // deleting them — so a downloaded mod's reference row survives and the card still resolves.
        // Incremental UPDATE syncs stop early and never prune. Queries exclude RemovedUtc IS NOT NULL.
        Alter.Table("RemoteIndexEntries")
            .AddColumn("RemovedUtc").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Column("RemovedUtc").FromTable("RemoteIndexEntries");
    }
}
