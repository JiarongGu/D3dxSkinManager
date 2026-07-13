using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607130003)]
public class _202607130003_AddRemoteLibraryPreferCache : FluentMigrator.Migration
{
    public override void Up()
    {
        // Per-library detail fetch mode (library editor → Detail): 0 = live-first (default — fetch the
        // fresh page, fall back to the saved copy only if the site is unreachable), 1 = cache-first (serve
        // the saved copy immediately; the detail page's Refresh button pulls live on demand).
        Alter.Table("RemoteLibraries")
            .AddColumn("PreferCache").AsBoolean().NotNullable().WithDefaultValue(false);
    }

    public override void Down()
    {
        Delete.Column("PreferCache").FromTable("RemoteLibraries");
    }
}
