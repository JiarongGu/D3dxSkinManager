using D3dxSkinManager.Modules.Workflow.Entities;

namespace D3dxSkinManager.Modules.Workflow.Models;

/// <summary>
/// Workflow model - same as entity (no runtime fields needed for simplicity)
/// </summary>
public class WorkflowInfo
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public WorkflowStatus Status { get; set; }
    public string Context { get; set; } = "{}";
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
