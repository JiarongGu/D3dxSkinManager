import React, { useState, useMemo, useEffect } from 'react';
import { Layout, message, ConfigProvider, theme as antdTheme, App as AntdApp } from 'antd';
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
import { SlideInScreenProvider } from './shared/context/SlideInScreenContext';
import { SlideInScreenManager } from './shared/components/common/SlideInScreen';
import { AppInitializer } from './shared/components/AppInitializer';
import { useModData } from './modules/core/hooks/useModData';
import { useModFilters } from './modules/core/hooks/useModFilters';
import { useModActions } from './modules/core/hooks/useModActions';
import { keyboardManager, SHORTCUTS } from './modules/core/utils/KeyboardShortcutManager';
import { KeyboardShortcutsDialog } from './modules/core/components/dialogs/KeyboardShortcutsDialog';
import { AboutDialog } from './modules/core/components/dialogs/AboutDialog';
import { HelpWindow } from './modules/core/components/windows/HelpWindow';
import './App.css';
import './styles/visual-enhancements.css';
import './styles/theme-colors.css';

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

  // Load mod data and filters
  const { mods, loading, loadMods, loadFilters } = useModData();

  // Calculate loaded mods count
  const modsLoadedCount = useMemo(() => {
    return mods.filter(mod => mod.isLoaded).length;
  }, [mods]);

  // Status bar handlers
  const handleHelpClick = () => {
    setHelpWindowVisible(true);
  };

  const handleSuggestionsClick = () => {
    message.info('Optimization suggestions coming soon!');
    // TODO: Open suggestions window
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

    keyboardManager.register('refresh', {
      ...SHORTCUTS.REFRESH,
      callback: () => {
        loadMods();
        message.success('Refreshed');
      },
    });

    // Start listening
    keyboardManager.start();

    return () => {
      keyboardManager.stop();
    };
  }, [loadMods]);

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
              <ModsProvider>
                <ModHierarchicalView />
              </ModsProvider>
            )}
            {selectedTab === 'launch' && (
              <LaunchView />
            )}
            {selectedTab === 'tools' && (
              <ToolsView onModsChanged={loadMods} />
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
          userName="User"
          serverStatus="connected"
          modsLoaded={modsLoadedCount}
          modsTotal={mods.length}
          statusMessage={statusMessage}
          statusType={statusType}
          progressPercent={progressPercent}
          progressVisible={progressVisible}
          onHelpClick={handleHelpClick}
          onSuggestionsClick={handleSuggestionsClick}
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
      <AntdApp>
        <ProfileProvider>
          <AppInitializer>
            <AppContent />
          </AppInitializer>
        </ProfileProvider>
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
      <SlideInScreenProvider>
        <App />
      </SlideInScreenProvider>
    </ThemeProvider>
  );
};

export default AppWithProviders;
