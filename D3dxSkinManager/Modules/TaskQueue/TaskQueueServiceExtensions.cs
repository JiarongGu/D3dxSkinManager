using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Infrastructure;
using D3dxSkinManager.Infrastructure.WebView;
using D3dxSkinManager.Modules.TaskQueue.Models;
using D3dxSkinManager.Modules.TaskQueue.Processors;
using D3dxSkinManager.Modules.TaskQueue.Repositories;
using D3dxSkinManager.Modules.TaskQueue.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D3dxSkinManager.Modules.TaskQueue;

/// <summary>
/// Extension methods for configuring TaskQueue services
/// </summary>
public static class TaskQueueServiceExtensions
{
    /// <summary>
    /// Register TaskQueue module services (profile-scoped)
    /// </summary>
    public static IServiceCollection AddTaskQueueServices(this IServiceCollection services)
    {
        Console.WriteLine("[TaskQueue] Registering TaskQueue services (profile-scoped)...");

        // Repositories
        services.TryAddSingleton<ITaskChainRepository, TaskChainRepository>();
        services.TryAddSingleton<ITaskInfoRepository, TaskInfoRepository>();

        // Core services
        services.TryAddSingleton<ITaskQueueService, TaskQueueService>();
        services.TryAddSingleton<IRoutingConditionEvaluator, RoutingConditionEvaluator>();

        // Task processors
        services.TryAddSingleton<ModImportTaskProcessor>();
        services.TryAddSingleton<CompressFolderTaskProcessor>();
        services.TryAddSingleton<ImportFromTempTaskProcessor>();

        // Facade
        services.TryAddSingleton<ITaskQueueFacade, TaskQueueFacade>();

        Console.WriteLine("[TaskQueue] TaskQueue services registered");
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