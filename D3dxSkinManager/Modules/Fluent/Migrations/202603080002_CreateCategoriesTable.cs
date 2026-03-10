using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

/// <summary>
/// Initial migration: Create Categories table
/// This represents the base schema for category tree structure
/// </summary>
[Migration(202603080002)]
public class _202603080002_CreateCategoriesTable : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("Categories")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable().Unique()
            .WithColumn("ParentId").AsString().Nullable()
            .WithColumn("ThumbnailPath").AsString().Nullable()
            .WithColumn("Priority").AsInt32().WithDefaultValue(0)
            .WithColumn("Description").AsString().Nullable()
            .WithColumn("Metadata").AsString().Nullable()
            .WithColumn("CreatedAt").AsString().NotNullable()
            .WithColumn("UpdatedAt").AsString().NotNullable();

        Create.Index("idx_Categories_parent")
            .OnTable("Categories")
            .OnColumn("ParentId");

        Create.Index("idx_Categories_priority")
            .OnTable("Categories")
            .OnColumn("Priority").Descending();
    }

    public override void Down()
    {
        Delete.Index("idx_Categories_priority").OnTable("Categories");
        Delete.Index("idx_Categories_parent").OnTable("Categories");
        Delete.Table("Categories");
    }
}
