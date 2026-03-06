/**
 * Settings operations - Business logic for settings management
 * Centralized operations with consistent error handling and state updates
 */

import { useSettingsStore } from "../store/settingsStore";
import { logger } from "../../../shared/utils/logger";
import { notification } from "../../../shared/utils/notification";
import { handleError } from "../../../shared/utils/errorHandler";
import { profileService, settingsService } from "../../../shared/services/ipc";
import { executeWithDelayedLoading } from "../../../shared/utils/delayedLoading";

/**
 * Load global settings from backend
 * Called on app initialization or settings view mount
 */
export async function loadGlobalSettings(): Promise<void> {
  const { setGlobalSettings, setGlobalSettingsLoading, setLogLevel, setError } =
    useSettingsStore.getState();

  executeWithDelayedLoading(
    async () => {
      setError(undefined);
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
      } catch (error: unknown) {
        logger.error(
          "[settingsOperations] Failed to load global settings:",
          error,
        );
        setError(
          error instanceof Error
            ? error.message
            : "Failed to load global settings",
        );
        // Fallback to current logger level on error
        const currentLevel = logger.getCurrentLevelName();
        setLogLevel(currentLevel);
        handleError(error);
      }
    },
    setGlobalSettingsLoading,
    200,
  );
}

/**
 * Update log level
 * Updates both store and backend
 */
export async function updateLogLevel(
  level: string,
  t: (key: string, params?: any) => string,
): Promise<void> {
  const { setLogLevel } = useSettingsStore.getState();

  setLogLevel(level);

  try {
    await settingsService.updateGlobalSetting("logLevel", level);
    notification.success(
      t("settings.notifications.logLevelChanged", { level }),
    );
  } catch (error: unknown) {
    notification.error(t("settings.notifications.logLevelFailed"));
    logger.error("[settingsOperations] Failed to save log level:", error);
    handleError(error);
  }
}

/**
 * Reset window state
 * Window will be centered on next restart
 */
export async function resetWindowState(
  t: (key: string) => string,
): Promise<void> {
  try {
    await settingsService.resetWindowState();
    notification.success(t("settings.notifications.windowStateReset"));
  } catch (error: unknown) {
    notification.error(t("settings.notifications.windowStateResetFailed"));
    logger.error("[settingsOperations] Failed to reset window state:", error);
    handleError(error);
  }
}

/**
 * Load profile configuration (work directory settings)
 * Called when profile changes or settings view mounts
 */
export async function loadProfileConfig(profileId: string): Promise<void> {
  const { setInitialProfileConfig, setInternalWorkPath, setError } =
    useSettingsStore.getState();

  if (!profileId) {
    return;
  }

  setError(undefined);

  try {
    const config = await profileService.getProfileConfig(profileId);

    if (config) {
      // Use case-insensitive reading - normalize to lowercase
      const mode = (config.work?.mode?.toLowerCase() || "internal") as
        | "internal"
        | "external";
      const directory = config.work?.directory || "";
      const internalPath = config.work?.internalWorkDirectory || "";

      // Update store with initial config
      setInitialProfileConfig({ mode, directory });

      // Set internal work path from backend (for display when mode is "internal")
      if (internalPath) {
        setInternalWorkPath(internalPath);
      }
    }
  } catch (error: unknown) {
    logger.error("[settingsOperations] Failed to load profile config:", error);
    setError(
      error instanceof Error ? error.message : "Failed to load profile config",
    );
    handleError(error);
  }
}

/**
 * Save profile configuration (work directory settings)
 * Validates and persists work directory settings
 */
export async function saveProfileConfig(
  profileId: string,
  workMode: "internal" | "external",
  workDirectory: string,
  t: (key: string) => string,
): Promise<boolean> {
  const { setInitialProfileConfig, setError } = useSettingsStore.getState();

  if (!profileId) {
    notification.error(t("errors.noProfileSelected"));
    return false;
  }

  // Validate external directory if external mode
  if (workMode === "external") {
    const isValid = validateDirectoryPath(workDirectory);
    if (!isValid) {
      notification.error(t("settings.notifications.workDirectoryInvalid"));
      return false;
    }
  }

  setError(undefined);

  try {
    await profileService.updateProfileConfig({
      profileId,
      work: {
        mode: workMode,
        directory: workMode === "external" ? workDirectory : undefined,
      },
    });

    // Update initial config in store to reflect saved state
    setInitialProfileConfig({
      mode: workMode,
      directory: workDirectory,
    });

    notification.success(t("settings.notifications.profileConfigSaved"));
    return true;
  } catch (error: unknown) {
    notification.error(t("settings.notifications.profileConfigSaveFailed"));
    logger.error("[settingsOperations] Failed to save profile config:", error);
    setError(
      error instanceof Error ? error.message : "Failed to save profile config",
    );
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
