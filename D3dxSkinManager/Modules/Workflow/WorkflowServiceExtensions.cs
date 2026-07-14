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
        // WorkflowConcurrencyManager (per-item Task.Run self-awaiting a semaphore). It has TWO lanes with
        // independent caps: DOWNLOAD (network) from GlobalSettings.MaxParallelDownloads (default 4) and
        // IMPORT (CPU compress) from MaxParallelImports (default 5). Both read for the initial values, then
        // updated LIVE on GLOBAL_SETTINGS_CHANGED (the actor exposes MaxImport/MaxDownloadConcurrency).
        services.AddSingleton<IImportQueueActor>(sp =>
        {
            var settingSvc = sp.GetRequiredService<IGlobalSettingService>();
            var initial = settingSvc.GetSettingsAsync().GetAwaiter().GetResult();
            var actor = new ImportQueueActor(
                sp.GetRequiredService<Func<IEnumerable<IImportJobHandler>>>(),
                sp.GetRequiredService<ILogHelper>(),
                maxImportConcurrency: Clamp(initial.MaxParallelImports),
                maxDownloadConcurrency: Clamp(initial.MaxParallelDownloads));
            // Apply a settings change to the running queue without a restart/profile-switch.
            sp.GetService<IEventBus>()?.Subscribe(ModuleNames.SETTING, SettingEvents.GLOBAL_SETTINGS_CHANGED, async _ =>
            {
                var s = await settingSvc.GetSettingsAsync().ConfigureAwait(false);
                actor.MaxImportConcurrency = Clamp(s.MaxParallelImports);
                actor.MaxDownloadConcurrency = Clamp(s.MaxParallelDownloads);
            });
            return actor;
        });
        services.AddSingleton<IWorkflowResumeService, WorkflowResumeService>();

        // Workflow handlers - register as the concrete type AND both interfaces (workflow router + import
        // job handler). The actor resolves IImportJobHandler LAZILY (via the factory below) to avoid a
        // cycle: the handler depends on the actor, the actor dispatches to the handler.
        services.AddSingleton<ModImportWorkflowHandler>();
        services.AddSingleton<IWorkflowHandler>(sp => sp.GetRequiredService<ModImportWorkflowHandler>());
        services.AddSingleton<IImportJobHandler>(sp => sp.GetRequiredService<ModImportWorkflowHandler>());

        // Remote imports run as TWO job types (lanes) on the SAME actor: the DOWNLOAD leg
        // (RemoteDownloadHandler, download lane) then the IMPORT leg (RemoteImportWorkflowHandler, import
        // lane). The workflow ROW type is REMOTE_IMPORT (owned by RemoteImportWorkflowHandler as
        // IWorkflowHandler); RemoteDownloadHandler is an IImportJobHandler only (a lane dispatch key, not a
        // row type). Both resolve IRemoteImportService from the profile container at runtime.
        services.AddSingleton<RemoteImportWorkflowHandler>();
        services.AddSingleton<IWorkflowHandler>(sp => sp.GetRequiredService<RemoteImportWorkflowHandler>());
        services.AddSingleton<IImportJobHandler>(sp => sp.GetRequiredService<RemoteImportWorkflowHandler>());

        services.AddSingleton<RemoteDownloadHandler>();
        services.AddSingleton<IImportJobHandler>(sp => sp.GetRequiredService<RemoteDownloadHandler>());

        services.AddSingleton<Func<IEnumerable<IImportJobHandler>>>(sp => sp.GetServices<IImportJobHandler>);

        // Facade - will receive all registered IWorkflowHandler instances
        services.AddSingleton<IWorkflowFacade, WorkflowFacade>();

        return services;
    }

    /// <summary>Keep a parallel-concurrency setting in a sane range (1–8): compression is CPU-bound and too
    /// many parallel downloads hammer a host, so a huge number just thrashes; 0/negative would stall a lane.</summary>
    private static int Clamp(int value) => Math.Clamp(value, 1, 8);
}
