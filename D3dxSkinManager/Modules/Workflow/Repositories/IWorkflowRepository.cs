using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Entities;

namespace D3dxSkinManager.Modules.Workflow.Repositories;

/// <summary>
/// Repository interface for WorkflowInfo operations (generic CRUD)
/// </summary>
public interface IWorkflowRepository
{
    Task<WorkflowInfo> AddAsync(WorkflowInfo workflow);
    Task<WorkflowInfo?> GetByIdAsync(string id);
    Task<List<WorkflowInfo>> GetByTypeAsync(string type);
    Task<List<WorkflowInfo>> GetActiveByTypeAsync(string type);
    Task UpdateAsync(WorkflowInfo workflow);
    Task DeleteAsync(string id);
    Task<int> DeleteBatchAsync(IEnumerable<string> ids);
    Task<List<WorkflowInfo>> GetByIdsAsync(IEnumerable<string> ids);
}
