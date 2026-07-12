using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607130001)]
public class _202607130001_AddModStateToModPresets : FluentMigrator.Migration
{
    public override void Up()
    {
        // Optional JSON array of the mods' persisted d3dx_user.ini var lines, captured with the preset so
        // applying it restores each mod's 3DMigoto toggle/variant state ("mod state"). Null when the preset
        // didn't capture it. See D3dmigotoUserConfigService.
        Alter.Table("ModPresets").AddColumn("ModState").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("ModState").FromTable("ModPresets");
    }
}
