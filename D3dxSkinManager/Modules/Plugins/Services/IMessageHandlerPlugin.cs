using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Plugins.Services;

/// <summary>
/// Plugin interface for handling custom IPC messages from the frontend.
/// Allows plugins to register custom message types and handlers.
/// </summary>
public interface IMessageHandlerPlugin : IPlugin
{
    /// <summary>
    /// Get the list of message types this plugin can handle.
    /// These are custom message types beyond the standard ones.
    /// Example: "MY_PLUGIN_ACTION", "CUSTOM_QUERY"
    /// </summary>
    IEnumerable<string> GetHandledMessageTypes();

    /// <summary>
    /// Handle a custom message from the frontend.
    /// </summary>
    /// <param name="request">Message request from frontend</param>
    /// <returns>Response to send back to frontend</returns>
    Task<MessageResponse> HandleMessageAsync(MessageRequest request);
}
