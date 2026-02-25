using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Plugin.Services;

namespace D3dxSkinManager.Modules.Context.Services
{
    public interface IProfileServerService: IDisposable
    {
        Task StartAsync();

        Task StopAsync();
    }

    public class ProfileServerService : IProfileServerService, IDisposable
    {
        //private readonly IImageServerService _imageServerService;
        private readonly IPluginLoader _pluginLoader;
        private readonly IEventBus _pluginEventBus;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly ILogHelper _logger;

        public ProfileServerService(IPluginLoader pluginLoader, IEventBus pluginEventBus, IPluginRegistry pluginRegistry, ILogHelper logger)
        {
            _pluginLoader = pluginLoader;
            _pluginEventBus = pluginEventBus;
            _pluginRegistry = pluginRegistry;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            //_imageServerService = imageServerService;
        }

        /// <summary>
        /// Loads and initializes plugins
        /// </summary>
        public async Task StartAsync()
        {
            // Load plugins from directory
            var loadedCount = await _pluginLoader.LoadPluginsAsync().ConfigureAwait(false);

            // Initialize plugins
            await _pluginLoader.InitializePluginsAsync().ConfigureAwait(false);

            _logger.Info($"Loaded and initialized {loadedCount} plugin(s)", "Init");

            //await _imageServerService.StartAsync().ConfigureAwait(false);

            await _pluginEventBus.EmitAsync(ModuleNames.CORE, CoreEvents.APPLICATION_STARTED).ConfigureAwait(false);
        }


        /// <summary>
        /// Shutdown all plugins
        /// </summary>
        public async Task StopAsync()
        {
            //await _imageServerService.StopAsync().ConfigureAwait(false);
            await _pluginEventBus.EmitAsync(ModuleNames.CORE, CoreEvents.APPLICATION_SHUTDOWN).ConfigureAwait(false);

            var plugins = _pluginRegistry.GetAllPlugins().ToList();
            foreach (var plugin in plugins)
            {
                try
                {
                    await plugin.ShutdownAsync().ConfigureAwait(false);
                    _logger.Info($"Plugin shut down: {plugin.Name}", "Shutdown");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error shutting down plugin {plugin.Name}: {ex.Message}", "Shutdown", ex);
                }
            }
        }

        public void Dispose()
        {
            // Use Task.Run to avoid potential deadlock in Dispose
            Task.Run(async () => await StopAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
            //_imageServerService.Dispose();
        }
    }
}
