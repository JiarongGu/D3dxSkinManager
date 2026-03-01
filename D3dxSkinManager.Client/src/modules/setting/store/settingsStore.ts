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
import { GlobalSettings } from '../services/settingsService';

// ============================================================================
// State Interface
// ============================================================================

export interface SettingsState {
  // Global Settings
  globalSettings: GlobalSettings | undefined;
  globalSettingsLoading: boolean;

  // Log Level
  logLevel: string;

  // Profile-Specific Settings
  modCacheMode: 'internal' | 'external';
  modCacheDirectory: string;
  internalModCachePath: string;
  profileConfigChanged: boolean;
  initialProfileConfig: {
    mode: 'internal' | 'external';
    directory: string;
  };

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

  // Profile Settings Actions
  setModCacheMode: (mode: 'internal' | 'external') => void;
  setModCacheDirectory: (directory: string) => void;
  setInternalModCachePath: (path: string) => void;
  setProfileConfigChanged: (changed: boolean) => void;
  setInitialProfileConfig: (config: { mode: 'internal' | 'external'; directory: string }) => void;

  // Combined Actions
  updateModCacheSettings: (mode: 'internal' | 'external', directory: string) => void;
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

  // Profile-Specific Settings
  modCacheMode: 'internal',
  modCacheDirectory: '',
  internalModCachePath: '',
  profileConfigChanged: false,
  initialProfileConfig: {
    mode: 'internal',
    directory: '',
  },

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
    // Profile Settings Actions
    // ============================================================

    setModCacheMode: (mode) =>
      set((state) => {
        state.modCacheMode = mode;
        // Check if config changed
        const hasChanged =
          mode !== state.initialProfileConfig.mode ||
          state.modCacheDirectory !== state.initialProfileConfig.directory;
        state.profileConfigChanged = hasChanged;
      }),

    setModCacheDirectory: (directory) =>
      set((state) => {
        state.modCacheDirectory = directory;
        // Check if config changed
        const hasChanged =
          state.modCacheMode !== state.initialProfileConfig.mode ||
          directory !== state.initialProfileConfig.directory;
        state.profileConfigChanged = hasChanged;
      }),

    setInternalModCachePath: (path) =>
      set((state) => {
        state.internalModCachePath = path;
      }),

    setProfileConfigChanged: (changed) =>
      set((state) => {
        state.profileConfigChanged = changed;
      }),

    setInitialProfileConfig: (config) =>
      set((state) => {
        state.initialProfileConfig = config;
        state.modCacheMode = config.mode;
        state.modCacheDirectory = config.directory;
        state.profileConfigChanged = false;
      }),

    // ============================================================
    // Combined Actions
    // ============================================================

    updateModCacheSettings: (mode, directory) =>
      set((state) => {
        state.modCacheMode = mode;
        state.modCacheDirectory = directory;
        // Check if config changed
        const hasChanged =
          mode !== state.initialProfileConfig.mode ||
          directory !== state.initialProfileConfig.directory;
        state.profileConfigChanged = hasChanged;
      }),

    resetProfileConfig: () =>
      set((state) => {
        state.modCacheMode = state.initialProfileConfig.mode;
        state.modCacheDirectory = state.initialProfileConfig.directory;
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
