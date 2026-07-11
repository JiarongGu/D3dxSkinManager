using D3dxSkinManager.Modules.Tool.ScreenCapture.Models;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Services;

/// <summary>
/// Business logic for screen-capture profiles (CRUD + lifecycle events). Owns the DTO build,
/// the insert-vs-update decision, and event emission that previously lived in <c>ToolFacade</c>;
/// the facade now only parses the IPC payload and delegates here.
/// </summary>
public interface IScreenCaptureProfileService
{
    /// <summary>All saved capture profiles.</summary>
    Task<List<ScreenCaptureProfile>> GetAllAsync();

    /// <summary>Create (no <c>Id</c>) or update (<c>Id</c> set) a capture profile; emits the matching
    /// lifecycle event; returns the profile id.</summary>
    Task<string> SaveAsync(SaveScreenCaptureProfileRequest request);

    /// <summary>Delete a capture profile by id; emits <c>CAPTURE_PROFILE_DELETED</c>.</summary>
    Task DeleteAsync(string id);
}

/// <summary>Implementation of <see cref="IScreenCaptureProfileService"/>.</summary>
public class ScreenCaptureProfileService : IScreenCaptureProfileService
{
    private readonly IScreenCaptureProfileRepository _repository;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;

    public ScreenCaptureProfileService(
        IScreenCaptureProfileRepository repository,
        IProfileEventBus eventBus,
        ILogHelper logger)
    {
        _repository = repository;
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task<List<ScreenCaptureProfile>> GetAllAsync() => _repository.GetAllAsync();

    public async Task<string> SaveAsync(SaveScreenCaptureProfileRequest request)
    {
        var profile = new ScreenCaptureProfile
        {
            Id = request.Id ?? Guid.NewGuid().ToString(),
            Name = request.Name,
            X = request.X,
            Y = request.Y,
            Width = request.Width,
            Height = request.Height,
        };

        if (string.IsNullOrEmpty(request.Id))
        {
            var id = await _repository.InsertAsync(profile).ConfigureAwait(false);
            _logger.Info($"Created screen-capture profile '{profile.Name}' ({id})", "ScreenCaptureProfileService");
            await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.CAPTURE_PROFILE_CREATED,
                new { id, name = profile.Name }).ConfigureAwait(false);
            return id;
        }

        await _repository.UpdateAsync(profile).ConfigureAwait(false);
        _logger.Info($"Updated screen-capture profile '{profile.Name}' ({profile.Id})", "ScreenCaptureProfileService");
        await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.CAPTURE_PROFILE_UPDATED,
            new { id = profile.Id, name = profile.Name }).ConfigureAwait(false);
        return profile.Id;
    }

    public async Task DeleteAsync(string id)
    {
        await _repository.DeleteAsync(id).ConfigureAwait(false);
        _logger.Info($"Deleted screen-capture profile {id}", "ScreenCaptureProfileService");
        await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.CAPTURE_PROFILE_DELETED,
            new { id }).ConfigureAwait(false);
    }
}
