using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607120001)]
public class _202607120001_AddRemoteLibrariesTableAndModRef : FluentMigrator.Migration
{
    public override void Up()
    {
        // The per-profile configured remote LIBRARIES — moved out of {profile}/remote-libraries.json into
        // SQLite so library data is native to SQL (joinable/queryable). Site ADAPTERS stay GLOBAL JSON
        // ({data}/remote-sources). The one-time JSON→table data migration runs in RemoteLibraryStore on
        // first read (it needs file access the SQL layer doesn't have). See remote-library-redesign.md.
        Create.Table("RemoteLibraries")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("SourceId").AsString().NotNullable()
            .WithColumn("ListId").AsString().NotNullable()
            .WithColumn("Name").AsString().NotNullable().WithDefaultValue("")
            .WithColumn("TagRules").AsString().Nullable()                       // JSON array of RemoteTagRule
            .WithColumn("Active").AsInt32().NotNullable().WithDefaultValue(0)   // exactly one row is 1
            .WithColumn("SortOrder").AsInt64().NotNullable().WithDefaultValue(0)// preserves the JSON list order
            .WithColumn("AddedAtUtc").AsDateTime().NotNullable();

        Create.Index("idx_remote_libraries_sourcelist")
            .OnTable("RemoteLibraries")
            .OnColumn("SourceId").Ascending()
            .OnColumn("ListId").Ascending();

        // A mod imported from a remote library references it by FK (RemoteLibraries.Id). The library
        // entity owns the name + config; the mod just points at it. Nullable (non-remote mods, or a mod
        // whose library was removed → nulled). Not backfilled here: RemoteLibraries is populated later
        // (RemoteLibraryStore migrates the JSON on first access), so the one-time backfill that maps a
        // mod's metadata.remote (sourceId+listId) to a library Id runs in code once libraries exist
        // (ModRepository.BackfillRemoteLibraryReferences, kicked off at profile init).
        Alter.Table("Mods").AddColumn("RemoteLibraryId").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Table("RemoteLibraries");
        Delete.Column("RemoteLibraryId").FromTable("Mods");
    }
}
