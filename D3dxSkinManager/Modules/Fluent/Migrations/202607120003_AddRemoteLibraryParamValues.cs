using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202607120003)]
public class _202607120003_AddRemoteLibraryParamValues : FluentMigrator.Migration
{
    public override void Up()
    {
        // A library's values for its source's declared Params (key→value, stored as a JSON object),
        // substituted into the effective source config for this library ({param.<key>}) by
        // RemoteSourceResolver. Nullable — libraries from before the parameterized-source model have none.
        // See remote-library-redesign.md.
        Alter.Table("RemoteLibraries").AddColumn("ParamValues").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("ParamValues").FromTable("RemoteLibraries");
    }
}
