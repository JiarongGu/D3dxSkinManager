using D3dxSkinManager.Modules.Workflow.Handlers;
using D3dxSkinManager.Modules.Workflow.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace D3dxSkinManager.Modules.Workflow;

/// <summary>
/// Service registration for Workflow module
/// </summary>
public static class WorkflowServiceExtensions
{
    public static IServiceCollection AddWorkflowServices(this IServiceCollection services)
    {
        // Repository
        services.AddSingleton<IWorkflowRepository, WorkflowRepository>();

        // Workflow handlers
        services.AddSingleton<ModImportWorkflowHandler>();

        // Facade
        services.AddSingleton<IWorkflowFacade, WorkflowFacade>();

        return services;
    }
}
