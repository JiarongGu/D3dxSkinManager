using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202604130001)]
public class _202604130001_CreateModAnalysisTable : FluentMigrator.Migration
{
    public override void Up()
    {
        // Session = one analysis run (full or per-category)
        Create.Table("AnalysisSessions")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("CategoryId").AsString().Nullable()
            .WithColumn("CategoryName").AsString().Nullable()
            .WithColumn("Status").AsString().NotNullable().WithDefaultValue("running")
            .WithColumn("TotalMods").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("AnalyzedCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("HealthyCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("WarningCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ErrorCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IdenticalCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("TextureVariantCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ConflictCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("StartedAt").AsString().NotNullable()
            .WithColumn("CompletedAt").AsString().Nullable();

        // Per-mod findings within a session
        Create.Table("AnalysisFindings")
            .WithColumn("Id").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("SessionId").AsString().NotNullable()
            .WithColumn("ModId").AsString().NotNullable()
            .WithColumn("TargetHashes").AsString().NotNullable().WithDefaultValue("[]")
            .WithColumn("BufferHash").AsString().NotNullable().WithDefaultValue("")
            .WithColumn("TextureHash").AsString().NotNullable().WithDefaultValue("")
            .WithColumn("HealthStatus").AsString().NotNullable().WithDefaultValue("unknown")
            .WithColumn("HealthIssues").AsString().NotNullable().WithDefaultValue("[]")
            .WithColumn("PluginDependencies").AsString().NotNullable().WithDefaultValue("[]")
            .WithColumn("IniFileCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ResourceFileCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("TextureOverrideCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("BufferSizeBytes").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("TextureSizeBytes").AsInt64().NotNullable().WithDefaultValue(0);

        Create.Index("IX_AnalysisFindings_SessionId")
            .OnTable("AnalysisFindings")
            .OnColumn("SessionId").Ascending();

        Create.Index("IX_AnalysisFindings_SessionMod")
            .OnTable("AnalysisFindings")
            .WithOptions().Unique()
            .OnColumn("SessionId").Ascending()
            .OnColumn("ModId").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_AnalysisFindings_SessionMod").OnTable("AnalysisFindings");
        Delete.Index("IX_AnalysisFindings_SessionId").OnTable("AnalysisFindings");
        Delete.Table("AnalysisFindings");
        Delete.Table("AnalysisSessions");
    }
}
