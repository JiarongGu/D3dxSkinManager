import React, { useEffect } from "react";
import { Spin, Alert } from "antd";
import { useProfile } from "../context/ProfileContext";
import { useSettingsStore } from "../../modules/setting/store/settingsStore";
import { bridgeService } from "../services/bridgeService";
import { useTranslation } from 'react-i18next';
import './AppLoader.css';

interface AppLoaderProps {
  children: React.ReactNode;
}

/**
 * AppLoader Component
 *
 * Handles the app loading sequence:
 * 1. Wait for SettingsProvider to load global settings
 * 2. Wait for ProfileProvider to initialize profiles
 * 3. Render children when both are ready
 *
 * Note: SettingsProvider is loaded BEFORE AppLoader in the provider hierarchy,
 * so global settings are already being loaded when this component mounts.
 */
export const AppLoader: React.FC<AppLoaderProps> = ({ children }) => {
  const { t } = useTranslation();
  const { selectedProfile, loading: profilesLoading } = useProfile();

  // Get settings state from SettingsProvider
  const globalSettings = useSettingsStore(s => s.globalSettings);
  const settingsError = useSettingsStore(s => s.error);

  // Notify backend when app is fully initialized
  useEffect(() => {
    if (globalSettings && !profilesLoading && selectedProfile) {
      // Notify backend that WebView is ready
      // This clears stale drop zones and hides the splash screen
      bridgeService.notifyWebViewReady();
    }
  }, [globalSettings, profilesLoading, selectedProfile]);

  // Determine if app is ready
  const isReady = globalSettings && !profilesLoading && selectedProfile;

  // Render based on loading stage
  if (settingsError) {
    return (
      <div className="app-loader-error-container">
        <Alert
          message={t('app.init.errorTitle')}
          description={settingsError}
          type="error"
          showIcon
        />
      </div>
    );
  }

  if (!isReady) {
    return (
      <div className="app-loader-loading-container">
        <Spin size="large" />
        <div className="app-loader-loading-message">{t('app.loading')}</div>
      </div>
    );
  }

  // Ready - render children
  return <>{children}</>;
};
