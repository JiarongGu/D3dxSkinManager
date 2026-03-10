using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

/// <summary>
/// Initial migration: Create Workflows table
/// This represents the base schema for workflow management
/// </summary>
[Migration(202603080004)]
public class _202603080004_CreateWorkflowsTable : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Table("Workflows")
            .WithColumn("Id").AsString().NotNullable().PrimaryKey()
            .WithColumn("Type").AsString().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("Context").AsString().NotNullable().WithDefaultValue("{}")
            .WithColumn("ErrorMessage").AsString().Nullable()
            .WithColumn("CreatedAt").AsString().NotNullable()
            .WithColumn("CompletedAt").AsString().Nullable();

        Create.Index("idx_workflows_type")
            .OnTable("Workflows")
            .OnColumn("Type");

        Create.Index("idx_workflows_status")
            .OnTable("Workflows")
            .OnColumn("Status");
    }

    public override void Down()
    {
        Delete.Index("idx_workflows_status").OnTable("Workflows");
        Delete.Index("idx_workflows_type").OnTable("Workflows");
        Delete.Table("Workflows");
    }
}
