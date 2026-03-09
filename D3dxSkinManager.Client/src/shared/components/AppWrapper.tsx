import { App, ConfigProvider, theme } from "antd";
import { useEffect } from "react";

import { ProfileProvider, useProfile } from "../context/ProfileContext";
import { SettingsProvider } from "../context/SettingsProvider";
import { ThemeProvider, useTheme } from "../context/ThemeContext";
import { I18Provider } from "../context/I18Provider";
import { AppLoader } from "./AppLoader";
import { setNotificationApi } from "../utils/notification";

import "../../styles/visual-enhancements.css";
import "../../styles/theme-colors.css";
import "../../styles/custom-notification.css";

/**
 * Component to initialize notification API from AntdApp context
 * This ensures notifications use the correct theme from ConfigProvider
 */
export const NotificationInitializer: React.FC<{
  children: React.ReactNode;
}> = ({ children }) => {
  const { notification: notificationApi } = App.useApp();

  useEffect(() => {
    // Initialize the notification API singleton
    setNotificationApi(notificationApi);
  }, [notificationApi]);

  return <>{children}</>;
};

const AppWrapperSettingsInner: React.FC<{
  children: React.ReactNode;
}> = ({ children }) => {
  const { effectiveTheme } = useTheme();

  return (
    <ConfigProvider
      theme={{
        algorithm:
          effectiveTheme === "dark"
            ? theme.darkAlgorithm
            : theme.defaultAlgorithm,
      }}
      componentSize="middle"
    >
      <App notification={{ maxCount: 1, stack: false }}>
        <NotificationInitializer>
          <AppLoader>{children}</AppLoader>
        </NotificationInitializer>
      </App>
    </ConfigProvider>
  );
};

export const AppWrapper: React.FC<{
  children: React.ReactNode;
}> = ({ children }) => {
  return (
    <ProfileProvider>
      <SettingsProvider>
        <I18Provider>
          <ThemeProvider>
            <AppWrapperSettingsInner>{children}</AppWrapperSettingsInner>
          </ThemeProvider>
        </I18Provider>
      </SettingsProvider>
    </ProfileProvider>
  );
};
