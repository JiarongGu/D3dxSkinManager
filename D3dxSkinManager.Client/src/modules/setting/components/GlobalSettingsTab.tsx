import React from "react";
import { Select } from "antd";
import { ReloadOutlined } from "@ant-design/icons";
import {
  CompactCard,
  CompactWarningButton,
  CompactSelect,
  CompactField,
} from "../../../shared/components/compact";
import { useTheme, ThemeMode } from "../../../shared/context/ThemeContext";
import { useTranslation } from "react-i18next";
import { AVAILABLE_LANGUAGES } from "../../../shared/types/language.types";
import { logger, Logger } from "../../../shared/utils/logger";
import { useSettingsStore } from "../store/settingsStore";
import * as settingsOps from "../operations/settingsOperations";
import { notification } from "../../../shared/utils/notification";
import { changeLanguage } from "../../../shared/services/i18n";

const { Option } = Select;

/**
 * Global (app-wide) settings. Same CompactField row style as the profile tab for consistency. These
 * controls save immediately (theme/language/logLevel), so there's no per-section Save.
 */
export const GlobalSettingsTab: React.FC = () => {
  const { theme, setTheme } = useTheme();
  const { t, i18n } = useTranslation();
  const { logLevel } = useSettingsStore();

  const handleLogLevelChange = async (value: string) => {
    await settingsOps.updateLogLevel(value, t);
  };

  const handleThemeChange = (value: ThemeMode) => {
    setTheme(value);
    const themeLabel = value === "light" ? t("settings.theme.light") : t("settings.theme.dark");
    notification.success(t("settings.notifications.themeChanged", { theme: themeLabel }));
  };

  const handleLanguageChange = async (value: string) => {
    try {
      await changeLanguage(value);
      const selectedLang = AVAILABLE_LANGUAGES.find((l) => l.code === value);
      notification.success(t("settings.notifications.languageChanged", { language: selectedLang?.name || value }));
    } catch (error: unknown) {
      notification.error(t("settings.notifications.languageFailed"));
      logger.error("[GlobalSettingsTab] Failed to change language:", error);
    }
  };

  const handleResetWindowState = async () => {
    await settingsOps.resetWindowState(t);
  };

  return (
    <CompactCard>
      <div className="settings-view-form-grid">
        <CompactField label={t("settings.global.theme.label")} description={t("settings.global.theme.tooltip")}>
          <CompactSelect value={theme} onChange={handleThemeChange}>
            <Option value="light">{t("settings.theme.light")}</Option>
            <Option value="dark">{t("settings.theme.dark")}</Option>
          </CompactSelect>
        </CompactField>

        <CompactField label={t("settings.global.language.label")} description={t("settings.global.language.tooltip")}>
          <CompactSelect value={i18n.language} onChange={handleLanguageChange}>
            {AVAILABLE_LANGUAGES.map((lang) => (
              <Option key={lang.code} value={lang.code}>{lang.name}</Option>
            ))}
          </CompactSelect>
        </CompactField>

        <CompactField label={t("settings.global.logLevel.label")} description={t("settings.global.logLevel.tooltip")}>
          <CompactSelect value={logLevel} onChange={handleLogLevelChange}>
            {Logger.getLevelOptions().map((option) => (
              <Option key={option.value} value={option.value} title={option.description}>
                {option.label}
              </Option>
            ))}
          </CompactSelect>
        </CompactField>

        <CompactField label={t("settings.global.resetWindowState")} description={t("settings.global.resetWindowStateTooltip")}>
          <CompactWarningButton icon={<ReloadOutlined />} onClick={handleResetWindowState} block>
            {t("settings.global.resetWindowState")}
          </CompactWarningButton>
        </CompactField>
      </div>
    </CompactCard>
  );
};
