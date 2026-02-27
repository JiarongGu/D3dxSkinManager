using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.WinForms;

namespace D3dxSkinManager.Infrastructure.WebView
{
    public sealed class WebViewSession : IDisposable
    {
        public string SessionId { get; }
        public WebView2 WebView { get; }
        public IpcCommunicationHandler Ipc { get; }
        public DropZoneManager DropZone { get; }
        public EventBusIpcBridge EventBridge { get; }
        public MessageDispatcher Dispatcher { get; }
        public WebViewInitializer Initializer { get; }

        private readonly ILogHelper _logger;

        public WebViewSession(
            string sessionId,
            WebView2 webView,
            ILogHelper logger,
            IServiceProvider serviceProvider,
            ICustomSchemeHandler schemeHandler,
            Form mainForm,
            ProfileServiceRouter profileRouter,
            Action<MessageDispatcher> configurePipeline)
        {
            SessionId = sessionId;
            WebView = webView ?? throw new ArgumentNullException(nameof(webView));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Per-session initializer
            Initializer = new WebViewInitializer(WebView, schemeHandler);

            // Per-session IPC
            Ipc = new IpcCommunicationHandler(WebView, _logger);

            // Per-session DropZone
            DropZone = new DropZoneManager(WebView, mainForm, _logger, Ipc);

            // Per-session event bridge (push backend events -> this webview)
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            EventBridge = new EventBusIpcBridge(eventBus, Ipc, _logger);

            // Per-session dispatcher (requests from this webview -> shared services)
            Dispatcher = new MessageDispatcher(Ipc, _logger);
            configurePipeline(Dispatcher); // <-- you reuse the same pipeline for all sessions
        }

        public async Task StartAsync()
        {
            _logger.Info($"[{SessionId}] Starting WebView session...", "Host");

            await Initializer.InitializeAsync();

            // Important ordering note:
            // - Hook IPC before navigation if you want early messages
            // - Or navigate first if your app only talks after ready
            Ipc.Initialize();

            // Dispatcher + bridge can initialize before/after navigation; typically before is fine
            Dispatcher.Initialize();
            EventBridge.Initialize();

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
