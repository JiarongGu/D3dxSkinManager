using D3dxSkinManager.Composition;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core;

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
    public async Task<IpcResponse> HandleMessageAsync(IpcRequest request)
    {
        try
        {
            _logger.Debug($"Handling message: {request.Type}", ModuleName);

            var responseData = await RouteMessageAsync(request).ConfigureAwait(false);

            return IpcResponse.CreateSuccess(request.Id, responseData);
        }
        catch (ModException modEx)
        {
            // Handle ModException specially to include error code and data
            _logger.Error($"Mod operation error '{request.Type}': [{modEx.ErrorCode}] {modEx.Message}", ModuleName, modEx);

            return IpcResponse.CreateError(request.Id, modEx.Message, new
            {
                errorCode = modEx.ErrorCode,
                data = modEx.Data
            });
        }
        catch (Exception ex)
        {
            // Handle unknown errors with UNKNOWN_ERROR code
            _logger.Error($"Unknown error handling message '{request.Type}': {ex.Message}", ModuleName, ex);

            return Models.IpcResponse.CreateError(request.Id, ex.Message, new
            {
                errorCode = ErrorCodes.UNKNOWN_ERROR,
                data = new { exceptionType = ex.GetType().Name }
            });
        }
    }

    /// <summary>
    /// Routes the message to the appropriate handler method.
    /// Derived classes must implement this to handle their specific message types.
    /// </summary>
    /// <param name="request">The incoming message request</param>
    /// <returns>The response data, or null if no data to return</returns>
    protected abstract Task<object?> RouteMessageAsync(Models.IpcRequest request);
}
