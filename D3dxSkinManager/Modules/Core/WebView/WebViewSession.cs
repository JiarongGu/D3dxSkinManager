using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.WebView
{
    public sealed class WebViewSession : IDisposable
    {
        public string SessionId { get; }
        public WebView2 WebView { get; }
        public IpcHandler Ipc { get; }
        public DropZoneManager DropZone { get; }
        public EventBusIpcBridge EventBridge { get; }
        public WebViewInitializer Initializer { get; }
        public SplashScreenPanel? SplashScreen { get; private set; }

        private readonly ILogHelper _logger;
        private readonly MessageDispatcher _globalDispatcher;
        private readonly MessageDispatcher _sessionDispatcher;
        private readonly Form _form;

        public WebViewSession(
            string sessionId,
            WebView2 webView,
            ILogHelper logger,
            IServiceProvider serviceProvider,
            ICustomSchemeHandler schemeHandler,
            Form form,
            SplashScreenPanel? splashScreen = null,
            bool ownEnvironment = false)
        {
            SessionId = sessionId;
            WebView = webView ?? throw new ArgumentNullException(nameof(webView));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _form = form ?? throw new ArgumentNullException(nameof(form));
            SplashScreen = splashScreen;

            // Get embedded resource provider from DI
            var resourceProvider = serviceProvider.GetRequiredService<IEmbeddedResourceProvider>();

            // The install ROOT for the WebView2 user-data folder — resolved from IAppEnvironment (the
            // --app-root value), never AppDomain.BaseDirectory (= {install}/libs in prod). See launcher-topology.md.
            var appEnvironment = serviceProvider.GetRequiredService<IAppEnvironment>();

            // Per-session initializer. Secondary windows (ownEnvironment=true) run on their own STA
            // thread and must create their own CoreWebView2Environment there.
            Initializer = new WebViewInitializer(WebView, schemeHandler, resourceProvider, appEnvironment.BaseDirectory, ownEnvironment);

            // Per-session IPC
            Ipc = new IpcHandler(WebView, _logger);

            // Per-session DropZone
            DropZone = new DropZoneManager(WebView, form, _logger, Ipc);

            // Per-session event bridge (push backend events -> this webview)
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            EventBridge = new EventBusIpcBridge(eventBus, Ipc, _logger);

            // Get global singleton dispatcher
            _globalDispatcher = serviceProvider.GetRequiredService<MessageDispatcher>();

            // Create session-level dispatcher for APP and DROP_ZONE routes
            _sessionDispatcher = new MessageDispatcher(_logger);
            RegisterSessionRoutes();

            // Wire up IPC to dispatcher pipeline
            Ipc.MessageReceived += OnIpcMessageReceived;
        }

        /// <summary>
        /// Register session-specific routes (APP and DROP_ZONE) in the session dispatcher
        /// </summary>
        private void RegisterSessionRoutes()
        {
            _logger.Info($"[{SessionId}] Registering session-specific routes", "WebViewSession");

            // APP routes
            _sessionDispatcher.MapModule("APP", routes =>
            {
                routes.Route("WEBVIEW_READY", message =>
                {
                    var webViewId = message.Payload?.GetProperty("webViewId").GetString() ?? "unknown";
                    _logger.Info($"[{SessionId}] WebView ready (ID: {webViewId})", "WebViewSession");

                    DropZone?.ClearAll();

                    if (SplashScreen != null)
                    {
                        _logger.Info($"[{SessionId}] Hiding splash screen", "WebViewSession");
                        HideSplashScreen();
                    }

                    return new { success = true, webViewId };
                });
            });

            // DROP_ZONE routes
            _sessionDispatcher.MapModule("DROP_ZONE", routes =>
            {
                routes.Route("REGISTER", message =>
                {
                    var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                    var x = message.Payload?.GetProperty("x").GetInt32() ?? 0;
                    var y = message.Payload?.GetProperty("y").GetInt32() ?? 0;
                    var width = message.Payload?.GetProperty("width").GetInt32() ?? 0;
                    var height = message.Payload?.GetProperty("height").GetInt32() ?? 0;
                    DropZone.RegisterZone(zoneId, x, y, width, height);
                    return new { success = true, zoneId };
                });

                routes.Route("UPDATE", message =>
                {
                    var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                    var x = message.Payload?.GetProperty("x").GetInt32() ?? 0;
                    var y = message.Payload?.GetProperty("y").GetInt32() ?? 0;
                    var width = message.Payload?.GetProperty("width").GetInt32() ?? 0;
                    var height = message.Payload?.GetProperty("height").GetInt32() ?? 0;
                    DropZone.UpdateZoneBounds(zoneId, x, y, width, height);
                    return new { success = true };
                });

                routes.Route("UNREGISTER", message =>
                {
                    var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                    DropZone.UnregisterZone(zoneId);
                    return new { success = true };
                });

                routes.Route("SHOW", message =>
                {
                    var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                    DropZone.ShowOverlay(zoneId);
                    return new { success = true };
                });
            });
        }

        /// <summary>
        /// Handle IPC messages from this session's WebView
        /// Routes to session dispatcher first (APP, DROP_ZONE), then global dispatcher
        /// </summary>
        private async void OnIpcMessageReceived(object? sender, IpcMessageReceivedEventArgs e)
        {
            // Async on the UI thread: each `await` yields the message pump so concurrent IPC calls still
            // interleave (the frontend gets concurrency), WITHOUT consuming a thread-pool thread per call.
            // A previous version offloaded every message to Task.Run, but under heavy backend load (e.g.
            // a full mod analysis extracting thousands of archives + blocking pool threads) that starved
            // the thread pool and made IPC — even a quick XXMI detect — time out, freezing the app. Heavy
            // work is already offloaded by the backend's own queues (FileOperationPlanner, ModOperationQueue,
            // fire-and-forget facades), which is the correct place to bound concurrency.
            try
            {
                _logger.Verbose($"[{SessionId}] Received IPC message: {e.Message.Module}/{e.Message.Type}", "WebViewSession");

                var response = await _sessionDispatcher.ProcessMessageAsync(e.Message)
                               ?? await _globalDispatcher.ProcessMessageAsync(e.Message);

                e.SendResponse(response ?? IpcResponse.CreateError(e.Message.Id,
                    $"No handler registered for {e.Message.Module}/{e.Message.Type}"));
            }
            catch (Exception ex)
            {
                _logger.Error($"[{SessionId}] Error processing IPC message: {ex.Message}", "WebViewSession", ex);
                e.SendResponse(IpcResponse.CreateError(e.Message.Id, $"Session error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Hide and dispose splash screen for this session
        /// </summary>
        private void HideSplashScreen()
        {
            if (SplashScreen != null && _form != null)
            {
                _logger.Info($"[{SessionId}] Hiding splash screen panel", "WebViewSession");

                if (_form.InvokeRequired)
                {
                    _form.Invoke(new Action(() =>
                    {
                        _form.Controls.Remove(SplashScreen);
                        SplashScreen.Dispose();
                        SplashScreen = null;
                    }));
                }
                else
                {
                    _form.Controls.Remove(SplashScreen);
                    SplashScreen.Dispose();
                    SplashScreen = null;
                }
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

                // Dispose splash screen if still present
                if (SplashScreen != null)
                {
                    HideSplashScreen();
                }

                // Dispose the IPC handler so its 50ms batch timer stops (it otherwise fired for the life
                // of the process, posting to a torn-down WebView).
                Ipc.Dispose();

                // Dispose the DropZone manager — it IS IDisposable: unhooks the parent form's
                // Deactivate/Activated handlers (which would otherwise linger and fire on disposed
                // overlays if the form outlives this session) and destroys any remaining overlays.
                DropZone?.Dispose();

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
