/**
 * Settings operations - Business logic for settings management
 * Centralized operations with consistent error handling and state updates
 */

import { useSettingsStore } from "../store/settingsStore";
import { logger } from "../../../shared/utils/logger";
import { notification } from "../../../shared/utils/notification";
import { handleError } from "../../../shared/utils/errorHandler";
import { ModImportConfiguration, ModWorkConfiguration, profileService, settingsService } from "../../../shared/services/ipc";
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
 * Toggle the "check for updates automatically" global setting.
 * Optimistically updates the store, then persists to the backend.
 */
export async function updateAutoUpdateCheck(
  enabled: boolean,
  t: (key: string, params?: any) => string,
): Promise<void> {
  const { globalSettings, setGlobalSettings } = useSettingsStore.getState();

  // Optimistic store update so the Switch reflects immediately.
  if (globalSettings) {
    setGlobalSettings({ ...globalSettings, autoUpdateCheck: enabled });
  }

  try {
    await settingsService.updateGlobalSetting("autoUpdateCheck", String(enabled));
  } catch (error: unknown) {
    // Revert on failure.
    if (globalSettings) {
      setGlobalSettings({ ...globalSettings, autoUpdateCheck: !enabled });
    }
    notification.error(t("settings.notifications.settingsSaveFailed"));
    logger.error("[settingsOperations] Failed to save autoUpdateCheck:", error);
    handleError(error);
  }
}

/**
 * Toggle the content-veil global setting (blur previews the sensitivity heuristic flags).
 * Optimistically updates the store, then persists to the backend.
 */
export async function updateContentVeilEnabled(
  enabled: boolean,
  t: (key: string, params?: any) => string,
): Promise<void> {
  const { globalSettings, setGlobalSettings } = useSettingsStore.getState();

  if (globalSettings) {
    setGlobalSettings({ ...globalSettings, contentVeilEnabled: enabled });
  }

  try {
    await settingsService.updateGlobalSetting("contentVeilEnabled", String(enabled));
  } catch (error: unknown) {
    if (globalSettings) {
      setGlobalSettings({ ...globalSettings, contentVeilEnabled: !enabled });
    }
    notification.error(t("settings.notifications.settingsSaveFailed"));
    logger.error("[settingsOperations] Failed to save contentVeilEnabled:", error);
    handleError(error);
  }
}

/**
 * Set how many mod IMPORTS (extract+recompress, CPU-bound) run in parallel — the import queue's import
 * lane (clamped 1–8). Applies live: the backend emits GLOBAL_SETTINGS_CHANGED and the ImportQueueActor
 * updates its import-lane cap.
 */
export async function updateMaxParallelImports(
  value: number,
  t: (key: string, params?: any) => string,
): Promise<void> {
  const { globalSettings, setGlobalSettings } = useSettingsStore.getState();
  const clamped = Math.min(8, Math.max(1, Math.round(value)));
  const previous = globalSettings?.maxParallelImports ?? 5;

  if (globalSettings) {
    setGlobalSettings({ ...globalSettings, maxParallelImports: clamped });
  }

  try {
    await settingsService.updateGlobalSetting("maxParallelImports", String(clamped));
  } catch (error: unknown) {
    if (globalSettings) {
      setGlobalSettings({ ...globalSettings, maxParallelImports: previous });
    }
    notification.error(t("settings.notifications.settingsSaveFailed"));
    logger.error("[settingsOperations] Failed to save maxParallelImports:", error);
    handleError(error);
  }
}

/**
 * Set how many remote DOWNLOADS (network-bound) run in parallel — the import queue's download lane,
 * separate from imports so a slow download doesn't hold a compress slot (a finished download waits for an
 * import slot). Clamped 1–8. Applies live via GLOBAL_SETTINGS_CHANGED.
 */
export async function updateMaxParallelDownloads(
  value: number,
  t: (key: string, params?: any) => string,
): Promise<void> {
  const { globalSettings, setGlobalSettings } = useSettingsStore.getState();
  const clamped = Math.min(8, Math.max(1, Math.round(value)));
  const previous = globalSettings?.maxParallelDownloads ?? 4;

  if (globalSettings) {
    setGlobalSettings({ ...globalSettings, maxParallelDownloads: clamped });
  }

  try {
    await settingsService.updateGlobalSetting("maxParallelDownloads", String(clamped));
  } catch (error: unknown) {
    if (globalSettings) {
      setGlobalSettings({ ...globalSettings, maxParallelDownloads: previous });
    }
    notification.error(t("settings.notifications.settingsSaveFailed"));
    logger.error("[settingsOperations] Failed to save maxParallelDownloads:", error);
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
 * Load profile configuration (work directory, cleanup, mod import settings)
 * Called when profile changes or settings view mounts
 */
export async function loadProfileConfig(profileId: string): Promise<void> {
  const { setInitialProfileConfig, setInternalWorkPath, setInitialModImportConfig, setLaunchConfig, setGameUpdated, setError } =
    useSettingsStore.getState();

  if (!profileId) {
    return;
  }

  setError(undefined);

  try {
    const config = await profileService.getProfileConfig(profileId);

    if (config) {
      // Mod work directory and cleanup configuration
      const mode = (config.modWork?.mode?.toLowerCase() || "internal") as ModWorkConfiguration['mode'];
      const directory = config.modWork?.directory || "";
      const internalPath = config.modWork?.internalDirectory || "";
      const cleanupEnabled = config.modWork?.cleanupEnabled ?? true;
      const cleanupMaxCaches = config.modWork?.cleanupMaxCaches ?? 10;

      // Update store with initial config
      setInitialProfileConfig({
        mode,
        directory,
        cleanupEnabled,
        cleanupMaxCaches,
      });

      // Set internal work path from backend (for display when mode is "internal")
      if (internalPath) {
        setInternalWorkPath(internalPath);
      }

      // Mod import configuration
      const compressionType = (config.modImport?.compressionType || "7z") as ModImportConfiguration['compressionType'];
      const compressionMode = (config.modImport?.compressionMode || "high") as ModImportConfiguration['compressionMode'];

      // Update store with mod import config
      setInitialModImportConfig({
        compressionType,
        compressionMode,
      });

      // Launch config mirror (displayed by the XXMI binding summary)
      setLaunchConfig(config.launch?.path || "", config.launch?.args || "");

      // "Game updated" watermark (drives the mod-list "may need re-fix" flag)
      setGameUpdated(config.gameUpdatedUtc || undefined);
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
 * Save profile configuration (work directory, cleanup, mod import settings)
 * Validates and persists all profile configuration settings
 */
export async function saveProfileConfig(
  profileId: string,
  workMode: ModWorkConfiguration['mode'],
  workDirectory: string,
  cleanupEnabled: boolean,
  cleanupMaxCaches: number,
  compressionType: ModImportConfiguration['compressionType'],
  compressionMode: ModImportConfiguration['compressionMode'],
  t: (key: string) => string,
): Promise<boolean> {
  const { setInitialProfileConfig, setInitialModImportConfig, setError } = useSettingsStore.getState();

  if (!profileId) {
    notification.error(t("errors.noProfileSelected"));
    return false;
  }

  // Both external (custom) and xxmi store a custom directory — validate it.
  const usesCustomDir = workMode === "external" || workMode === "xxmi";
  if (usesCustomDir) {
    const isValid = validateDirectoryPath(workDirectory);
    if (!isValid) {
      notification.error(t("settings.notifications.workDirectoryInvalid"));
      return false;
    }
  }

  // Validate cleanupMaxCaches range
  const clampedMaxCaches = Math.max(1, Math.min(100, cleanupMaxCaches));

  setError(undefined);

  try {
    await profileService.updateProfileConfig({
      profileId,
      workMode,
      workDirectory: usesCustomDir ? workDirectory : undefined,
      cleanupEnabled,
      cleanupMaxCaches: clampedMaxCaches,
      compressionType,
      compressionMode,
    });

    // Update initial config in store to reflect saved state
    setInitialProfileConfig({
      mode: workMode,
      directory: workDirectory,
      cleanupEnabled,
      cleanupMaxCaches: clampedMaxCaches,
    });

    setInitialModImportConfig({
      compressionType,
      compressionMode,
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
