using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

/// <summary>
/// Initial migration: Create Mods table
/// This represents the base schema for mod storage
/// </summary>
[Migration(202603080001)]
public class _202603080001_CreateModsTable : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("Mods")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("Category").AsString().NotNullable()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("Author").AsString().Nullable()
            .WithColumn("Description").AsString().Nullable()
            .WithColumn("Type").AsString().WithDefaultValue("7z")
            .WithColumn("Grading").AsString().WithDefaultValue("G")
            .WithColumn("Tags").AsString().Nullable()
            .WithColumn("DisablePreview").AsInt32().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsString().NotNullable()
            .WithColumn("UpdatedAt").AsString().NotNullable()
            .WithColumn("Metadata").AsString().Nullable();

        Create.Index("idx_mods_category")
            .OnTable("Mods")
            .OnColumn("Category");

        Create.Index("idx_mods_author")
            .OnTable("Mods")
            .OnColumn("Author");
    }

    public override void Down()
    {
        Delete.Index("idx_mods_author").OnTable("Mods");
        Delete.Index("idx_mods_category").OnTable("Mods");
        Delete.Table("Mods");
    }
}
