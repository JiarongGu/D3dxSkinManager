using D3dxSkinManager.Modules.Workflow.Models;

namespace D3dxSkinManager.Modules.Workflow.Entities;

/// <summary>
/// Extension methods for mapping between Workflow entities and models
/// </summary>
public static class WorkflowEntityMappers
{
    /// <summary>
    /// Convert WorkflowEntity (DB) to WorkflowInfo (domain)
    /// </summary>
    public static WorkflowInfo ToDomain(this WorkflowEntity entity)
    {
        return new WorkflowInfo
        {
            Id = entity.Id,
            Type = entity.Type,
            Status = entity.Status,
            Context = entity.Context,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt,
            CompletedAt = entity.CompletedAt
        };
    }

    /// <summary>
    /// Convert WorkflowInfo (domain) to WorkflowEntity (DB)
    /// </summary>
    public static WorkflowEntity ToEntity(this WorkflowInfo domain)
    {
        return new WorkflowEntity
        {
            Id = domain.Id,
            Type = domain.Type,
            Status = domain.Status,
            Context = domain.Context,
            ErrorMessage = domain.ErrorMessage,
            CreatedAt = domain.CreatedAt,
            CompletedAt = domain.CompletedAt
        };
    }

    /// <summary>
    /// Convert list of WorkflowEntity to list of WorkflowInfo
    /// </summary>
    public static List<WorkflowInfo> ToDomainList(this IEnumerable<WorkflowEntity> entities)
    {
        return entities.Select(ToDomain).ToList();
    }
}
