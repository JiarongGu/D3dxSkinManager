using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202603100002)]
public class _202603100002_CreateScreenCaptureProfilesTable : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("ScreenCaptureProfiles")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("Name").AsString().NotNullable()
            .WithColumn("X").AsInt32().NotNullable()
            .WithColumn("Y").AsInt32().NotNullable()
            .WithColumn("Width").AsInt32().NotNullable()
            .WithColumn("Height").AsInt32().NotNullable();

        Create.Index("IX_ScreenCaptureProfiles_Name")
            .OnTable("ScreenCaptureProfiles")
            .OnColumn("Name")
            .Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_ScreenCaptureProfiles_Name").OnTable("ScreenCaptureProfiles");
        Delete.Table("ScreenCaptureProfiles");
    }
}
