using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202604120001)]
public class _202604120001_CreateModPresetsTable : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("ModPresets")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("ModIds").AsString().NotNullable() // JSON array of mod IDs
            .WithColumn("CreatedAt").AsString().NotNullable()
            .WithColumn("UpdatedAt").AsString().NotNullable();

        Create.Index("IX_ModPresets_Name")
            .OnTable("ModPresets")
            .OnColumn("Name")
            .Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_ModPresets_Name").OnTable("ModPresets");
        Delete.Table("ModPresets");
    }
}
