using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;


namespace D3dxSkinManager.Plugins.ModifyListOrder;

/// <summary>
/// Plugin to customize display order for classes, objects, and mods.
/// Port of modify_list_order Python plugin.
///
/// Features:
/// - Customizes display order for classification list
/// - Customizes display order for object list
/// - Customizes display order for mod/choice list
/// - Drag-and-drop list reordering UI
/// - Supports multiple sort rules (name, grading, author, tags)
/// - Persistent sort configuration per user
///
/// TODO: Full implementation requires:
/// - Sort configuration storage (order.json per user)
/// - UI for drag-and-drop reordering
/// - Integration with treeview sorting logic
/// - Multiple sort rule support
/// - Default vs custom sort modes
/// </summary>
public class ModifyListOrderPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.modifylistorder";
    public string Name => "Modify List Order";
    public string Version => "1.0.0";
    public string Description => "Customizes display order for classes, objects, and mods";
    public string Author => "D3dxSkinManager";

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _context.Log(LogLevel.Info, $"[{Name}] Initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "GET_LIST_ORDER", "SET_LIST_ORDER", "RESET_LIST_ORDER" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "GET_LIST_ORDER" => await GetListOrderAsync(request),
                "SET_LIST_ORDER" => await SetListOrderAsync(request),
                "RESET_LIST_ORDER" => await ResetListOrderAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            _context?.Log(LogLevel.Error, $"[{Name}] Error handling message", ex);
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<MessageResponse> GetListOrderAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // TODO: Extract list type from request
            // Expected format: { "listType": "class" | "object" | "choice", "userName": "..." }

            _context.Log(LogLevel.Info, $"[{Name}] Getting list order");

            // TODO: Implement order retrieval
            // - Load order.json for user
            // - Return sort configuration for requested list type
            // - Include sort rule and custom order list

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                listType = "unknown",
                sortRule = "default",
                customOrder = new string[0],
                message = "Get list order not yet implemented",
                todo = "Full implementation required"
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error getting list order", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to get order: {ex.Message}");
        }
    }

    private async Task<MessageResponse> SetListOrderAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // TODO: Extract order configuration from request
            // Expected format: { "listType": "...", "sortRule": "...", "customOrder": [...], "userName": "..." }

            _context.Log(LogLevel.Info, $"[{Name}] Setting list order");

            // TODO: Implement order configuration
            // - Validate sort rule and custom order
            // - Save to order.json for user
            // - Update display order in UI
            // - Support different list types:
            //   - class: classification list order
            //   - object: object list order (with class reference support)
            //   - choice: mod/choice list order (with multiple sort rules)

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                message = "Set list order not yet implemented",
                todo = "Full implementation required"
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error setting list order", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to set order: {ex.Message}");
        }
    }

    private async Task<MessageResponse> ResetListOrderAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // TODO: Extract list type from request
            // Expected format: { "listType": "class" | "object" | "choice", "userName": "..." }

            _context.Log(LogLevel.Info, $"[{Name}] Resetting list order");

            // TODO: Implement order reset
            // - Reset to default sort rule
            // - Clear custom order configuration
            // - Update order.json
            // - Refresh UI display

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                message = "Reset list order not yet implemented",
                todo = "Full implementation required"
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error resetting list order", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to reset order: {ex.Message}");
        }
    }

    // TODO: Add helper methods:
    // - private async Task<OrderConfiguration> LoadOrderConfigurationAsync(string userName)
    // - private async Task SaveOrderConfigurationAsync(string userName, OrderConfiguration config)
    // - private List<string> GetDefaultClassOrder()
    // - private List<string> GetDefaultObjectOrder(string className)
    // - private List<string> GetDefaultChoiceOrder()
    // - private bool ValidateSortRule(string listType, string sortRule)

    // TODO: Add data classes:
    // - class OrderConfiguration { ... }
    // - class ClassOrderConfig { ... }
    // - class ObjectOrderConfig { ... }
    // - class ChoiceOrderConfig { ... }
}
