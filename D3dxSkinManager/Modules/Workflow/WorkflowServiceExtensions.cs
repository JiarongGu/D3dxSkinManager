using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Setting;
using D3dxSkinManager.Modules.Setting.Services;
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
        // WorkflowConcurrencyManager (per-item Task.Run self-awaiting a semaphore). Its max concurrency
        // comes from the GlobalSettings.MaxParallelImports user setting (default 5): read for the initial
        // value, then updated LIVE on GLOBAL_SETTINGS_CHANGED (the actor exposes MaxConcurrency → SetMax).
        services.AddSingleton<IImportQueueActor>(sp =>
        {
            var settingSvc = sp.GetRequiredService<IGlobalSettingService>();
            var actor = new ImportQueueActor(
                sp.GetRequiredService<Func<IEnumerable<IImportJobHandler>>>(),
                sp.GetRequiredService<ILogHelper>(),
                ClampImports(settingSvc.GetSettingsAsync().GetAwaiter().GetResult().MaxParallelImports));
            // Apply a settings change to the running queue without a restart/profile-switch.
            sp.GetService<IEventBus>()?.Subscribe(ModuleNames.SETTING, SettingEvents.GLOBAL_SETTINGS_CHANGED, async _ =>
                actor.MaxConcurrency = ClampImports((await settingSvc.GetSettingsAsync().ConfigureAwait(false)).MaxParallelImports));
            return actor;
        });
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

    /// <summary>Keep the parallel-import setting in a sane range (1–8): compression is CPU-bound, so a
    /// huge number just thrashes; 0/negative would stall the queue.</summary>
    private static int ClampImports(int value) => Math.Clamp(value, 1, 8);
}
