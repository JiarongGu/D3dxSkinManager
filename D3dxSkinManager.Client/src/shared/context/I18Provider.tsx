import React, { useEffect, useState } from 'react';
import { I18nextProvider } from 'react-i18next';
import { Spin } from 'antd';
import logger from '../utils/logger';
import i18n, { loadLanguageFromSettings } from '../services/i18n';
import { useSettingsStore } from '../../modules/setting/store/settingsStore';

interface I18ProviderProps {
  children: React.ReactNode;
}

/**
 * Initializes i18next before rendering children
 * Loads language preference from backend settings
 */
export const I18Provider: React.FC<I18ProviderProps> = ({ children }) => {
  const [isInitialized, setIsInitialized] = useState(false);
  const globalSettings = useSettingsStore((state) => state.globalSettings);

  useEffect(() => {
    const initialize = async () => {
      try {
        logger.info('[I18nInitializer] Initializing i18next...');
        await loadLanguageFromSettings();
        logger.info('[I18nInitializer] i18next initialized successfully');
        setIsInitialized(true);
      } catch (error: unknown) {
        logger.error('[I18nInitializer] Failed to initialize i18next:', error);
        // Still set initialized to true to prevent infinite loading
        setIsInitialized(true);
      }
    };

    initialize();
  }, []);

  // Note: We don't listen to GLOBAL_SETTINGS_CHANGED here
  // SettingsProvider handles that event and reloads settings into the store
  // We just react to store changes via the useEffect on globalSettings below
  // This prevents duplicate event handlers and race conditions
  useEffect(() => {
    if (globalSettings?.language && isInitialized && i18n.language !== globalSettings.language) {
      logger.info('[I18nInitializer] Language changed via store:', globalSettings.language);
      i18n.changeLanguage(globalSettings.language).catch((error: unknown) => {
        logger.error('[I18nInitializer] Failed to change language:', error);
      });
    }
  }, [globalSettings, isInitialized]);

  if (!isInitialized) {
    return (
      <div className="i18n-loading-container">
        <Spin size="large" />
      </div>
    );
  }

  return <I18nextProvider i18n={i18n}>{children}</I18nextProvider>;
};
