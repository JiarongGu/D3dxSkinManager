using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607120002)]
public class _202607120002_AddRemoteTagLabelsAndSourcesTables : FluentMigrator.Migration
{
    public override void Up()
    {
        // "Everything remote is driven from SQLite": the remaining per-profile remote data moves off JSON.
        // Site ADAPTER configs (the global {data}/remote-sources/*.json) stay as the editable DEFINITION,
        // but are mirrored per-profile into RemoteSources and synced on load (JSON changed → upsert).
        // Tag labels/aliases move off {profile}/remote-tag-labels.json into RemoteTagLabels. See
        // remote-library-redesign.md.

        // Per-profile tag labels: sourceId → lang → rawTag → label.
        Create.Table("RemoteTagLabels")
            .WithColumn("SourceId").AsString().NotNullable().PrimaryKey()
            .WithColumn("Lang").AsString().NotNullable().PrimaryKey()
            .WithColumn("RawTag").AsString().NotNullable().PrimaryKey()
            .WithColumn("Label").AsString().NotNullable().WithDefaultValue("");

        // Per-profile mirror of the site adapter configs. The full RemoteSourceConfig is stored as JSON in
        // one column (a nested config read whole); the JSON files remain the definition + sync source.
        Create.Table("RemoteSources")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("ConfigJson").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("RemoteTagLabels");
        Delete.Table("RemoteSources");
    }
}
