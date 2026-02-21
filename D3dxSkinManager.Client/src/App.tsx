import React, { useState, useMemo, useEffect, useCallback } from 'react';
import { Layout, ConfigProvider, theme as antdTheme, App as AntdApp } from 'antd';
import { notification, setNotificationApi } from './shared/utils/notification';
import { AppHeader } from './modules/core/components/layout/AppHeader';
import { AppStatusBar, StatusType } from './modules/core/components/layout/AppStatusBar';
import { ModHierarchicalView } from './modules/mods/components/ModHierarchicalView';
import { ModsProvider } from './modules/mods/context/ModsContext';
import { LaunchView } from './modules/launch/components/LaunchView';
import { SettingsView } from './modules/settings/components/SettingsView';
import { ToolsView } from './modules/tools/components/ToolsView';
import { PluginsView } from './modules/plugins/components/PluginsView';
import { AnnotationProvider } from './shared/components/common/TooltipSystem';
import { ProfileProvider } from './shared/context/ProfileContext';
import { ThemeProvider, useTheme } from './shared/context/ThemeContext';
import { SlideInScreenProvider, useSlideInScreen } from './shared/context/SlideInScreenContext';
import { I18nInitializer } from './i18n/I18nInitializer';
import { SlideInScreenManager } from './shared/components/common/SlideInScreen';
import { AppInitializer } from './shared/components/AppInitializer';
import { OperationProvider, useOperation } from './shared/context/OperationContext';
import OperationMonitorScreen from './shared/components/operation/OperationMonitorScreen';
import { useModsContext } from './modules/mods/context/ModsContext';
import { keyboardManager, SHORTCUTS } from './modules/core/utils/KeyboardShortcutManager';
import { KeyboardShortcutsDialog } from './modules/core/components/dialogs/KeyboardShortcutsDialog';
import { AboutDialog } from './modules/core/components/dialogs/AboutDialog';
import { HelpWindow } from './modules/core/components/windows/HelpWindow';
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
  const [helpWindowVisible, setHelpWindowVisible] = useState(false);
  const [aboutDialogVisible, setAboutDialogVisible] = useState(false);

  // Status bar state
  const [statusMessage, setStatusMessage] = useState<string>('');
  const [statusType, setStatusType] = useState<StatusType>('normal');
  const [progressPercent, setProgressPercent] = useState<number>(0);
  const [progressVisible, setProgressVisible] = useState<boolean>(false);

  // Get mod data from ModsContext
  const { state, actions } = useModsContext();
  const { mods } = state;

  // Get operation data from OperationContext
  const { state: operationState } = useOperation();
  const { currentOperation, activeOperations } = operationState;

  // Get slide-in screen controls
  const { openScreen, closeScreen } = useSlideInScreen();

  // Calculate loaded mods count
  const modsLoadedCount = useMemo(() => {
    return mods.filter((mod) => mod.isLoaded).length;
  }, [mods]);

  // Derive progress from current operation
  const operationProgress = currentOperation?.percentComplete || 0;
  const operationName = currentOperation?.operationName;
  const isOperationActive = activeOperations.length > 0;

  // Status bar handlers
  const handleHelpClick = () => {
    setHelpWindowVisible(true);
  };

  const handleOperationMonitorClick = useCallback(() => {
    const screenId = openScreen({
      title: 'Operation Monitor',
      content: <OperationMonitorScreen onClose={() => closeScreen(screenId)} />,
    });
  }, [openScreen, closeScreen]);

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

    keyboardManager.register('refresh', {
      ...SHORTCUTS.REFRESH,
      callback: () => {
        actions.loadMods();
        notification.success('Refreshed successfully');
      },
    });

    keyboardManager.register('operation-monitor', {
      key: 'o',
      ctrlKey: true,
      shiftKey: true,
      description: 'Open operation monitor',
      callback: handleOperationMonitorClick,
    });

    // Start listening
    keyboardManager.start();

    return () => {
      keyboardManager.stop();
    };
  }, [actions, handleOperationMonitorClick]);

  return (
    <AnnotationProvider initialLevel="all">
      <Layout style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
        {/* Fixed Header with Tabs */}
        <AppHeader selectedTab={selectedTab} onTabChange={setSelectedTab} />

        {/* Main Content Area - Scrollable */}
        <Layout style={{ flex: 1, overflow: 'hidden' }}>
          <Content
            style={{
              padding: "0",
              margin: 0,
              background: 'var(--color-bg-container)',
              height: '100%',
              overflow: 'hidden',
            }}
          >
            {selectedTab === 'mods' && (
              <ModHierarchicalView />
            )}
            {selectedTab === 'launch' && (
              <LaunchView />
            )}
            {selectedTab === 'tools' && (
              <ToolsView onModsChanged={actions.loadMods} />
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
          modsLoaded={modsLoadedCount}
          modsTotal={mods.length}
          statusMessage={statusMessage}
          statusType={statusType}
          progressPercent={isOperationActive ? operationProgress : progressPercent}
          progressVisible={isOperationActive || progressVisible}
          operationName={operationName}
          activeOperationCount={activeOperations.length}
          onHelpClick={handleHelpClick}
          onProgressClick={handleOperationMonitorClick}
        />
      </Layout>

      {/* Keyboard Shortcuts Dialog */}
      <KeyboardShortcutsDialog
        visible={shortcutsDialogVisible}
        onClose={() => setShortcutsDialogVisible(false)}
        shortcuts={keyboardManager.getShortcuts()}
      />

      {/* Help Window */}
      <HelpWindow
        visible={helpWindowVisible}
        onClose={() => setHelpWindowVisible(false)}
      />

      {/* About Dialog */}
      <AboutDialog
        visible={aboutDialogVisible}
        onClose={() => setAboutDialogVisible(false)}
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
            <OperationProvider>
              <AppInitializer>
                <ModsProvider>
                  <AppContent />
                </ModsProvider>
              </AppInitializer>
            </OperationProvider>
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
