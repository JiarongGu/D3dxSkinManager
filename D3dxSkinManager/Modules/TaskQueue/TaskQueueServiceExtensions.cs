using D3dxSkinManager.Composition;
using D3dxSkinManager.Modules.TaskQueue.Processors;
using D3dxSkinManager.Modules.TaskQueue.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D3dxSkinManager.Modules.TaskQueue;

/// <summary>
/// Service registration extensions for TaskQueue module
/// </summary>
public static class TaskQueueServiceExtensions
{
    /// <summary>
    /// Register TaskQueue module services (profile-scoped)
    /// </summary>
    public static IServiceCollection AddTaskQueueServices(this IServiceCollection services)
    {
        Console.WriteLine("[TaskQueueFacade] Registering TaskQueue services (profile-scoped)...");

        // Core services
        services.TryAddSingleton<ITaskQueueService, TaskQueueService>();

        // Task processors
        services.TryAddSingleton<ModImportTaskProcessor>();
        services.TryAddSingleton<CompressFolderTaskProcessor>();
        services.TryAddSingleton<ImportFromTempTaskProcessor>();

        // Facade
        services.TryAddSingleton<ITaskQueueFacade, TaskQueueFacade>();

        Console.WriteLine("[TaskQueueFacade] TaskQueue services registered");
        return services;
    }

    /// <summary>
    /// Register TaskQueueFacade message handlers with the MessageDispatcher
    /// </summary>
    public static MessageDispatcher UseTaskQueueFacade(this MessageDispatcher dispatcher, ServiceProvider serviceProvider)
    {
        var facade = serviceProvider.GetService<TaskQueueFacade>();
        if (facade == null)
        {
            Console.WriteLine("[TaskQueueFacade] Warning: TaskQueueFacade not registered in service container");
            return dispatcher;
        }

        Console.WriteLine("[TaskQueueFacade] Registering TASK_QUEUE module handlers");

        // Register the module handler
        dispatcher.UseModule("TASK_QUEUE", facade.HandleMessageAsync);

        return dispatcher;
    }
}
