using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Profiles.Services;
using System.Text.Json;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Service responsible for eager loading operations during application startup
/// to improve perceived performance by pre-warming caches and initializing
/// heavy operations during the splash screen phase.
/// </summary>
public interface IEagerLoadingService
{
    /// <summary>
    /// Perform eager loading operations asynchronously.
    /// This should be called during splash screen display to pre-warm caches
    /// and initialize heavy operations before the UI is fully interactive.
    /// Includes both global (database, profiles) and profile-scoped (category tree, mods) operations.
    /// </summary>
    /// <param name="progress">Optional progress reporter for splash screen updates</param>
    /// <returns>Task representing the async operation</returns>
    Task EagerLoadAsync(IProgress<EagerLoadingProgress>? progress = null);
}

/// <summary>
/// Progress information for eager loading operations
/// </summary>
public class EagerLoadingProgress
{
    /// <summary>
    /// Current operation being performed (e.g., "Initializing database", "Loading active profile")
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Percent { get; set; }

    /// <summary>
    /// Whether the operation completed successfully
    /// </summary>
    public bool IsComplete { get; set; }
}


/// <summary>
/// Service that performs eager loading of commonly used data during application startup.
/// Operations are performed asynchronously during splash screen display to improve
/// perceived performance.
///
/// Eager Loading Strategy:
/// 1. Database initialization (if not already initialized)
/// 2. Active profile detection and loading
/// 3. Profile-scoped cache warming via MessageDispatcher (category tree, mod statistics)
///
/// Uses MessageDispatcher to trigger profile-scoped operations, which naturally routes
/// through ProfileServiceRouter and respects the active profile context.
/// </summary>
public class EagerLoadingService : IEagerLoadingService
{
    private readonly ILogHelper _logger;
    private readonly IProfileService _profileService;
    private readonly IMessageDispatcher _messageDispatcher;

    public EagerLoadingService(
        ILogHelper logger,
        IProfileService profileService,
        IMessageDispatcher messageDispatcher)
    {
        _logger = logger;
        _profileService = profileService;
        _messageDispatcher = messageDispatcher;
    }

    /// <summary>
    /// Perform eager loading operations during startup
    /// </summary>
    public async Task EagerLoadAsync(IProgress<EagerLoadingProgress>? progress = null)
    {
        _logger.Info("Starting eager loading operations...", "EagerLoading");

        try
        {
            // Step 1: Initialize database connections (if needed)
            await InitializeDatabaseAsync(progress);

            // Step 2: Load active profile
            var activeProfile = await LoadActiveProfileAsync(progress);

            // Step 3: If there's an active profile, pre-warm profile-scoped caches
            if (activeProfile != null)
            {
                await WarmProfileCachesAsync(activeProfile.Id, progress);
            }

            // Mark as complete
            progress?.Report(new EagerLoadingProgress
            {
                Operation = "Ready",
                Percent = 100,
                IsComplete = true
            });

            _logger.Info("Eager loading completed successfully", "EagerLoading");
        }
        catch (Exception ex)
        {
            // Don't let eager loading failures crash the app
            // Log the error and continue - the app will work, just without pre-warmed caches
            _logger.Error($"Eager loading failed (non-critical): {ex.Message}", "EagerLoading", ex);
        }
    }

    /// <summary>
    /// Initialize database connections early
    /// </summary>
    private async Task InitializeDatabaseAsync(IProgress<EagerLoadingProgress>? progress)
    {
        progress?.Report(new EagerLoadingProgress
        {
            Operation = "Initializing database...",
            Percent = 10
        });

        _logger.Verbose("Initializing database connections", "EagerLoading");

        // Database initialization happens automatically via DI when services are first accessed
        // We just need to trigger a lightweight query to ensure connections are established
        try
        {
            await _profileService.GetAllProfilesAsync();
            _logger.Verbose("Database initialized successfully", "EagerLoading");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Database initialization warning: {ex.Message}", "EagerLoading");
        }
    }

    /// <summary>
    /// Load the active profile early
    /// </summary>
    private async Task<Profiles.Models.Profile?> LoadActiveProfileAsync(IProgress<EagerLoadingProgress>? progress)
    {
        progress?.Report(new EagerLoadingProgress
        {
            Operation = "Loading active profile...",
            Percent = 30
        });

        _logger.Verbose("Loading active profile", "EagerLoading");

        try
        {
            var activeProfile = await _profileService.GetActiveProfileAsync();

            if (activeProfile != null)
            {
                _logger.Info($"Active profile loaded: {activeProfile.Name} (ID: {activeProfile.Id})", "EagerLoading");
            }
            else
            {
                _logger.Info("No active profile found", "EagerLoading");
            }

            return activeProfile;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to load active profile: {ex.Message}", "EagerLoading");
            return null;
        }
    }

    /// <summary>
    /// Warm profile-scoped caches using MessageDispatcher
    /// </summary>
    private async Task WarmProfileCachesAsync(string profileId, IProgress<EagerLoadingProgress>? progress)
    {
        _logger.Info($"Pre-warming caches for profile: {profileId}", "EagerLoading");

        // Pre-warm category tree
        await WarmCategoryTreeAsync(profileId, progress);
    }

    /// <summary>
    /// Pre-warm category tree cache via MessageDispatcher
    /// </summary>
    private async Task WarmCategoryTreeAsync(string profileId, IProgress<EagerLoadingProgress>? progress)
    {
        progress?.Report(new EagerLoadingProgress
        {
            Operation = "Generating category tree...",
            Percent = 100
        });

        _logger.Verbose($"Pre-warming category tree cache for profile: {profileId}", "EagerLoading");

        try
        {
            // Send CATEGORY.GET_CATEGORY_TREE message which will route through ProfileServiceRouter
            var result = await _messageDispatcher.SendAsync("CATEGORY", "GET_CATEGORY_TREE", profileId, null);

            if (result.Success)
            {
                _logger.Info("Category tree cached successfully", "EagerLoading");
            }
            else
            {
                _logger.Warn($"Category tree pre-warming returned error: {result.Error}", "EagerLoading");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to pre-warm category tree: {ex.Message}", "EagerLoading");
        }
    }
}
