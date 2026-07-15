using D3dxSkinManager.Modules.Workflow.Models;

namespace D3dxSkinManager.Modules.Workflow.Handlers;

/// <summary>
/// Optional handler capability: bulk-enrich a list of this handler's workflows with display-only fields
/// (e.g. category names) before they are returned to the UI, in a single query (avoids N+1). Handlers
/// that don't need it simply don't implement it — the facade skips enrichment via a capability check,
/// with no concrete-type downcast or per-type string check.
/// </summary>
public interface IWorkflowListEnricher
{
    Task EnrichWorkflowsAsync(List<WorkflowInfo> workflows);
}
