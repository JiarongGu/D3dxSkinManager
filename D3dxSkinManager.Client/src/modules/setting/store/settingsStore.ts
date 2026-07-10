/**
 * Centralized Settings Store using Zustand
 * Single source of truth for all settings module state
 *
 * Zustand provides:
 * - Built-in subscriptions (no need for separate event bus)
 * - Selector-based subscriptions (only re-render when specific state changes)
 * - Middleware support (immer for immutable updates)
 * - Simple API with React hooks
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { GlobalSettings, ModImportConfiguration, ModWorkConfiguration } from '../../../shared/services/ipc';

// ============================================================================
// State Interface
// ============================================================================

export interface SettingsState {
  // Global Settings
  globalSettings: GlobalSettings | undefined;
  globalSettingsLoading: boolean;

  // Log Level
  logLevel: string;

  // Profile-Specific Settings (Mod Work Directory & Cleanup)
  workMode: ModWorkConfiguration['mode'];
  workDirectory: string;
  internalWorkPath: string;
  cleanupEnabled: boolean;
  cleanupMaxCaches: number;
  profileConfigChanged: boolean;
  initialProfileConfig: {
    mode: ModWorkConfiguration['mode'];
    directory: string;
    cleanupEnabled: boolean;
    cleanupMaxCaches: number;
  };

  // Profile-Specific Settings (Mod Import)
  compressionType: ModImportConfiguration['compressionType'];
  compressionMode: ModImportConfiguration["compressionMode"];
  initialModImportConfig: {
    compressionType: ModImportConfiguration['compressionType'];
    compressionMode: ModImportConfiguration['compressionMode'];
  };

  // Launch config (config.launch — what the status-bar Launch button runs). Editable in the Mod
  // Work tab; the XXMI bind flow auto-fills it. Baseline mirrors the other per-section configs.
  launchPath: string;
  launchArgs: string;
  initialLaunchConfig: {
    path: string;
    args: string;
  };

  /** config.gameUpdatedUtc — ISO time the user last marked the game updated (undefined = never). */
  gameUpdatedUtc: string | undefined;

  // UI State
  error: string | undefined;
}

// ============================================================================
// Actions Interface
// ============================================================================

export interface SettingsActions {
  // Global Settings Actions
  setGlobalSettings: (settings: GlobalSettings | undefined) => void;
  setGlobalSettingsLoading: (loading: boolean) => void;

  // Log Level Actions
  setLogLevel: (level: string) => void;

  // Profile Settings Actions (Work Directory & Cleanup)
  setWorkMode: (mode: ModWorkConfiguration['mode']) => void;
  setWorkDirectory: (directory: string) => void;
  setInternalWorkPath: (path: string) => void;
  setCleanupEnabled: (enabled: boolean) => void;
  setCleanupMaxCaches: (max: number) => void;
  setProfileConfigChanged: (changed: boolean) => void;
  setInitialProfileConfig: (config: { mode:  ModWorkConfiguration['mode']; directory: string; cleanupEnabled: boolean; cleanupMaxCaches: number }) => void;

  // Profile Settings Actions (Mod Import)
  setCompressionType: (type: ModImportConfiguration['compressionType']) => void;
  setCompressionMode: (mode: ModImportConfiguration['compressionMode']) => void;
  setInitialModImportConfig: (config: ModImportConfiguration) => void;

  // Launch config
  setLaunchPath: (path: string) => void;
  setLaunchArgs: (args: string) => void;
  /** Set current values AND the baseline (used on load + after a successful save/bind). */
  setLaunchConfig: (path: string, args: string) => void;

  /** Set the "game updated" watermark (mirrors config.gameUpdatedUtc; undefined = never marked). */
  setGameUpdated: (utc: string | undefined) => void;

  // Combined Actions
  updateWorkSettings: (mode:  ModWorkConfiguration['mode'], directory: string) => void;
  resetProfileConfig: () => void;

  // Error Actions
  setError: (error: string | undefined) => void;

  // Global Actions
  reset: () => void;
}

export type SettingsStore = SettingsState & SettingsActions;

// ============================================================================
// Initial State
// ============================================================================

const initialState: SettingsState = {
  // Global Settings
  globalSettings: undefined,
  globalSettingsLoading: false,

  // Log Level
  logLevel: 'info',

  // Profile-Specific Settings (Work Directory & Cleanup)
  workMode: 'internal',
  workDirectory: '',
  internalWorkPath: '',
  cleanupEnabled: true,
  cleanupMaxCaches: 10,
  profileConfigChanged: false,
  initialProfileConfig: {
    mode: 'internal',
    directory: '',
    cleanupEnabled: true,
    cleanupMaxCaches: 10,
  },

  // Profile-Specific Settings (Mod Import)
  compressionType: '7z',
  compressionMode: 'high',
  initialModImportConfig: {
    compressionType: '7z',
    compressionMode: 'high',
  },

  // Launch config
  launchPath: '',
  launchArgs: '',
  initialLaunchConfig: {
    path: '',
    args: '',
  },

  // "Game updated" watermark (config.gameUpdatedUtc) — mods fixed before it may need re-fixing.
  gameUpdatedUtc: undefined,

  // UI State
  error: undefined,
};

// ============================================================================
// Store Creation with Zustand
// ============================================================================

export const useSettingsStore = create<SettingsStore>()(
  immer((set, get) => ({
    ...initialState,

    // ============================================================
    // Global Settings Actions
    // ============================================================

    setGlobalSettings: (settings) =>
      set((state) => {
        state.globalSettings = settings;
        if (settings?.logLevel) {
          state.logLevel = settings.logLevel;
        }
      }),

    setGlobalSettingsLoading: (loading) =>
      set((state) => {
        state.globalSettingsLoading = loading;
      }),

    // ============================================================
    // Log Level Actions
    // ============================================================

    setLogLevel: (level) =>
      set((state) => {
        state.logLevel = level;
      }),

    // ============================================================
    // Profile Settings Actions (Work Directory & Cleanup)
    // ============================================================

    setWorkMode: (mode) =>
      set((state) => {
        state.workMode = mode;
        // Check if config changed
        const hasChanged =
          mode !== state.initialProfileConfig.mode ||
          state.workDirectory !== state.initialProfileConfig.directory ||
          state.cleanupEnabled !== state.initialProfileConfig.cleanupEnabled ||
          state.cleanupMaxCaches !== state.initialProfileConfig.cleanupMaxCaches ||
          state.compressionType !== state.initialModImportConfig.compressionType ||
          state.compressionMode !== state.initialModImportConfig.compressionMode;
        state.profileConfigChanged = hasChanged;
      }),

    setWorkDirectory: (directory) =>
      set((state) => {
        state.workDirectory = directory;
        // Check if config changed
        const hasChanged =
          state.workMode !== state.initialProfileConfig.mode ||
          directory !== state.initialProfileConfig.directory ||
          state.cleanupEnabled !== state.initialProfileConfig.cleanupEnabled ||
          state.cleanupMaxCaches !== state.initialProfileConfig.cleanupMaxCaches ||
          state.compressionType !== state.initialModImportConfig.compressionType ||
          state.compressionMode !== state.initialModImportConfig.compressionMode;
        state.profileConfigChanged = hasChanged;
      }),

    setInternalWorkPath: (path) =>
      set((state) => {
        state.internalWorkPath = path;
      }),

    setCleanupEnabled: (enabled) =>
      set((state) => {
        state.cleanupEnabled = enabled;
        // Check if config changed
        const hasChanged =
          state.workMode !== state.initialProfileConfig.mode ||
          state.workDirectory !== state.initialProfileConfig.directory ||
          enabled !== state.initialProfileConfig.cleanupEnabled ||
          state.cleanupMaxCaches !== state.initialProfileConfig.cleanupMaxCaches ||
          state.compressionType !== state.initialModImportConfig.compressionType ||
          state.compressionMode !== state.initialModImportConfig.compressionMode;
        state.profileConfigChanged = hasChanged;
      }),

    setCleanupMaxCaches: (max) =>
      set((state) => {
        state.cleanupMaxCaches = max;
        // Check if config changed
        const hasChanged =
          state.workMode !== state.initialProfileConfig.mode ||
          state.workDirectory !== state.initialProfileConfig.directory ||
          state.cleanupEnabled !== state.initialProfileConfig.cleanupEnabled ||
          max !== state.initialProfileConfig.cleanupMaxCaches ||
          state.compressionType !== state.initialModImportConfig.compressionType ||
          state.compressionMode !== state.initialModImportConfig.compressionMode;
        state.profileConfigChanged = hasChanged;
      }),

    setProfileConfigChanged: (changed) =>
      set((state) => {
        state.profileConfigChanged = changed;
      }),

    setInitialProfileConfig: (config) =>
      set((state) => {
        state.initialProfileConfig = config;
        state.workMode = config.mode;
        state.workDirectory = config.directory;
        state.cleanupEnabled = config.cleanupEnabled;
        state.cleanupMaxCaches = config.cleanupMaxCaches;
        state.profileConfigChanged = false;
      }),

    // ============================================================
    // Profile Settings Actions (Mod Import)
    // ============================================================

    setCompressionType: (type) =>
      set((state) => {
        state.compressionType = type;
        // Check if config changed
        const hasChanged =
          state.workMode !== state.initialProfileConfig.mode ||
          state.workDirectory !== state.initialProfileConfig.directory ||
          state.cleanupEnabled !== state.initialProfileConfig.cleanupEnabled ||
          state.cleanupMaxCaches !== state.initialProfileConfig.cleanupMaxCaches ||
          type !== state.initialModImportConfig.compressionType ||
          state.compressionMode !== state.initialModImportConfig.compressionMode;
        state.profileConfigChanged = hasChanged;
      }),

    setCompressionMode: (mode) =>
      set((state) => {
        state.compressionMode = mode;
        // Check if config changed
        const hasChanged =
          state.workMode !== state.initialProfileConfig.mode ||
          state.workDirectory !== state.initialProfileConfig.directory ||
          state.cleanupEnabled !== state.initialProfileConfig.cleanupEnabled ||
          state.cleanupMaxCaches !== state.initialProfileConfig.cleanupMaxCaches ||
          state.compressionType !== state.initialModImportConfig.compressionType ||
          mode !== state.initialModImportConfig.compressionMode;
        state.profileConfigChanged = hasChanged;
      }),

    setInitialModImportConfig: (config) =>
      set((state) => {
        state.initialModImportConfig = config;
        state.compressionType = config.compressionType;
        state.compressionMode = config.compressionMode;
      }),

    setLaunchPath: (path) =>
      set((state) => {
        state.launchPath = path;
      }),

    setLaunchArgs: (args) =>
      set((state) => {
        state.launchArgs = args;
      }),

    setLaunchConfig: (path, args) =>
      set((state) => {
        state.launchPath = path;
        state.launchArgs = args;
        state.initialLaunchConfig = { path, args };
      }),

    setGameUpdated: (utc) =>
      set((state) => {
        state.gameUpdatedUtc = utc;
      }),

    // ============================================================
    // Combined Actions
    // ============================================================

    updateWorkSettings: (mode, directory) =>
      set((state) => {
        state.workMode = mode;
        state.workDirectory = directory;
        // Check if config changed
        const hasChanged =
          mode !== state.initialProfileConfig.mode ||
          directory !== state.initialProfileConfig.directory ||
          state.cleanupEnabled !== state.initialProfileConfig.cleanupEnabled ||
          state.cleanupMaxCaches !== state.initialProfileConfig.cleanupMaxCaches ||
          state.compressionType !== state.initialModImportConfig.compressionType ||
          state.compressionMode !== state.initialModImportConfig.compressionMode;
        state.profileConfigChanged = hasChanged;
      }),

    resetProfileConfig: () =>
      set((state) => {
        state.workMode = state.initialProfileConfig.mode;
        state.workDirectory = state.initialProfileConfig.directory;
        state.cleanupEnabled = state.initialProfileConfig.cleanupEnabled;
        state.cleanupMaxCaches = state.initialProfileConfig.cleanupMaxCaches;
        state.compressionType = state.initialModImportConfig.compressionType;
        state.compressionMode = state.initialModImportConfig.compressionMode;
        state.profileConfigChanged = false;
      }),

    // ============================================================
    // Error Actions
    // ============================================================

    setError: (error) =>
      set((state) => {
        state.error = error;
      }),

    // ============================================================
    // Global Actions
    // ============================================================

    reset: () => set(initialState),
  }))
);
