using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Core.Facades;

/// <summary>
/// Abstract base class for all module facades.
/// Provides common message handling pattern with standardized error handling.
/// </summary>
public abstract class BaseFacade : IModuleFacade
{
    protected readonly ILogHelper _logger;

    protected BaseFacade(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// The name of the module for logging purposes.
    /// </summary>
    protected abstract string ModuleName { get; }

    /// <summary>
    /// Handles incoming IPC messages with standardized error handling.
    /// </summary>
    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            _logger.Debug($"Handling message: {request.Type}", ModuleName);

            var responseData = await RouteMessageAsync(request);

            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling message '{request.Type}': {ex.Message}", ModuleName, ex);
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    /// <summary>
    /// Routes the message to the appropriate handler method.
    /// Derived classes must implement this to handle their specific message types.
    /// </summary>
    /// <param name="request">The incoming message request</param>
    /// <returns>The response data, or null if no data to return</returns>
    protected abstract Task<object?> RouteMessageAsync(MessageRequest request);
}
