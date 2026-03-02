/**
 * Settings operations - Business logic for settings management
 * Centralized operations with consistent error handling and state updates
 */

import { useSettingsStore } from '../store/settingsStore';
import { settingsService } from '../services/settingsService';
import { profileService } from '../../profile/services/profileService';
import { logger } from '../../../shared/utils/logger';
import { notification } from '../../../shared/utils/notification';
import { handleError } from '../../../shared/utils/errorHandler';

/**
 * Load global settings from backend
 * Called on app initialization or settings view mount
 */
export async function loadGlobalSettings(): Promise<void> {
  const { setGlobalSettings, setGlobalSettingsLoading, setLogLevel, setError } = useSettingsStore.getState();

  setError(undefined);
  setGlobalSettingsLoading(true);

  try {
    const settings = await settingsService.getGlobalSettings();
    setGlobalSettings(settings);

    if (settings?.logLevel) {
      setLogLevel(settings.logLevel);
    } else {
      // Fallback to current logger level
      const currentLevel = logger.getCurrentLevelName();
      setLogLevel(currentLevel);
    }
  } catch (error) {
    logger.error('[settingsOperations] Failed to load global settings:', error);
    setError(error instanceof Error ? error.message : 'Failed to load global settings');
    // Fallback to current logger level on error
    const currentLevel = logger.getCurrentLevelName();
    setLogLevel(currentLevel);
    handleError(error);
  } finally {
    setGlobalSettingsLoading(false);
  }
}

/**
 * Update log level
 * Updates both store and backend
 */
export async function updateLogLevel(level: string, t: (key: string, params?: any) => string): Promise<void> {
  const { setLogLevel } = useSettingsStore.getState();

  setLogLevel(level);

  try {
    await settingsService.updateGlobalSetting('logLevel', level);
    notification.success(t('settings.notifications.logLevelChanged', { level }));
  } catch (error) {
    notification.error(t('settings.notifications.logLevelFailed'));
    logger.error('[settingsOperations] Failed to save log level:', error);
    handleError(error);
  }
}

/**
 * Reset window state
 * Window will be centered on next restart
 */
export async function resetWindowState(t: (key: string) => string): Promise<void> {
  try {
    await settingsService.resetWindowState();
    notification.success(t('settings.notifications.windowStateReset'));
  } catch (error) {
    notification.error(t('settings.notifications.windowStateResetFailed'));
    logger.error('[settingsOperations] Failed to reset window state:', error);
    handleError(error);
  }
}

/**
 * Load profile configuration (mod cache settings)
 * Called when profile changes or settings view mounts
 */
export async function loadProfileConfig(
  profileId: string
): Promise<void> {
  const { setInitialProfileConfig, setInternalModCachePath, setError } = useSettingsStore.getState();

  if (!profileId) {
    return;
  }

  setError(undefined);

  try {
    const config = await profileService.getProfileConfig(profileId);

    if (config) {
      // Use case-insensitive reading - normalize to lowercase
      const mode = (config.modCache?.mode?.toLowerCase() || 'internal') as 'internal' | 'external';
      const directory = config.modCache?.directory || '';

      // Update store with initial config
      setInitialProfileConfig({ mode, directory });

      // Internal path is now calculated by backend
      // No need to compute it here since profiles don't store dataDirectory anymore
    }
  } catch (error) {
    logger.error('[settingsOperations] Failed to load profile config:', error);
    setError(error instanceof Error ? error.message : 'Failed to load profile config');
    handleError(error);
  }
}

/**
 * Save profile configuration (mod cache settings)
 * Validates and persists mod cache settings
 */
export async function saveProfileConfig(
  profileId: string,
  modCacheMode: 'internal' | 'external',
  modCacheDirectory: string,
  t: (key: string) => string
): Promise<boolean> {
  const { setInitialProfileConfig, setError } = useSettingsStore.getState();

  if (!profileId) {
    notification.error(t('errors.noProfileSelected'));
    return false;
  }

  // Validate external directory if external mode
  if (modCacheMode === 'external') {
    const isValid = validateDirectoryPath(modCacheDirectory);
    if (!isValid) {
      notification.error(t('settings.notifications.modCacheDirectoryInvalid'));
      return false;
    }
  }

  setError(undefined);

  try {
    await profileService.updateProfileConfig({
      profileId,
      modCache: {
        mode: modCacheMode,
        directory: modCacheMode === 'external' ? modCacheDirectory : undefined,
      },
    });

    // Update initial config in store to reflect saved state
    setInitialProfileConfig({
      mode: modCacheMode,
      directory: modCacheDirectory,
    });

    notification.success(t('settings.notifications.profileConfigSaved'));
    return true;
  } catch (error) {
    notification.error(t('settings.notifications.profileConfigSaveFailed'));
    logger.error('[settingsOperations] Failed to save profile config:', error);
    setError(error instanceof Error ? error.message : 'Failed to save profile config');
    handleError(error);
    return false;
  }
}

/**
 * Validate directory path
 * For now, just checks if it's not empty
 * TODO: Add proper directory validation via backend
 */
function validateDirectoryPath(path: string): boolean {
  if (!path) return false;
  return path.trim().length > 0;
}
