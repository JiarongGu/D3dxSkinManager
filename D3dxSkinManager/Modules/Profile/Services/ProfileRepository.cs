using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Profiles.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace D3dxSkinManager.Modules.Profiles.Services
{
    public interface IProfileRepository
    {
        Task CreateProfileAsync(Profile profile);

        Task UpdateProfileAsync(Profile profile);

        Task DeleteProfileAsync(string profileId);

        Task<Profile?> GetProfileAsync(string profileId);

        Task<List<Profile>> GetAllProfilesAsync();

        Task<string> GetActiveProfileIdAsync();

        Task SetActiveProfileIdAsync(string profileId);

        Task<ProfileConfiguration?> GetProfileConfigurationAsync(string profileId);

        Task SaveProfileConfigurationAsync(string profileId, ProfileConfiguration config);
    }

    public class ProfileRepository : IProfileRepository
    {
        private readonly IGlobalPathService _globalPaths;
        private readonly IFileHelper _fileHelper;
        private readonly ILogHelper _logger;
        private readonly IMemoryCache _cache;

        private readonly SemaphoreSlim _profilesLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _configurationsLock = new SemaphoreSlim(1, 1);

        private List<Profile> _profiles = new List<Profile>();
        private string _activeProfileId = string.Empty;

        public ProfileRepository(IGlobalPathService globalPath, IFileHelper fileHelper, ILogHelper logger, IMemoryCache cache)
        {
            _globalPaths = globalPath;
            _fileHelper = fileHelper;
            _logger = logger;
            _cache = cache;

            LoadProfiles();
        }

        private void LoadProfiles()
        {
            try
            {
                if (!File.Exists(_globalPaths.ProfilesConfigPath))
                {
                    _logger.Info("Profiles configuration file not found. Will be created when first profile is added.", "ProfileRepository");
                    return;
                }

                var json = File.ReadAllText(_globalPaths.ProfilesConfigPath);
                var data = JsonSerializer.Deserialize<ProfilesData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data != null)
                {
                    _profiles = data.Profiles;
                    _activeProfileId = data.ActiveProfileId;
                    _logger.Info($"Loaded {_profiles.Count} profile(s) from disk.", "ProfileRepository");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load profiles from disk: {ex.Message}", "ProfileRepository");
            }
        }

        /// <summary>
        /// Ensure profiles are loaded from disk if the list is empty
        /// This handles cases where SaveProfileConfigurationAsync is called before any other profile operations
        /// NOTE: Caller must NOT hold _profilesLock when calling this method to avoid deadlock
        /// </summary>
        private async Task EnsureProfilesLoadedAsync()
        {
            // Quick check without lock first (optimization)
            if (_profiles.Count > 0) return;

            await _profilesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                if (_profiles.Count == 0 && File.Exists(_globalPaths.ProfilesConfigPath))
                {
                    _logger.Info("Profiles list is empty, reloading from disk", "ProfileRepository");
                    LoadProfiles();
                }
            }
            finally
            {
                _profilesLock.Release();
            }
        }

        public async Task CreateProfileAsync(Profile profile)
        {
            await _profilesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_profiles.FirstOrDefault(p => p.Id == profile.Id) != null)
                {
                    _logger.Warn($"Profile with ID {profile.Id} already exists. Skipping creation.", "ProfileRepository");
                    return;
                }

                _profiles.Add(profile);

                // If this is the first profile, set it as active
                if (string.IsNullOrEmpty(_activeProfileId))
                {
                    _activeProfileId = profile.Id;
                    _logger.Info($"Set first profile as active: {profile.Id}", "ProfileRepository");
                }

                // Create profile data directory
                var profileDir = _globalPaths.GetProfileDirectoryPath(profile.Id);
                await _fileHelper.CreateDirectoryAsync(profileDir).ConfigureAwait(false);

                await SaveProfilesAsync().ConfigureAwait(false);
            }
            finally
            {
                _profilesLock.Release();
            }
        }

        public async Task UpdateProfileAsync(Profile profile)
        {
            await _profilesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var index = _profiles.FindIndex(p => p.Id == profile.Id);
                if (index == -1)
                {
                    _logger.Warn($"Profile with ID {profile.Id} not found. Cannot update.", "ProfileRepository");
                    return;
                }

                _profiles[index] = profile;

                await SaveProfilesAsync().ConfigureAwait(false);
            }
            finally
            {
                _profilesLock.Release();
            }
        }

        public async Task DeleteProfileAsync(string profileId)
        {
            await _profilesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (profileId == _activeProfileId)
                {
                    throw new InvalidOperationException("Cannot delete the active profile. Please switch to another profile first.");
                }

                var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
                if (profile == null)
                {
                    _logger.Warn($"Profile with ID {profileId} not found. Cannot delete.", "ProfileRepository");
                    return;
                }
                _profiles.Remove(profile);

                // Delete profile directory
                var profileDir = _globalPaths.GetProfileDirectoryPath(profileId);
                try
                {
                    await _fileHelper.DeleteDirectoryAsync(profileDir).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to delete profile directory: {ex.Message}", "ProfileRepository");
                }

                // Remove cached configuration
                _cache.Remove($"ProfileConfig_{profileId}");

                await SaveProfilesAsync().ConfigureAwait(false);
            }
            finally
            {
                _profilesLock.Release();
            }
        }

        private async Task SaveProfilesAsync()
        {
            try
            {
                // Directory should already exist (created by GlobalPathService.EnsureDirectoriesExist())
                await JsonHelper.SerializeToFileAsync(_globalPaths.ProfilesConfigPath, new ProfilesData
                {
                    Profiles = _profiles,
                    ActiveProfileId = _activeProfileId
                }).ConfigureAwait(false);

                _logger.Info($"Saved {_profiles.Count} profile(s) to disk.", "ProfileRepository");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to save profiles to disk: {ex.Message}", "ProfileRepository");
            }
        }

        public async Task<Profile?> GetProfileAsync(string profileId)
        {
            await _profilesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
                if (profile == null)
                {
                    _logger.Warn($"Profile with ID {profileId} not found.", "ProfileRepository");
                    return null;
                }
                return profile;
            }
            finally
            {
                _profilesLock.Release();
            }
        }

        public async Task<List<Profile>> GetAllProfilesAsync()
        {
            await _profilesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return _profiles.ToList();
            }
            finally
            {
                _profilesLock.Release();
            }
        }

        public async Task<string> GetActiveProfileIdAsync()
        {
            await _profilesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return _activeProfileId;
            }
            finally
            {
                _profilesLock.Release();
            }
        }

        public async Task SetActiveProfileIdAsync(string profileId)
        {
            await _profilesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
                if (profile == null)
                {
                    throw new ArgumentException($"Profile with ID {profileId} not found.", nameof(profileId));
                }

                _activeProfileId = profileId;
                await SaveProfilesAsync().ConfigureAwait(false);

                _logger.Info($"Active profile set to: {profile.Name} ({profileId})", "ProfileRepository");
            }
            finally
            {
                _profilesLock.Release();
            }
        }

        /// <summary>Existence check that safely reads the shared <c>_profiles</c> list under _profilesLock.
        /// Callers must NOT hold _configurationsLock when calling (kept separate to avoid a nested lock).</summary>
        private async Task<bool> ProfileExistsAsync(string profileId)
        {
            await _profilesLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return _profiles.Any(p => p.Id == profileId);
            }
            finally
            {
                _profilesLock.Release();
            }
        }

        public async Task<ProfileConfiguration?> GetProfileConfigurationAsync(string profileId)
        {
            var cacheKey = $"ProfileConfig_{profileId}";

            // Check cache first
            if (_cache.TryGetValue(cacheKey, out ProfileConfiguration? cachedConfig))
            {
                return cachedConfig;
            }

            // Existence check under _profilesLock — _profiles (List<Profile>) is mutated under _profilesLock
            // by Create/Update/Delete, so reading it under the independent _configurationsLock was a data
            // race. Acquire + release _profilesLock BEFORE _configurationsLock so the two are never held
            // together (deadlock-free: nothing acquires _configurationsLock while holding _profilesLock).
            if (!await ProfileExistsAsync(profileId).ConfigureAwait(false))
            {
                _logger.Warn($"Profile with ID {profileId} not found.", "ProfileRepository");
                return null;
            }

            await _configurationsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                if (_cache.TryGetValue(cacheKey, out cachedConfig))
                {
                    return cachedConfig;
                }

                var configPath = _globalPaths.GetProfileConfigPath(profileId);

                ProfileConfiguration? config;
                if (File.Exists(configPath))
                {
                    config = await JsonHelper.DeserializeFromFileAsync<ProfileConfiguration>(configPath);
                }
                else
                {
                    // Return default configuration
                    config = new ProfileConfiguration
                    {
                        ProfileId = profileId
                    };
                }

                if (config != null)
                {
                    // Ensure profileId is always populated (in case deserialized config doesn't have it)
                    config.ProfileId = profileId;

                    // Cache the configuration with sliding expiration (5 minutes)
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    };
                    _cache.Set(cacheKey, config, cacheOptions);
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load profile configuration for {profileId}: {ex.Message}", "ProfileRepository");
                return null;
            }
            finally
            {
                _configurationsLock.Release();
            }
        }

        public async Task SaveProfileConfigurationAsync(string profileId, ProfileConfiguration config)
        {
            // Ensure profiles are loaded from disk first (before acquiring any locks)
            await EnsureProfilesLoadedAsync().ConfigureAwait(false);

            // Existence check under _profilesLock (see GetProfileConfigurationAsync) — never held together
            // with _configurationsLock.
            if (!await ProfileExistsAsync(profileId).ConfigureAwait(false))
            {
                throw new ArgumentException($"Profile with ID {profileId} not found.", nameof(profileId));
            }

            await _configurationsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Ensure profileId is always set before saving
                config.ProfileId = profileId;

                var configPath = _globalPaths.GetProfileConfigPath(profileId);
                await JsonHelper.SerializeToFileAsync(configPath, config).ConfigureAwait(false);

                // Update cache with sliding expiration (5 minutes)
                var cacheKey = $"ProfileConfig_{profileId}";
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(5)
                };
                _cache.Set(cacheKey, config, cacheOptions);

                _logger.Info($"Saved configuration for profile: {profileId}", "ProfileRepository");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to save profile configuration for {profileId}: {ex.Message}", "ProfileRepository");
                throw;
            }
            finally
            {
                _configurationsLock.Release();
            }
        }

        private class ProfilesData
        {
            public List<Profile> Profiles { get; set; } = new();
            public string ActiveProfileId { get; set; } = string.Empty;
        }
    }
}
