/**
 * SettingsProvider - handles initialization and profile changes for settings module
 *
 * Responsibilities:
 * - Load global settings on mount (prevents flash of default state)
 * - Load profile-specific settings when profile changes
 * - Persist settings state across tab switches (no reload needed)
 * - Reset state when profile is cleared
 *
 * DESIGN PRINCIPLE:
 * Settings are loaded once at app startup and persist in memory.
 * Tab switches don't trigger reloads - state is maintained.
 * This is loaded BEFORE AppInitializer to ensure settings are available.
 */

import React, { useEffect } from 'react';
import { useProfile } from '../../shared/context/ProfileContext';
import { useSettingsStore } from './store/settingsStore';
import { eventBus, Module, SettingsEventType } from '../../shared/services/eventBus';
import * as settingsOps from './operations/settingsOperations';

interface SettingsProviderProps {
  children: React.ReactNode;
}

/**
 * SettingsProvider - manages settings module lifecycle
 *
 * Similar to ModProvider pattern:
 * - Preloads data on mount
 * - Reactively updates when profile changes
 * - Maintains state across tab switches
 */
export const SettingsProvider: React.FC<SettingsProviderProps> = ({ children }) => {
  const { selectedProfileId } = useProfile();
  const reset = useSettingsStore((state) => state.reset);

  // Load global settings once on mount
  // This prevents flash of default state when navigating to settings tab
  useEffect(() => {
    void settingsOps.loadGlobalSettings();
  }, []); // Run only once on mount

  // Subscribe to backend settings change events
  useEffect(() => {
    const unsubscribe = eventBus.subscribe(
      Module.SETTING,
      SettingsEventType.GLOBAL_SETTINGS_CHANGED,
      () => {
        console.log('[SettingsProvider] Global settings changed event received, reloading settings...');
        void settingsOps.loadGlobalSettings();
      }
    );

    return () => {
      unsubscribe();
    };
  }, []);

  // Handle profile changes - load profile-specific settings
  useEffect(() => {
    if (selectedProfileId) {
      // Load profile configuration (mod cache settings)
      void settingsOps.loadProfileConfig(selectedProfileId);
    } else {
      // Reset profile-specific settings when no profile is selected
      // Keep global settings intact
      const { setInitialProfileConfig } = useSettingsStore.getState();
      setInitialProfileConfig({
        mode: 'internal',
        directory: '',
      });
    }
  }, [selectedProfileId]);

  // Note: We intentionally don't call reset() on profile change
  // because global settings (theme, language, log level) should persist
  // Only profile-specific settings (mod cache) should update

  return <>{children}</>;
};
