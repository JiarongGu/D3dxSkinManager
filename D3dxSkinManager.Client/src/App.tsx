import React, { useState, useEffect, useCallback, Suspense, lazy } from "react";
import { useTranslation } from "react-i18next";
import { Layout, Spin } from "antd";
import { registerNavigateToTab } from "./shared/hooks/useAppNavigation";
import { initProcessBridge } from "./shared/store/processBridge";
import { AppHeader } from "./modules/core/components/layout/AppHeader";
import { AppStatusBar } from "./modules/core/components/layout/AppStatusBar";
// Eager: the default ("mods") tab + always-present chrome. Everything else is code-split below so it
// stays OUT of the initial bundle — the mods tab is the first paint, the rest loads on demand.
import { ModHierarchicalView } from "./modules/mod/components/ModHierarchicalView";
import { AnnotationProvider } from "./shared/context/AnnotationContext";
import {
  SlideInScreenProvider,
  useSlideInScreenContext,
} from "./shared/context/SlideInScreenContext";
import { SlideInScreenManager } from "./shared/components/common/SlideInScreen";
import { AppLoader } from "./shared/components/AppLoader";
import {
  keyboardManager,
  SHORTCUTS,
} from "./modules/core/utils/KeyboardShortcutManager";
import { ModProvider } from "./modules/mod";
import { AppWrapper } from "./shared/components/AppWrapper";
import { ErrorBoundary } from "./shared/components/ErrorBoundary";
import { ONBOARDING_DONE_KEY } from "./modules/core/components/onboarding/onboardingConstants";
import { settingsService, systemService, UpdateInfo } from "./shared/services/ipc";
import { logger } from "./shared/utils/logger";

// Lazy — non-default tabs + on-demand dialogs. Each becomes its own chunk loaded only when first
// shown, so the initial mount parses far less JS (faster first paint).
const SettingsView = lazy(() =>
  import("./modules/setting/components/SettingsView").then((m) => ({ default: m.SettingsView })),
);
const ToolsView = lazy(() =>
  import("./modules/tool/components/ToolsView").then((m) => ({ default: m.ToolsView })),
);
const RemoteLibraryView = lazy(() =>
  import("./modules/remote/components/RemoteLibraryView").then((m) => ({
    default: m.RemoteLibraryView,
  })),
);
const HelpWindow = lazy(() =>
  import("./modules/help").then((m) => ({ default: m.HelpWindow })),
);
const KeyboardShortcutsDialog = lazy(() =>
  import("./modules/core/components/dialogs/KeyboardShortcutsDialog").then((m) => ({
    default: m.KeyboardShortcutsDialog,
  })),
);
const OnboardingWizard = lazy(() =>
  import("./modules/core/components/onboarding/OnboardingWizard").then((m) => ({
    default: m.OnboardingWizard,
  })),
);
const UpdateDialog = lazy(() =>
  import("./modules/setting/components/UpdateDialog").then((m) => ({ default: m.UpdateDialog })),
);

import "./App.css";

const { Content } = Layout;

/**
 * Main app content component
 * Uses ProfileContext to access selected profile
 */
const AppContent: React.FC = () => {
  const { t } = useTranslation();
  const [selectedTab, setSelectedTab] = useState("mods");
  const [shortcutsDialogVisible, setShortcutsDialogVisible] = useState(false);
  const [showOnboarding, setShowOnboarding] = useState(false);
  const [updateInfo, setUpdateInfo] = useState<UpdateInfo | undefined>(undefined);

  // Get slide-in screen controls
  const { openScreen, closeScreen, closeAllScreens, screens } =
    useSlideInScreenContext();

  // Handle tab change - close all slide-in screens
  const handleTabChange = useCallback(
    (tab: string) => {
      closeAllScreens();
      setSelectedTab(tab);
    },
    [closeAllScreens],
  );

  // Status bar handlers - toggle help window
  const handleHelpClick = () => {
    // Check if help screen is already open
    const helpScreen = screens.find((s) => s.title === "Help & Documentation");

    if (helpScreen) {
      // Help is open, close it
      closeScreen(helpScreen.id);
    } else {
      // Help is closed, open it
      openScreen({
        title: "Help & Documentation",
        content: <HelpWindow />,
        width: "900px",
      });
    }
  };

  // Register tab navigation so tools/screens can switch tabs
  useEffect(() => registerNavigateToTab(handleTabChange), [handleTabChange]);

  // Global bridge: backend ProcessRegistry snapshot → process store (status bar + Activity panel)
  useEffect(() => initProcessBridge(), []);

  // First run: AppContent only mounts once a profile is ready (AppLoader gate), so a missing localStorage
  // flag here means a brand-new install → show the onboarding wizard once.
  useEffect(() => {
    let isFirstRun = false;
    try {
      isFirstRun = !localStorage.getItem(ONBOARDING_DONE_KEY);
    } catch {
      // localStorage unavailable — skip onboarding rather than risk a loop.
    }
    if (isFirstRun) setShowOnboarding(true);

    // DEV: re-open the wizard for pure-UI testing (see desktop-app-testing.md).
    if (import.meta.env.DEV) {
      (window as unknown as { __openOnboarding?: () => void }).__openOnboarding = () =>
        setShowOnboarding(true);
      // DEV: preview the "update available" dialog (no newer release exists to trigger it live).
      (window as unknown as { __showUpdateAvailable?: () => void }).__showUpdateAvailable = () =>
        setUpdateInfo({
          currentVersion: "2.4",
          latestVersion: "2.5",
          updateAvailable: true,
          releaseName: "D3dxSkinManager v2.5",
          releaseNotes:
            "### New Features\n\n- App self-update with a nice update screen\n- Auto-update toggle in settings (off by default)\n\n### Bug Fixes\n\n- Various stability improvements",
          releaseUrl: "https://github.com/JiarongGu/D3dxSkinManager/releases/tag/v2.5",
          publishedAt: "2026-06-19T00:00:00Z",
          hasManifest: true,
          changedFileCount: 3,
          downloadSize: 14694798,
        });
    }
  }, []);

  // Startup auto-update check — only when the user opted in (setting defaults OFF). Silent on failure
  // (no network at startup shouldn't nag); shows the update dialog only if a newer version exists.
  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const settings = await settingsService.getGlobalSettings();
        if (!settings?.autoUpdateCheck) return;
        const info = await systemService.checkForUpdate();
        if (!cancelled && info.updateAvailable) setUpdateInfo(info);
      } catch (error: unknown) {
        logger.warn("[App] Startup update check skipped:", error);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // Initialize keyboard shortcuts
  useEffect(() => {
    // Register global shortcuts
    keyboardManager.register("help", {
      ...SHORTCUTS.CANCEL,
      key: "?",
      shiftKey: true,
      description: t('shortcuts.showShortcuts'),
      callback: () => setShortcutsDialogVisible(true),
    });

    keyboardManager.register("help-alt", {
      key: "/",
      ctrlKey: true,
      description: t('shortcuts.showShortcuts'),
      callback: () => setShortcutsDialogVisible(true),
    });

    // Start listening
    keyboardManager.start();

    return () => {
      keyboardManager.stop();
    };
  }, []);

  return (
    <AnnotationProvider initialLevel="all">
      <Layout className="app-main-layout">
        {/* Fixed Header with Tabs */}
        <AppHeader selectedTab={selectedTab} onTabChange={handleTabChange} />

        {/* Main Content Area - Scrollable */}
        <Layout className="app-content-layout">
          <Content className="app-content">
            {/* Each tab wrapped in its own boundary so a crash in one tab degrades locally, not app-wide.
                Suspense covers the lazy (non-default) tabs while their chunk loads on first open. */}
            <Suspense fallback={<div style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100%" }}><Spin /></div>}>
              {selectedTab === "mods" && <ErrorBoundary compact label="Mods"><ModHierarchicalView /></ErrorBoundary>}
              {selectedTab === "remote" && <ErrorBoundary compact label="Remote"><RemoteLibraryView /></ErrorBoundary>}
              {selectedTab === "tools" && <ErrorBoundary compact label="Tools"><ToolsView /></ErrorBoundary>}
              {selectedTab === "settings" && <ErrorBoundary compact label="Settings"><SettingsView /></ErrorBoundary>}
            </Suspense>
          </Content>
        </Layout>

        {/* Fixed Footer */}
        <AppStatusBar onHelpClick={handleHelpClick} />
      </Layout>

      {/* Lazy on-demand UI — each loads its chunk only when opened. SlideInScreenManager is here too
          because it renders the lazy HelpWindow as screen content. fallback=null: nothing shows until
          the user actually triggers one. */}
      <Suspense fallback={null}>
        {/* Keyboard Shortcuts Dialog */}
        <KeyboardShortcutsDialog
          visible={shortcutsDialogVisible}
          onClose={() => setShortcutsDialogVisible(false)}
          shortcuts={keyboardManager.getShortcuts()}
        />

        {/* Slide-in Screen Manager */}
        <SlideInScreenManager />

        {/* First-run onboarding (shown once; reopenable in DEV via window.__openOnboarding) */}
        <OnboardingWizard open={showOnboarding} onClose={() => setShowOnboarding(false)} />

        {/* Startup auto-update prompt (only when a newer version was found) */}
        <UpdateDialog
          open={!!updateInfo}
          prefetched={updateInfo}
          onClose={() => setUpdateInfo(undefined)}
        />
      </Suspense>
    </AnnotationProvider>
  );
};

export const App: React.FC = () => {
  return (
    <AppWrapper>
      <ModProvider>
        <SlideInScreenProvider>
          <AppContent />
        </SlideInScreenProvider>
      </ModProvider>
    </AppWrapper>
  );
};

export default App;
