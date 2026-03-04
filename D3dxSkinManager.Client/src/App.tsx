import React, { useState, useEffect, useCallback } from 'react';
import { Layout, ConfigProvider, theme as antdTheme, App as AntdApp } from 'antd';
import { setNotificationApi } from './shared/utils/notification';
import { AppHeader } from './modules/core/components/layout/AppHeader';
import { AppStatusBar, StatusType } from './modules/core/components/layout/AppStatusBar';
import { ModHierarchicalView } from './modules/mod/components/ModHierarchicalView';
import { ModProvider } from './modules/mod';
import { LaunchView } from './modules/launch/components/LaunchView';
import { SettingsView } from './modules/setting/components/SettingsView';
import { ToolsView } from './modules/tool/components/ToolsView';
import { PluginsView } from './modules/plugin/components/PluginsView';
import { AnnotationProvider } from './shared/components/common/TooltipSystem';
import { ProfileProvider } from './shared/context/ProfileContext';
import { ThemeProvider, useTheme } from './shared/context/ThemeContext';
import { SlideInScreenProvider, useSlideInScreenContext } from './shared/context/SlideInScreenContext';
import { I18nInitializer } from './i18n/I18nInitializer';
import { SlideInScreenManager } from './shared/components/common/SlideInScreen';
import { AppInitializer } from './shared/components/AppInitializer';
import { keyboardManager, SHORTCUTS } from './modules/core/utils/KeyboardShortcutManager';
import { KeyboardShortcutsDialog } from './modules/core/components/dialogs/KeyboardShortcutsDialog';
import { HelpWindow } from './modules/help';
import './App.css';
import './styles/visual-enhancements.css';
import './styles/theme-colors.css';
import './styles/custom-notification.css';

const { Content } = Layout;

/**
 * Main app content component
 * Uses ProfileContext to access selected profile
 */
const AppContent: React.FC = () => {
  const [selectedTab, setSelectedTab] = useState('mods');
  const [shortcutsDialogVisible, setShortcutsDialogVisible] = useState(false);

  // Status bar state
  const [statusMessage, setStatusMessage] = useState<string>('');
  const [statusType, setStatusType] = useState<StatusType>('normal');
  const [progressPercent, setProgressPercent] = useState<number>(0);
  const [progressVisible, setProgressVisible] = useState<boolean>(false);

  // Get slide-in screen controls
  const { openScreen, closeScreen, closeAllScreens, screens } = useSlideInScreenContext();

  // Handle tab change - close all slide-in screens
  const handleTabChange = useCallback((tab: string) => {
    closeAllScreens();
    setSelectedTab(tab);
  }, [closeAllScreens]);

  // Status bar handlers - toggle help window
  const handleHelpClick = () => {
    // Check if help screen is already open
    const helpScreen = screens.find(s => s.title === 'Help & Documentation');

    if (helpScreen) {
      // Help is open, close it
      closeScreen(helpScreen.id);
    } else {
      // Help is closed, open it
      openScreen({
        title: 'Help & Documentation',
        content: <HelpWindow />,
        width: '900px',
      });
    }
  };

  // Initialize keyboard shortcuts
  useEffect(() => {
    // Register global shortcuts
    keyboardManager.register('help', {
      ...SHORTCUTS.CANCEL,
      key: '?',
      shiftKey: true,
      description: 'Show keyboard shortcuts',
      callback: () => setShortcutsDialogVisible(true),
    });

    keyboardManager.register('help-alt', {
      key: '/',
      ctrlKey: true,
      description: 'Show keyboard shortcuts',
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
            {selectedTab === 'mods' && (
              <ModHierarchicalView />
            )}
            {selectedTab === 'launch' && (
              <LaunchView />
            )}
            {selectedTab === 'tools' && (
              <ToolsView />
            )}
            {selectedTab === 'plugins' && (
              <PluginsView />
            )}
            {selectedTab === 'settings' && (
              <SettingsView />
            )}
          </Content>
        </Layout>

        {/* Fixed Footer */}
        <AppStatusBar
          serverStatus="connected"
          statusMessage={statusMessage}
          statusType={statusType}
          progressPercent={progressPercent}
          progressVisible={progressVisible}
          onHelpClick={handleHelpClick}
        />
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

/**
 * Component to initialize notification API from AntdApp context
 */
const NotificationInitializer: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { notification: notificationApi } = AntdApp.useApp();

  useEffect(() => {
    // Initialize the notification API singleton
    setNotificationApi(notificationApi);
  }, [notificationApi]);

  return <>{children}</>;
};

/**
 * App wrapper with theme and config providers
 * ProfileProvider wraps everything and manages profile state
 */
const App: React.FC = () => {
  const { effectiveTheme } = useTheme();

  return (
    <ConfigProvider
      theme={{
        algorithm: effectiveTheme === 'dark' ? antdTheme.darkAlgorithm : antdTheme.defaultAlgorithm,
      }}
      componentSize="middle"
    >
      <AntdApp notification={{ maxCount: 1, stack: false }}>
        <NotificationInitializer>
          <ProfileProvider>
            <AppInitializer>
              <ModProvider>
                <AppContent />
              </ModProvider>
            </AppInitializer>
          </ProfileProvider>
        </NotificationInitializer>
      </AntdApp>
    </ConfigProvider>
  );
};

/**
 * Root app component with all providers
 */
const AppWithProviders: React.FC = () => {
  return (
    <ThemeProvider>
      <I18nInitializer>
        <SlideInScreenProvider>
          <App />
        </SlideInScreenProvider>
      </I18nInitializer>
    </ThemeProvider>
  );
};

export default AppWithProviders;
