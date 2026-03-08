namespace D3dxSkinManager.Modules.Fluent.Migrations;

/// <summary>
/// Initial migration: Create Mods table
/// This represents the base schema for mod storage
/// </summary>
[Migration(202603080001, Description = "Create Mods table")]
public class Migration_202603080001_CreateModsTable : Migration
{
    public override void Up()
    {
        Create.Table("Mods")
            .WithColumn("SHA").AsText().NotNullable().PrimaryKey()
            .WithColumn("Category").AsText().NotNullable()
            .WithColumn("Name").AsText().NotNullable()
            .WithColumn("Author").AsText().Nullable()
            .WithColumn("Description").AsText().Nullable()
            .WithColumn("Type").AsText().WithDefaultValue("7z")
            .WithColumn("Grading").AsText().WithDefaultValue("G")
            .WithColumn("Tags").AsText().Nullable()
            .WithColumn("DisablePreview").AsInteger().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsText().WithDefaultCurrentTimestamp()
            .WithColumn("UpdatedAt").AsText().WithDefaultCurrentTimestamp()
            .WithColumn("Metadata").AsText().Nullable();

        Create.Index("idx_mods_category")
            .OnTable("Mods")
            .OnColumn("Category");

        Create.Index("idx_mods_author")
            .OnTable("Mods")
            .OnColumn("Author");
    }

    public override void Down()
    {
        Delete.Index("idx_mods_author");
        Delete.Index("idx_mods_category");
        Delete.Table("Mods");
    }
}
