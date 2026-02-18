using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;


namespace D3dxSkinManager.Plugins.HandleUserEnv;

/// <summary>
/// Plugin for user environment management (create/edit/delete users).
/// Port of handle_user_env Python plugin.
///
/// Features:
/// - Create new user environments
/// - Edit user profiles and descriptions
/// - Delete user environments
/// - List all users
/// - Manage user directories and configuration
///
/// TODO: Full implementation requires:
/// - User directory creation/deletion
/// - Description file management (description.txt)
/// - User configuration handling
/// - UI for user management
/// - Integration with login system
/// </summary>
public class HandleUserEnvPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.handleuserenv";
    public string Name => "Handle User Environment";
    public string Version => "1.0.0";
    public string Description => "User environment management (create/edit/delete users)";
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
        return new[] { "CREATE_USER", "EDIT_USER", "DELETE_USER", "LIST_USERS" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "CREATE_USER" => await CreateUserAsync(request),
                "EDIT_USER" => await EditUserAsync(request),
                "DELETE_USER" => await DeleteUserAsync(request),
                "LIST_USERS" => await ListUsersAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            _context?.Log(LogLevel.Error, $"[{Name}] Error handling message", ex);
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<MessageResponse> CreateUserAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // TODO: Extract user data from request
            // Expected format: { "userName": "...", "description": "..." }

            _context.Log(LogLevel.Info, $"[{Name}] Creating user");

            // TODO: Implement user creation
            // - Create user directory in data/home/{userName}
            // - Create modsIndex subdirectory
            // - Create description.txt file
            // - Initialize user configuration
            // - Set up user-specific directories

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                message = "User creation not yet implemented",
                todo = "Full implementation required"
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error creating user", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to create user: {ex.Message}");
        }
    }

    private async Task<MessageResponse> EditUserAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // TODO: Extract user data from request
            // Expected format: { "userName": "...", "description": "..." }

            _context.Log(LogLevel.Info, $"[{Name}] Editing user");

            // TODO: Implement user editing
            // - Update description.txt file
            // - Update user configuration
            // - Validate user exists

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                message = "User editing not yet implemented",
                todo = "Full implementation required"
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error editing user", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to edit user: {ex.Message}");
        }
    }

    private async Task<MessageResponse> DeleteUserAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // TODO: Extract user name from request
            // Expected format: { "userName": "..." }

            _context.Log(LogLevel.Info, $"[{Name}] Deleting user");

            // TODO: Implement user deletion
            // - Confirm deletion (safety check)
            // - Delete user directory recursively
            // - Clean up user-specific data
            // - Update user list

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                message = "User deletion not yet implemented",
                todo = "Full implementation required"
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error deleting user", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to delete user: {ex.Message}");
        }
    }

    private async Task<MessageResponse> ListUsersAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var homePath = Path.Combine(dataPath, "home");

            var users = new List<object>();

            if (Directory.Exists(homePath))
            {
                foreach (var userDir in Directory.GetDirectories(homePath))
                {
                    var userName = Path.GetFileName(userDir);
                    var descriptionPath = Path.Combine(userDir, "description.txt");
                    var description = "";

                    if (File.Exists(descriptionPath))
                    {
                        try
                        {
                            description = await File.ReadAllTextAsync(descriptionPath);
                        }
                        catch
                        {
                            // Ignore read errors
                        }
                    }

                    users.Add(new
                    {
                        userName,
                        description,
                        path = userDir
                    });
                }
            }

            _context.Log(LogLevel.Info, $"[{Name}] Listed {users.Count} users");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                users = users.ToArray()
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error listing users", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to list users: {ex.Message}");
        }
    }

    // TODO: Add helper methods:
    // - private bool ValidateUserName(string userName)
    // - private async Task CreateUserDirectoryStructureAsync(string userName)
    // - private async Task WriteDescriptionFileAsync(string userName, string description)
    // - private async Task<bool> UserExistsAsync(string userName)
}
