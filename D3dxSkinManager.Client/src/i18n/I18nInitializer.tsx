import React, { useEffect, useState } from 'react';
import { I18nextProvider } from 'react-i18next';
import { Spin } from 'antd';
import i18n, { loadLanguageFromSettings } from './i18n';
import logger from '../shared/utils/logger';
import { eventBus, Module, SettingsEventType } from '../shared/services/eventBus';

interface I18nInitializerProps {
  children: React.ReactNode;
}

/**
 * Initializes i18next before rendering children
 * Loads language preference from backend settings
 */
export const I18nInitializer: React.FC<I18nInitializerProps> = ({ children }) => {
  const [isInitialized, setIsInitialized] = useState(false);

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

  // Listen for global settings changes from backend (syncs language across all windows)
  useEffect(() => {
    const unsubscribe = eventBus.subscribe(
      Module.SETTING,
      SettingsEventType.GLOBAL_SETTINGS_CHANGED,
      async (event) => {
        if (event.payload?.language) {
          const newLanguage = event.payload.language;
          logger.info('[I18nInitializer] Language changed via event:', newLanguage);

          // Change i18n language
          try {
            await i18n.changeLanguage(newLanguage);
            logger.info('[I18nInitializer] Language changed successfully to:', newLanguage);
          } catch (error: unknown) {
            logger.error('[I18nInitializer] Failed to change language:', error);
          }
        }
      }
    );

    return unsubscribe;
  }, []);

  if (!isInitialized) {
    return (
      <div className="i18n-loading-container">
        <Spin size="large" />
      </div>
    );
  }

  return <I18nextProvider i18n={i18n}>{children}</I18nextProvider>;
};
