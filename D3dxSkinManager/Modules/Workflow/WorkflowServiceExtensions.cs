using D3dxSkinManager.Modules.Workflow.Handlers;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Services;
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

        // Services — the import queue actor (mailbox + single loop) replaces the old
        // WorkflowConcurrencyManager (per-item Task.Run self-awaiting a semaphore).
        services.AddSingleton<IImportQueueActor, ImportQueueActor>();
        services.AddSingleton<IWorkflowResumeService, WorkflowResumeService>();

        // Workflow handlers - register as the concrete type AND both interfaces (workflow router + import
        // job handler). The actor resolves IImportJobHandler LAZILY (via the factory below) to avoid a
        // cycle: the handler depends on the actor, the actor dispatches to the handler.
        services.AddSingleton<ModImportWorkflowHandler>();
        services.AddSingleton<IWorkflowHandler>(sp => sp.GetRequiredService<ModImportWorkflowHandler>());
        services.AddSingleton<IImportJobHandler>(sp => sp.GetRequiredService<ModImportWorkflowHandler>());

        // Remote imports are a SECOND job type on the SAME actor (resolves IRemoteImportService from the
        // profile container at runtime). Registered here with the other handlers.
        services.AddSingleton<RemoteImportWorkflowHandler>();
        services.AddSingleton<IWorkflowHandler>(sp => sp.GetRequiredService<RemoteImportWorkflowHandler>());
        services.AddSingleton<IImportJobHandler>(sp => sp.GetRequiredService<RemoteImportWorkflowHandler>());

        services.AddSingleton<Func<IEnumerable<IImportJobHandler>>>(sp => sp.GetServices<IImportJobHandler>);

        // Facade - will receive all registered IWorkflowHandler instances
        services.AddSingleton<IWorkflowFacade, WorkflowFacade>();

        return services;
    }
}
