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
import { useProfile } from './ProfileContext';
import { useSettingsStore } from '../../modules/setting/store/settingsStore';
import { Module, SettingsEventType } from '../services/eventBus';
import { useEventSubscription } from '../hooks/useEventSubscription';
import * as settingsOps from '../../modules/setting/operations/settingsOperations';
import logger from '../utils/logger';
import { useStableRef } from '../hooks/useStableRef';

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
  // Load global settings once on mount
  // This prevents flash of default state when navigating to settings tab
  useEffect(() => {
    void settingsOps.loadGlobalSettings();
  }, []); // Run only once on mount

  // Subscribe to backend settings change events
  useEventSubscription(
    Module.SETTING,
    SettingsEventType.GLOBAL_SETTINGS_CHANGED,
    () => {
      logger.info('[SettingsProvider] Global settings changed event received, reloading settings...');
      void settingsOps.loadGlobalSettings();
    }
  );

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
        cleanupEnabled: true,
        cleanupMaxCaches: 10,
      });
    }
  }, [selectedProfileId]);

  // Note: We intentionally don't call reset() on profile change
  // because global settings (theme, language, log level) should persist
  // Only profile-specific settings (mod cache) should update

  return <>{children}</>;
};
