using D3dxSkinManager.Modules.Workflow.Models;

namespace D3dxSkinManager.Modules.Workflow.Entities;

/// <summary>
/// Extension methods for mapping between Workflow entities and models
/// </summary>
public static class WorkflowEntityMappers
{
    /// <summary>
    /// Convert WorkflowEntity (DB) to WorkflowInfo (model)
    /// </summary>
    public static WorkflowInfo ToModel(this WorkflowEntity entity)
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
    /// Convert WorkflowInfo (model) to WorkflowEntity (DB)
    /// </summary>
    public static WorkflowEntity ToEntity(this WorkflowInfo model)
    {
        return new WorkflowEntity
        {
            Id = model.Id,
            Type = model.Type,
            Status = model.Status,
            Context = model.Context,
            ErrorMessage = model.ErrorMessage,
            CreatedAt = model.CreatedAt,
            CompletedAt = model.CompletedAt
        };
    }
}
