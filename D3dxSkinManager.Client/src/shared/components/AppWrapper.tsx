import { ProfileProvider } from "../context/ProfileContext";
import { SettingsProvider } from "../../modules/setting/SettingsProvider";
import { ThemeProvider, useTheme } from "../context/ThemeContext";
import { I18nInitializer } from "../../i18n/I18nInitializer";
import { App, ConfigProvider, theme } from "antd";
import { NotificationInitializer } from "./NotificationInitializer";
import "../../styles/visual-enhancements.css";
import "../../styles/theme-colors.css";
import "../../styles/custom-notification.css";

const AppWrapperInner: React.FC<{
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
      <I18nInitializer>
        <App notification={{ maxCount: 1, stack: false }}>
          <NotificationInitializer>{children}</NotificationInitializer>
        </App>
      </I18nInitializer>
    </ConfigProvider>
  );
};

export const AppWrapper: React.FC<{
  children: React.ReactNode;
}> = ({ children }) => {
  return (
    <ProfileProvider>
      <SettingsProvider>
        <I18nInitializer>
          <ThemeProvider>
            <AppWrapperInner>{children}</AppWrapperInner>
          </ThemeProvider>
        </I18nInitializer>
      </SettingsProvider>
    </ProfileProvider>
  );
};
