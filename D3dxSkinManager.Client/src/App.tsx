import React, { useState, useEffect, useCallback } from "react";
import {
  Layout,
  ConfigProvider,
  theme as antdTheme,
  App as AntdApp,
} from "antd";
import { AppHeader } from "./modules/core/components/layout/AppHeader";
import { AppStatusBar } from "./modules/core/components/layout/AppStatusBar";
import { ModHierarchicalView } from "./modules/mod/components/ModHierarchicalView";
import { SettingsView } from "./modules/setting/components/SettingsView";
import { ToolsView } from "./modules/tool/components/ToolsView";
import { PluginsView } from "./modules/plugin/components/PluginsView";
import { AnnotationProvider } from "./shared/components/common/TooltipSystem";
import { ThemeProvider, useTheme } from "./shared/context/ThemeContext";
import {
  SlideInScreenProvider,
  useSlideInScreenContext,
} from "./shared/context/SlideInScreenContext";
import { I18nInitializer } from "./i18n/I18nInitializer";
import { SlideInScreenManager } from "./shared/components/common/SlideInScreen";
import { AppInitializer } from "./shared/components/AppInitializer";
import {
  keyboardManager,
  SHORTCUTS,
} from "./modules/core/utils/KeyboardShortcutManager";
import { KeyboardShortcutsDialog } from "./modules/core/components/dialogs/KeyboardShortcutsDialog";
import { HelpWindow } from "./modules/help";
import { SettingsProvider } from "./modules/setting";
import { ProfileProvider } from "./shared/context/ProfileContext";
import { ModProvider } from "./modules/mod";
import { NotificationInitializer } from "./shared/components/NotificationInitializer";
import "./App.css";
import "./styles/visual-enhancements.css";
import "./styles/theme-colors.css";
import "./styles/custom-notification.css";
import { AppWrapper } from "./shared/components/AppWrapper";

const { Content } = Layout;

/**
 * Main app content component
 * Uses ProfileContext to access selected profile
 */
const AppContent: React.FC = () => {
  const [selectedTab, setSelectedTab] = useState("mods");
  const [shortcutsDialogVisible, setShortcutsDialogVisible] = useState(false);

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

  // Initialize keyboard shortcuts
  useEffect(() => {
    // Register global shortcuts
    keyboardManager.register("help", {
      ...SHORTCUTS.CANCEL,
      key: "?",
      shiftKey: true,
      description: "Show keyboard shortcuts",
      callback: () => setShortcutsDialogVisible(true),
    });

    keyboardManager.register("help-alt", {
      key: "/",
      ctrlKey: true,
      description: "Show keyboard shortcuts",
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
            {selectedTab === "mods" && <ModHierarchicalView />}
            {/* {selectedTab === "launch" && <LaunchView />} //TODO: disabled for now until its implemented*/}
            {selectedTab === "tools" && <ToolsView />}
            {selectedTab === "plugins" && <PluginsView />}
            {selectedTab === "settings" && <SettingsView />}
          </Content>
        </Layout>

        {/* Fixed Footer */}
        <AppStatusBar onHelpClick={handleHelpClick} />
      </Layout>

      {/* Keyboard Shortcuts Dialog */}
      <KeyboardShortcutsDialog
        visible={shortcutsDialogVisible}
        onClose={() => setShortcutsDialogVisible(false)}
        shortcuts={keyboardManager.getShortcuts()}
      />

      {/* Slide-in Screen Manager */}
      <SlideInScreenManager />
    </AnnotationProvider>
  );
};

export const App: React.FC = () => {
  return (
    <AppWrapper>
      <ModProvider>
        <SlideInScreenProvider>
          <AppInitializer>
            <AppContent />
          </AppInitializer>
        </SlideInScreenProvider>
      </ModProvider>
    </AppWrapper>
  );
};

export default App;
