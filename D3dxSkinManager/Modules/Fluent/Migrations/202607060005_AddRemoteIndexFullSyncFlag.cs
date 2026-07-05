using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607060005)]
public class _202607060005_AddRemoteIndexFullSyncFlag : FluentMigrator.Migration
{
    public override void Up()
    {
        // When a COMPLETE pass over every list page last finished. Incremental (early-stopping)
        // updates are only sound once a full pass exists — a partial/cancelled first crawl followed
        // by incrementals left a permanent hole in the index (the early stop never reached the
        // uncrawled deep pages). NULL = no complete pass yet → the next sync crawls everything.
        Alter.Table("RemoteIndexMeta")
            .AddColumn("FullSyncCompletedUtc").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Column("FullSyncCompletedUtc").FromTable("RemoteIndexMeta");
    }
}
