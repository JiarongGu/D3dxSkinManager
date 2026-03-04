namespace D3dxSkinManager.Modules.Workflow;

/// <summary>
/// Structured error information for workflow failures
/// Serialized to JSON and stored in workflow.ErrorMessage
/// Frontend parses this to display localized error messages
/// </summary>
public class WorkflowError
{
    /// <summary>
    /// Error code for i18n lookup (e.g., "WORKFLOW_DUPLICATE_MOD")
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Optional parameters for error message interpolation
    /// Example: { "name": "MyMod", "sha": "abc123..." }
    /// Frontend uses these with i18n: t('workflow.errors.WORKFLOW_DUPLICATE_MOD', { name: 'MyMod' })
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }
}
