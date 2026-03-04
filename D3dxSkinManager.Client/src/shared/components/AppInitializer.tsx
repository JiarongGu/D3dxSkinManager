import React, { useState, useEffect } from "react";
import { Spin, Alert } from "antd";
import { CompactButton } from "./compact";
import {
  settingsService,
  GlobalSettings,
} from "../services/ipc";
import { useProfile } from "../context/ProfileContext";
import { useModsStore } from "../../modules/mod/store/modsStore";
import { bridgeService } from "../services/bridgeService";
import { profileService } from "../services/ipc";
import { useTranslation } from 'react-i18next';
import './AppInitializer.css';

/**
 * Initialization state for the application
 */
interface InitState {
  stage:
    | "loading-global"
    | "loading-profiles"
    | "selecting-profile"
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
 * 2. Load all profiles from ProfileContext
 * 3. Determine which profile to use (from settings or user selection)
 * 4. Set the selected profile in ProfileContext
 * 5. Render children when ready
 */
export const AppInitializer: React.FC<AppInitializerProps> = ({ children }) => {
  const { t } = useTranslation();
  const { selectedProfile, profiles, actions } = useProfile();
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

  // Step 2: After global settings loaded, load profiles from ProfileContext
  useEffect(() => {
    if (state.globalSettings && state.stage === "loading-global") {
      setState((prev) => ({ ...prev, stage: "loading-profiles" }));
      actions.loadProfiles();
    }
  }, [state.globalSettings, state.stage, actions]);

  // Step 3: After profiles loaded, select initial profile
  useEffect(() => {
    if (
      profiles.length > 0 &&
      state.stage === "loading-profiles" &&
      !selectedProfile
    ) {
      selectInitialProfile();
    }
  }, [profiles, state.stage, selectedProfile]);

  // Step 4: Mark as ready when profile is selected
  useEffect(() => {
    if (selectedProfile && state.stage !== "ready" && state.stage !== "error") {
      setState((prev) => ({ ...prev, stage: "ready" }));
    }
  }, [selectedProfile, state.stage]);

  // Step 5: Notify backend when app is fully initialized
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

  const selectInitialProfile = async () => {
    try {
      // Get the active profile from the backend
      const profileList = await profileService.getAllProfiles();
      const activeProfileId = profileList.activeProfileId;
      let profileToSelect = profiles.find((p) => p.id === activeProfileId) || profiles[0];

      if (profileToSelect) {
        await actions.selectProfile(profileToSelect.id);
      } else {
        // No profiles exist - need to create one
        setState((prev) => ({
          ...prev,
          stage: "selecting-profile",
        }));
      }
    } catch (error: unknown) {
      // Error handled by error handler
      setState((prev) => ({
        ...prev,
        stage: "error",
        error: t('app.init.selectProfileFailed'),
      }));
    }
  };

  const handleProfileCreate = async (name: string, description?: string) => {
    try {
      const profile = await actions.createProfile(name, description);
      await actions.selectProfile(profile.id);
      setState((prev) => ({ ...prev, stage: "ready" }));
    } catch (error: unknown) {
      // Error handled by error handler
      setState((prev) => ({
        ...prev,
        stage: "error",
        error: t('app.init.createProfileFailed'),
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

  if (state.stage === "selecting-profile") {
    // TODO: Show profile creation dialog
    return (
      <div className="app-initializer-selecting-container">
        <Alert
          message={t('app.init.noProfilesTitle')}
          description={t('app.init.noProfilesDescription')}
          type="info"
          showIcon
        />
        <CompactButton
          type="primary"
          onClick={() => handleProfileCreate("Default", "My first profile")}
        >
          {t('app.init.createDefaultProfile')}
        </CompactButton>
      </div>
    );
  }

  if (state.stage !== "ready" || !selectedProfile) {
    const loadingMessage =
      state.stage === "loading-global"
        ? t('app.init.loadingSettings')
        : state.stage === "loading-profiles"
          ? t('app.init.loadingProfiles')
          : t('app.init.initializing');

    return (
      <div className="app-initializer-loading-container">
        <Spin size="large" />
        <div className="app-initializer-loading-message">{loadingMessage}</div>
        <div className="app-initializer-debug-info">
          {t('app.init.debugInfo', {
            stage: state.stage,
            profileCount: profiles.length,
            selected: selectedProfile ? t('common.yes') : t('common.no')
          })}
        </div>
      </div>
    );
  }

  // Ready - render children
  return <>{children}</>;
};
