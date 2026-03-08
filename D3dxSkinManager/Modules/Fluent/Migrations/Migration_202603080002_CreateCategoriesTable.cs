namespace D3dxSkinManager.Modules.Fluent.Migrations;

/// <summary>
/// Initial migration: Create Categories table
/// This represents the base schema for category tree structure
/// </summary>
[Migration(202603080002, Description = "Create Categories table")]
public class Migration_202603080002_CreateCategoriesTable : Migration
{
    public override void Up()
    {
        Create.Table("Categories")
            .WithColumn("Id").AsText().NotNullable().PrimaryKey()
            .WithColumn("Name").AsText().NotNullable().Unique().Collate("NOCASE")
            .WithColumn("ParentId").AsText().Nullable()
            .WithColumn("ThumbnailPath").AsText().Nullable()
            .WithColumn("Priority").AsInteger().WithDefaultValue(0)
            .WithColumn("Description").AsText().Nullable()
            .WithColumn("Metadata").AsText().Nullable()
            .WithColumn("CreatedAt").AsText().WithDefaultCurrentTimestamp()
            .WithColumn("UpdatedAt").AsText().WithDefaultCurrentTimestamp();

        Create.Index("idx_Categories_parent")
            .OnTable("Categories")
            .OnColumn("ParentId");

        Create.Index("idx_Categories_priority")
            .OnTable("Categories")
            .OnColumn("Priority")
            .Descending();
    }

    public override void Down()
    {
        Delete.Index("idx_Categories_priority");
        Delete.Index("idx_Categories_parent");
        Delete.Table("Categories");
    }
}
