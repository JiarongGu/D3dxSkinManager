using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607050002)]
public class _202607050002_CreateRemoteIndexTables : FluentMigrator.Migration
{
    public override void Up()
    {
        // The synced remote-library index, PER PROFILE (a profile binds one game). Replaces the
        // global JSON caches ({data}/remote-sources/.cache) — SQLite gives filtered/paged queries
        // + incremental UPDATE syncs without rewriting a whole file. See remote-library.md.
        Create.Table("RemoteIndexEntries")
            .WithColumn("SourceId").AsString().NotNullable().PrimaryKey()
            .WithColumn("ListId").AsString().NotNullable().PrimaryKey()
            .WithColumn("EntryId").AsString().NotNullable().PrimaryKey()
            .WithColumn("Title").AsString().NotNullable().WithDefaultValue("")
            .WithColumn("DetailUrl").AsString().NotNullable()
            .WithColumn("ImageUrl").AsString().NotNullable().WithDefaultValue("")
            .WithColumn("DateHint").AsString().Nullable()
            // Sync generation: an UPDATE sync only recrawls the newest pages; crawled entries get
            // the new generation so (Generation DESC, SortKey ASC) preserves site recency order
            // across partial crawls.
            .WithColumn("Generation").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("SortKey").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("FirstSeenUtc").AsDateTime().NotNullable()
            .WithColumn("LastSeenUtc").AsDateTime().NotNullable();

        Create.Index("idx_remote_index_order")
            .OnTable("RemoteIndexEntries")
            .OnColumn("SourceId").Ascending()
            .OnColumn("ListId").Ascending()
            .OnColumn("Generation").Descending()
            .OnColumn("SortKey").Ascending();

        Create.Table("RemoteIndexMeta")
            .WithColumn("SourceId").AsString().NotNullable().PrimaryKey()
            .WithColumn("ListId").AsString().NotNullable().PrimaryKey()
            .WithColumn("SyncedAtUtc").AsDateTime().Nullable()
            .WithColumn("TotalPages").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Generation").AsInt64().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Table("RemoteIndexEntries");
        Delete.Table("RemoteIndexMeta");
    }
}
