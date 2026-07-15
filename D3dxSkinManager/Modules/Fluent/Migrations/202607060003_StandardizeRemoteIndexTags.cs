using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607060003)]
public class _202607060003_StandardizeRemoteIndexTags : FluentMigrator.Migration
{
    public override void Up()
    {
        // STANDARDIZED remote-index schema (remote-library-redesign.md): every engine stores the same
        // shape, and a mod carries a LIST of site tags (JSON array — e.g. GameBanana super+sub
        // category) instead of the single Category column. The index is a re-syncable CACHE, so we
        // drop + recreate rather than dragging dead columns along — one Update/Full-reindex rebuilds it.
        Delete.Table("RemoteIndexEntries");

        Create.Table("RemoteIndexEntries")
            .WithColumn("SourceId").AsString().NotNullable().PrimaryKey()
            .WithColumn("ListId").AsString().NotNullable().PrimaryKey()
            .WithColumn("EntryId").AsString().NotNullable().PrimaryKey()
            .WithColumn("Title").AsString().NotNullable().WithDefaultValue("")
            .WithColumn("DetailUrl").AsString().NotNullable()
            .WithColumn("ImageUrl").AsString().NotNullable().WithDefaultValue("")
            // JSON array of site tags, queried via json_each (filter + distinct counts).
            .WithColumn("Tags").AsString().Nullable()
            .WithColumn("DateHint").AsString().Nullable()
            .WithColumn("Generation").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("SortKey").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("FirstSeenUtc").AsDateTime().NotNullable()
            .WithColumn("LastSeenUtc").AsDateTime().NotNullable()
            .WithColumn("RemovedUtc").AsDateTime().Nullable();

        Create.Index("idx_remote_index_order")
            .OnTable("RemoteIndexEntries")
            .OnColumn("SourceId").Ascending()
            .OnColumn("ListId").Ascending()
            .OnColumn("Generation").Descending()
            .OnColumn("SortKey").Ascending();

        // Force a fresh crawl: stale meta would make the next sync "incremental" against an empty table.
        Delete.FromTable("RemoteIndexMeta").AllRows();
    }

    public override void Down()
    {
        // The Up drops+recreates this re-syncable CACHE table, so restoring the prior shape+data isn't
        // meaningful. Drop it so the migration's schema change is actually undone — keeping the
        // migration history consistent with the real schema on rollback. A re-Up recreates the table and
        // the next sync refills it.
        Delete.Table("RemoteIndexEntries");
    }
}
