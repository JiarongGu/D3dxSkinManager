using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Infrastructure.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.WinForms;

namespace D3dxSkinManager.Infrastructure.WebView
{
    public sealed class WebViewSession : IDisposable
    {
        public string SessionId { get; }
        public WebView2 WebView { get; }
        public IpcHandler Ipc { get; }
        public DropZoneManager DropZone { get; }
        public EventBusIpcBridge EventBridge { get; }
        public WebViewInitializer Initializer { get; }

        private readonly ILogHelper _logger;
        private readonly MessageDispatcher _dispatcher;

        public WebViewSession(
            string sessionId,
            WebView2 webView,
            ILogHelper logger,
            IServiceProvider serviceProvider,
            ICustomSchemeHandler schemeHandler,
            Form mainForm)
        {
            SessionId = sessionId;
            WebView = webView ?? throw new ArgumentNullException(nameof(webView));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Get embedded resource provider from DI
            var resourceProvider = serviceProvider.GetRequiredService<IEmbeddedResourceProvider>();

            // Per-session initializer
            Initializer = new WebViewInitializer(WebView, schemeHandler, resourceProvider);

            // Per-session IPC
            Ipc = new IpcHandler(WebView, _logger);

            // Per-session DropZone
            DropZone = new DropZoneManager(WebView, mainForm, _logger, Ipc);

            // Per-session event bridge (push backend events -> this webview)
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            EventBridge = new EventBusIpcBridge(eventBus, Ipc, _logger);

            // Get global singleton dispatcher
            _dispatcher = serviceProvider.GetRequiredService<MessageDispatcher>();

            // Wire up IPC to global dispatcher
            Ipc.MessageReceived += OnIpcMessageReceived;
        }

        /// <summary>
        /// Handle IPC messages from this session's WebView and route to global dispatcher
        /// </summary>
        private async void OnIpcMessageReceived(object? sender, IpcMessageReceivedEventArgs e)
        {
            try
            {
                _logger.Verbose($"[{SessionId}] Received IPC message: {e.Message.Module}/{e.Message.Type}", "WebViewSession");

                // Process through global dispatcher
                var response = await _dispatcher.ProcessMessageAsync(e.Message);

                // Send response back to this session's WebView
                if (response != null)
                {
                    e.SendResponse(response);
                }
                else
                {
                    // No handler matched
                    var errorResponse = IpcResponse.CreateError(e.Message.Id,
                        $"No handler registered for {e.Message.Module}/{e.Message.Type}");
                    e.SendResponse(errorResponse);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[{SessionId}] Error processing IPC message: {ex.Message}", "WebViewSession", ex);
                var errorResponse = IpcResponse.CreateError(e.Message.Id, $"Session error: {ex.Message}");
                e.SendResponse(errorResponse);
            }
        }

        public async Task StartAsync()
        {
            _logger.Info($"[{SessionId}] Starting WebView session...", "Host");

            await Initializer.InitAsync();

            // Important ordering note:
            // - Hook IPC before navigation if you want early messages
            // - Or navigate first if your app only talks after ready
            Ipc.Init();

            // Initialize event bridge
            EventBridge.Init();

            Initializer.NavigateToApp();

            _logger.Info($"[{SessionId}] WebView session started", "Host");
        }

        public void Dispose()
        {
            try
            {
                _logger.Info($"[{SessionId}] Disposing WebView session...", "Host");

                // Dispose event bridge (unsubscribe from event bus)
                EventBridge?.Dispose();

                // Note: DropZone and Ipc don't implement IDisposable (lightweight wrappers)
                // Note: WebView2 is owned by the form, so we don't dispose it here
                // Note: Dispatcher doesn't have Dispose

                _logger.Info($"[{SessionId}] WebView session disposed", "Host");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{SessionId}] Error disposing session: {ex.Message}", "Host", ex);
            }
        }
    }
}
