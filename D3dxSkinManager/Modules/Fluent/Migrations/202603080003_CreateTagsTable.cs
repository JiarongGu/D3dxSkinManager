using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

/// <summary>
/// Initial migration: Create Tags table
/// This represents the base schema for tag definitions
/// </summary>
[Migration(202603080003)]
public class _202603080003_CreateTagsTable : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("Tags")
            .WithColumn("Name").AsString().NotNullable().PrimaryKey()
            .WithColumn("Color").AsString().NotNullable()
            .WithColumn("CreatedAt").AsString().NotNullable()
            .WithColumn("UpdatedAt").AsString().NotNullable();

        Create.Index("idx_tags_name")
            .OnTable("Tags")
            .OnColumn("Name");
    }

    public override void Down()
    {
        Delete.Index("idx_tags_name").OnTable("Tags");
        Delete.Table("Tags");
    }
}
