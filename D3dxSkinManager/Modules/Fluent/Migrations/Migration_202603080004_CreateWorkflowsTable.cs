namespace D3dxSkinManager.Modules.Fluent.Migrations;

/// <summary>
/// Initial migration: Create Workflows table
/// This represents the base schema for workflow management
/// </summary>
[Migration(202603080004, Description = "Create Workflows table")]
public class Migration_202603080004_CreateWorkflowsTable : Migration
{
    public override void Up()
    {
        Create.Table("Workflows")
            .WithColumn("Id").AsText().NotNullable().PrimaryKey()
            .WithColumn("Type").AsText().NotNullable()
            .WithColumn("Status").AsInteger().NotNullable()
            .WithColumn("Context").AsText().NotNullable().WithDefaultValue("{}")
            .WithColumn("ErrorMessage").AsText().Nullable()
            .WithColumn("CreatedAt").AsText().NotNullable()
            .WithColumn("CompletedAt").AsText().Nullable();

        Create.Index("idx_workflows_type")
            .OnTable("Workflows")
            .OnColumn("Type");

        Create.Index("idx_workflows_status")
            .OnTable("Workflows")
            .OnColumn("Status");
    }

    public override void Down()
    {
        Delete.Index("idx_workflows_status");
        Delete.Index("idx_workflows_type");
        Delete.Table("Workflows");
    }
}
