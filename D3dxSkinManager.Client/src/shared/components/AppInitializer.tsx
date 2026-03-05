import React, { useState, useEffect } from "react";
import { Spin, Alert } from "antd";
import {
  settingsService,
  GlobalSettings,
} from "../services/ipc";
import { useProfile } from "../context/ProfileContext";
import { useModsStore } from "../../modules/mod/store/modsStore";
import { bridgeService } from "../services/bridgeService";
import { useTranslation } from 'react-i18next';
import './AppInitializer.css';

/**
 * Initialization state for the application
 */
interface InitState {
  stage:
    | "loading-global"
    | "ready"
    | "error";
  globalSettings: GlobalSettings | undefined;
  error: string | undefined;
}

interface AppInitializerProps {
  children: React.ReactNode;
}

/**
 * AppInitializer Component
 *
 * Handles the initialization sequence:
 * 1. Load global settings (no profileId needed)
 * 2. Wait for ProfileProvider to initialize profiles
 * 3. Render children when both are ready
 */
export const AppInitializer: React.FC<AppInitializerProps> = ({ children }) => {
  const { t } = useTranslation();
  const { selectedProfile, loading: profilesLoading } = useProfile();
  const setPanelSizes = useModsStore(s => s.setPanelSizes);
  const [state, setState] = useState<InitState>({
    stage: "loading-global",
    globalSettings: undefined,
    error: undefined,
  });

  // Step 1: Load global settings
  useEffect(() => {
    loadGlobalSettings();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Step 2: Mark as ready when both global settings and profiles are loaded
  useEffect(() => {
    if (state.globalSettings && !profilesLoading && selectedProfile && state.stage !== "ready") {
      setState((prev) => ({ ...prev, stage: "ready" }));
    }
  }, [state.globalSettings, profilesLoading, selectedProfile, state.stage]);

  // Step 3: Notify backend when app is fully initialized
  useEffect(() => {
    if (state.stage === "ready") {
      // Notify backend that WebView is ready
      // This clears any stale drop zones from previous sessions (e.g., after hot-reload)
      bridgeService.notifyWebViewReady();

      // Notify backend that the app is fully initialized
      // This will hide the splash screen
      bridgeService.notifyAppInitialized();
    }
  }, [state.stage]);

  const loadGlobalSettings = async () => {
    try {
      // Load global settings - no profileId needed
      const settings = await settingsService.getGlobalSettings();

      // Initialize panel sizes from settings
      if (settings.tabs?.mod?.panelSize) {
        const [category, modList] = settings.tabs.mod.panelSize.split(' ').map(Number);
        if (!isNaN(category) && !isNaN(modList)) {
          setPanelSizes({
            categoryWidth: category,
            modListWidth: modList,
            previewWidth: 100 - category - modList
          });
        }
      }

      setState((prev) => ({
        ...prev,
        globalSettings: settings,
      }));
    } catch (error: unknown) {
      // Error handled by error handler
      setState((prev) => ({
        ...prev,
        stage: "error",
        error: t('app.init.loadGlobalSettingsFailed'),
      }));
    }
  };


  // Render based on initialization stage
  if (state.stage === "error") {
    return (
      <div className="app-initializer-error-container">
        <Alert
          message={t('app.init.errorTitle')}
          description={state.error}
          type="error"
          showIcon
        />
      </div>
    );
  }

  if (state.stage !== "ready" || profilesLoading || !selectedProfile) {
    const loadingMessage =
      state.stage === "loading-global"
        ? t('app.init.loadingSettings')
        : profilesLoading
          ? t('app.init.loadingProfiles')
          : t('app.init.initializing');

    return (
      <div className="app-initializer-loading-container">
        <Spin size="large" />
        <div className="app-initializer-loading-message">{loadingMessage}</div>
      </div>
    );
  }

  // Ready - render children
  return <>{children}</>;
};
