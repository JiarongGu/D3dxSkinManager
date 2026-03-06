using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Timers;

namespace D3dxSkinManager.Modules.Core.WebView;

/// <summary>
/// Event args for IPC message received events
/// </summary>
public class IpcMessageReceivedEventArgs : EventArgs
{
    public IpcRequest Message { get; }

    public Action<IpcResponse> SendResponse { get; }

    public IpcMessageReceivedEventArgs(IpcRequest message, Action<IpcResponse> sendResponse)
    {
        Message = message;
        SendResponse = sendResponse;
    }
}

/// <summary>
/// Handles IPC (Inter-Process Communication) between React frontend and .NET backend via WebView2
/// </summary>
public class IpcHandler : IIpcHandler
{
    private readonly WebView2 _webView;
    private readonly ILogHelper _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Notification batching
    private readonly ConcurrentQueue<(string module, string type, object? payload)> _pendingNotifications = new();
    private readonly global::System.Timers.Timer _batchTimer;
    private readonly object _batchLock = new();
    private const int BatchIntervalMs = 50;

    /// <summary>
    /// Event fired when a message is received from the frontend
    /// </summary>
    public event EventHandler<IpcMessageReceivedEventArgs>? MessageReceived;

    public IpcHandler(WebView2 webView, ILogHelper logger)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize batch timer
        _batchTimer = new global::System.Timers.Timer(BatchIntervalMs);
        _batchTimer.Elapsed += (sender, e) => FlushNotificationBatch();
        _batchTimer.AutoReset = true;
        _batchTimer.Start();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    /// <summary>
    /// Initialize IPC message handlers
    /// </summary>
    public void Init()
    {
        _logger.Info("Initializing communication handler...", "IPC");

        // Subscribe to messages from React
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        _logger.Info("Communication handler initialized", "IPC");
    }

    /// <summary>
    /// Handle messages received from React
    /// </summary>
    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            _logger.Verbose($"Received message: {json}", "IPC");

            // Parse the message
            var message = JsonSerializer.Deserialize<IpcRequest>(json, _jsonOptions);
            if (message == null)
            {
                _logger.Warn("Invalid message format", "IPC");
                return;
            }

            // Check if we have any handlers registered
            if (MessageReceived != null)
            {
                // Fire the event with the message and response callback
                var args = new IpcMessageReceivedEventArgs(message, SendResponse);
                MessageReceived.Invoke(this, args);
            }
            else
            {
                // No handlers registered - use default processing
                var response = await ProcessMessage(message);
                SendResponse(response);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling message: {ex.Message}", "IPC", ex);
        }
    }

    /// <summary>
    /// Default message processing (used when no handlers are registered)
    /// This is mainly for testing and development
    /// </summary>
    private async Task<IpcResponse> ProcessMessage(IpcRequest message)
    {
        _logger.Debug($"Processing message - Module: {message.Module}, Type: {message.Type}", "IPC");

        try
        {
            // Route message based on module
            return message.Module switch
            {
                _ => IpcResponse.CreateError(message.Id, $"Unknown module: {message.Module}")
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Error processing message: {ex.Message}", "IPC", ex);
            return IpcResponse.CreateError(message.Id, ex.Message);
        }
    }


    /// <summary>
    /// Send response back to React
    /// </summary>
    private void SendResponse(IpcResponse response)
    {
        try
        {
            // Wrap response with category for frontend routing
            var wrappedResponse = new
            {
                category = "IPC",
                id = response.Id,
                success = response.Success,
                data = response.Data,
                error = response.Error
            };

            var json = JsonSerializer.Serialize(wrappedResponse, _jsonOptions);

            // Marshal to UI thread since WebView2 requires UI thread access (non-blocking)
            if (_webView.InvokeRequired)
            {
                _webView.BeginInvoke(() =>
                {
                    _webView.CoreWebView2.PostWebMessageAsString(json);
                });
            }
            else
            {
                _webView.CoreWebView2.PostWebMessageAsString(json);
            }

            _logger.Verbose($"Sent IPC response: {response.Id}", "IPC");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error sending response: {ex.Message}", "IPC", ex);
        }
    }

    /// <summary>
    /// Send a push notification to React (queued for batching)
    /// Events are batched and sent every 50ms to reduce IPC overhead
    /// </summary>
    public void SendNotification(string module, string type, object? payload = null)
    {
        // Queue notification for batching
        _pendingNotifications.Enqueue((module, type, payload));
    }

    /// <summary>
    /// Flush pending notifications as a single batched IPC message
    /// Called every 50ms by the timer
    /// </summary>
    private void FlushNotificationBatch()
    {
        lock (_batchLock)
        {
            if (_pendingNotifications.IsEmpty)
                return;

            var batch = new List<(string module, string type, object? payload)>();
            while (_pendingNotifications.TryDequeue(out var notification))
            {
                batch.Add(notification);
            }

            if (batch.Count == 0)
                return;

            try
            {
                // Send all events (no filtering)
                var events = batch
                    .Select(e => new
                    {
                        module = e.module,
                        type = e.type,
                        payload = e.payload
                    })
                    .ToList();

                _logger.Verbose($"Flushing batch of {events.Count} events", "IPC");

                // Send as batched notification
                var batchId = Guid.NewGuid().ToString();
                var message = new
                {
                    category = "NOTIFICATION",
                    id = batchId,
                    module = "EVENT_BUS",
                    type = "BATCH",
                    payload = events,
                    timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(message, _jsonOptions);

                // Marshal to UI thread - all WebView2 access must be on UI thread
                if (_webView.InvokeRequired)
                {
                    _webView.BeginInvoke(() =>
                    {
                        try
                        {
                            if (_webView.CoreWebView2 != null)
                            {
                                _webView.CoreWebView2.PostWebMessageAsString(json);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error posting message from UI thread: {ex.Message}", "IPC");
                        }
                    });
                }
                else
                {
                    if (_webView.CoreWebView2 != null)
                    {
                        _webView.CoreWebView2.PostWebMessageAsString(json);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error flushing notification batch: {ex.Message}", "IPC", ex);
            }
        }
    }
}
