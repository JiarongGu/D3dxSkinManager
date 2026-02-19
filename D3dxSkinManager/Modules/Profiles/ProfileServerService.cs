using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Plugins.Services;

namespace D3dxSkinManager.Modules.Profiles
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
        private readonly IPluginEventBus _pluginEventBus;
        private readonly IPluginRegistry _pluginRegistry;

        public ProfileServerService(IPluginLoader pluginLoader, IPluginEventBus pluginEventBus, IPluginRegistry pluginRegistry) 
        {
            _pluginLoader = pluginLoader;
            _pluginEventBus = pluginEventBus;
            _pluginRegistry = pluginRegistry;
            //_imageServerService = imageServerService;
        }

        /// <summary>
        /// Loads and initializes plugins
        /// </summary>
        public async Task StartAsync()
        {
            // Load plugins from directory
            var loadedCount = await _pluginLoader.LoadPluginsAsync();

            // Initialize plugins
            await _pluginLoader.InitializePluginsAsync();

            Console.WriteLine($"[Init] Loaded and initialized {loadedCount} plugin(s)");

            //await _imageServerService.StartAsync();

            await _pluginEventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ApplicationStarted
            });
        }


        /// <summary>
        /// Shutdown all plugins
        /// </summary>
        public async Task StopAsync()
        {
            //await _imageServerService.StopAsync();
            await _pluginEventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ApplicationShutdown
            });

            var plugins = _pluginRegistry.GetAllPlugins().ToList();
            foreach (var plugin in plugins)
            {
                try
                {
                    await plugin.ShutdownAsync();
                    Console.WriteLine($"[Shutdown] Plugin shut down: {plugin.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Shutdown] Error shutting down plugin {plugin.Name}: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            StopAsync().Wait();
            //_imageServerService.Dispose();
        }
    }
}
