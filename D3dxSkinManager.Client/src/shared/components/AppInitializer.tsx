import React, { useState, useEffect } from "react";
import { Spin, Alert } from "antd";
import { CompactButton } from "./compact";
import {
  settingsService,
  GlobalSettings,
} from "../../modules/settings/services/settingsService";
import { useProfile } from "../context/ProfileContext";
import { systemService } from "../services/systemService";
import { bridgeService } from "../services/bridgeService";
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
  const [state, setState] = useState<InitState>({
    stage: "loading-global",
    globalSettings: undefined,
    error: undefined,
  });

  // Step 1: Load global settings
  useEffect(() => {
    loadGlobalSettings();
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

  const loadGlobalSettings = async () => {
    try {
      console.log("[AppInitializer] Loading global settings...");
      // Load global settings - no profileId needed
      const settings = await settingsService.getGlobalSettings();

      setState((prev) => ({
        ...prev,
        globalSettings: settings,
      }));

      console.log("[AppInitializer] Global settings loaded");
    } catch (error) {
      console.error("[AppInitializer] Failed to load global settings:", error);
      setState((prev) => ({
        ...prev,
        stage: "error",
        error: t('app.init.loadGlobalSettingsFailed'),
      }));
    }
  };

  const selectInitialProfile = async () => {
    try {
      console.log(
        "[AppInitializer] Selecting initial profile from",
        profiles.length,
        "profiles",
      );

      // Try to find the first profile or default profile
      let profileToSelect = profiles.find((p) => p.isActive) || profiles[0];

      if (profileToSelect) {
        console.log(
          "[AppInitializer] Found profile to select:",
          profileToSelect.name,
          profileToSelect.id,
        );
        await actions.selectProfile(profileToSelect.id);
        console.log("[AppInitializer] Profile selected successfully");
      } else {
        // No profiles exist - need to create one
        console.log(
          "[AppInitializer] No profiles found, showing create dialog",
        );
        setState((prev) => ({
          ...prev,
          stage: "selecting-profile",
        }));
      }
    } catch (error) {
      console.error(
        "[AppInitializer] Failed to select initial profile:",
        error,
      );
      setState((prev) => ({
        ...prev,
        stage: "error",
        error: t('app.init.selectProfileFailed'),
      }));
    }
  };

  const handleProfileCreate = async (name: string, description?: string) => {
    try {
      console.log("[AppInitializer] Creating new profile:", name);
      const profile = await actions.createProfile(name, description);
      console.log("[AppInitializer] Profile created:", profile.id);
      await actions.selectProfile(profile.id);
      console.log("[AppInitializer] New profile selected");
      setState((prev) => ({ ...prev, stage: "ready" }));
    } catch (error) {
      console.error("[AppInitializer] Failed to create profile:", error);
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
          onClick={() => handleProfileCreate("Default", "Default profile")}
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
