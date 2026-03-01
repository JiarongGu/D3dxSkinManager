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

        // Workflow handlers - register as both concrete type and interface
        services.AddSingleton<ModImportWorkflowHandler>();
        services.AddSingleton<IWorkflowHandler>(sp => sp.GetRequiredService<ModImportWorkflowHandler>());

        // Facade - will receive all registered IWorkflowHandler instances
        services.AddSingleton<IWorkflowFacade, WorkflowFacade>();

        return services;
    }
}
