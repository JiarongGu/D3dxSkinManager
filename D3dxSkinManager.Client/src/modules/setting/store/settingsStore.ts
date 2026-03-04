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
import { GlobalSettings } from '../../../shared/services/ipc';

// ============================================================================
// State Interface
// ============================================================================

export interface SettingsState {
  // Global Settings
  globalSettings: GlobalSettings | undefined;
  globalSettingsLoading: boolean;

  // Log Level
  logLevel: string;

  // Profile-Specific Settings (Work Directory)
  workMode: 'internal' | 'external';
  workDirectory: string;
  internalWorkPath: string;
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

  // Profile Settings Actions (Work Directory)
  setWorkMode: (mode: 'internal' | 'external') => void;
  setWorkDirectory: (directory: string) => void;
  setInternalWorkPath: (path: string) => void;
  setProfileConfigChanged: (changed: boolean) => void;
  setInitialProfileConfig: (config: { mode: 'internal' | 'external'; directory: string }) => void;

  // Combined Actions
  updateWorkSettings: (mode: 'internal' | 'external', directory: string) => void;
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

  // Profile-Specific Settings (Work Directory)
  workMode: 'internal',
  workDirectory: '',
  internalWorkPath: '',
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
    // Profile Settings Actions (Work Directory)
    // ============================================================

    setWorkMode: (mode) =>
      set((state) => {
        state.workMode = mode;
        // Check if config changed
        const hasChanged =
          mode !== state.initialProfileConfig.mode ||
          state.workDirectory !== state.initialProfileConfig.directory;
        state.profileConfigChanged = hasChanged;
      }),

    setWorkDirectory: (directory) =>
      set((state) => {
        state.workDirectory = directory;
        // Check if config changed
        const hasChanged =
          state.workMode !== state.initialProfileConfig.mode ||
          directory !== state.initialProfileConfig.directory;
        state.profileConfigChanged = hasChanged;
      }),

    setInternalWorkPath: (path) =>
      set((state) => {
        state.internalWorkPath = path;
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
        state.profileConfigChanged = false;
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
          directory !== state.initialProfileConfig.directory;
        state.profileConfigChanged = hasChanged;
      }),

    resetProfileConfig: () =>
      set((state) => {
        state.workMode = state.initialProfileConfig.mode;
        state.workDirectory = state.initialProfileConfig.directory;
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
