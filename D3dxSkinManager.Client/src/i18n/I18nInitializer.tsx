import React, { useEffect, useState } from 'react';
import { I18nextProvider } from 'react-i18next';
import { Spin } from 'antd';
import i18n, { loadLanguageFromSettings } from './i18n';
import logger from '../shared/utils/logger';

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
      } catch (error) {
        logger.error('[I18nInitializer] Failed to initialize i18next:', error);
        // Still set initialized to true to prevent infinite loading
        setIsInitialized(true);
      }
    };

    initialize();
  }, []);

  if (!isInitialized) {
    return (
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          height: '100vh',
          width: '100vw',
        }}
      >
        <Spin size="large" description="Loading..." />
      </div>
    );
  }

  return <I18nextProvider i18n={i18n}>{children}</I18nextProvider>;
};
