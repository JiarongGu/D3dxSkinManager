namespace D3dxSkinManager.Modules.Fluent.Migrations;

/// <summary>
/// Initial migration: Create Tags table
/// This represents the base schema for tag definitions
/// </summary>
[Migration(202603080003, Description = "Create Tags table")]
public class Migration_202603080003_CreateTagsTable : Migration
{
    public override void Up()
    {
        Create.Table("Tags")
            .WithColumn("Name").AsText().NotNullable().PrimaryKey()
            .WithColumn("Color").AsText().NotNullable()
            .WithColumn("CreatedAt").AsText().WithDefaultCurrentTimestamp()
            .WithColumn("UpdatedAt").AsText().WithDefaultCurrentTimestamp();

        Create.Index("idx_tags_name")
            .OnTable("Tags")
            .OnColumn("Name");
    }

    public override void Down()
    {
        Delete.Index("idx_tags_name");
        Delete.Table("Tags");
    }
}
