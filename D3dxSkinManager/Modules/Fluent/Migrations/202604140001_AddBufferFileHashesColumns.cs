using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202604140001)]
public class _202604140001_AddBufferFileHashesColumns : FluentMigrator.Migration
{
    public override void Up()
    {
        // Per-file hashes for subset/overlap duplicate detection (merged mod support)
        Alter.Table("AnalysisFindings")
            .AddColumn("BufferFileHashes").AsString().NotNullable().WithDefaultValue("[]")
            .AddColumn("TextureFileHashes").AsString().NotNullable().WithDefaultValue("[]");
    }

    public override void Down()
    {
        Delete.Column("BufferFileHashes").FromTable("AnalysisFindings");
        Delete.Column("TextureFileHashes").FromTable("AnalysisFindings");
    }
}
