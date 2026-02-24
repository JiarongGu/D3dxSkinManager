import { notification } from "../../../shared/utils/notification";
import React, { useState, useEffect } from "react";
import { Form, Select } from "antd";
import {
  SettingOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import { CompactCard, CompactWarningButton } from "../../../shared/components/compact";
import { useTheme, ThemeMode } from "../../../shared/context/ThemeContext";
import { useTranslation } from "react-i18next";
import { changeLanguage } from "../../../i18n/i18n";
import { AVAILABLE_LANGUAGES } from "../../../shared/types/language.types";
import { logger, Logger } from "../../../shared/utils/logger";
import { settingsService } from "../services/settingsService";
import { useProfile } from "../../../shared/context/ProfileContext";
import  "./SettingsView.css";

const { Option } = Select;

export const SettingsView: React.FC = () => {
  const [form] = Form.useForm();
  const { theme, setTheme } = useTheme();
  const { t, i18n } = useTranslation();
  const { selectedProfile, selectedProfileId } = useProfile();
  const [logLevel, setLogLevel] = useState<string>("info");
  const [thumbnailAlgorithm, setThumbnailAlgorithm] = useState<string>(
    "similarity-threshold",
  );

  // Load log level from backend on mount
  useEffect(() => {
    const loadLogLevel = async () => {
      try {
        const settings = await settingsService.getGlobalSettings();
        if (settings?.logLevel) {
          setLogLevel(settings.logLevel);
          form.setFieldValue("logLevel", settings.logLevel);
        } else {
          // Fallback to current logger level
          const currentLevel = logger.getCurrentLevelName();
          setLogLevel(currentLevel);
          form.setFieldValue("logLevel", currentLevel);
        }
      } catch (error) {
        console.error("[SettingsView] Failed to load log level:", error);
        // Fallback to current logger level
        const currentLevel = logger.getCurrentLevelName();
        setLogLevel(currentLevel);
        form.setFieldValue("logLevel", currentLevel);
      }
    };
    loadLogLevel();
  }, [form]);

  // Load thumbnail algorithm from profile config
  useEffect(() => {
    const loadThumbnailAlgorithm = async () => {
      if (!selectedProfileId) {
        return;
      }

      try {
        const { getActiveProfileConfig } =
          await import("../../profiles/services/profileConfigService");
        const config = await getActiveProfileConfig(selectedProfileId);
        if (config?.thumbnailAlgorithm) {
          setThumbnailAlgorithm(config.thumbnailAlgorithm);
          form.setFieldValue("thumbnailAlgorithm", config.thumbnailAlgorithm);
        }
      } catch (error) {
        console.error(
          "[SettingsView] Failed to load thumbnail algorithm:",
          error,
        );
      }
    };
    loadThumbnailAlgorithm();
  }, [form, selectedProfileId]);

  // Initialize form with theme and language
  useEffect(() => {
    form.setFieldsValue({
      theme: theme,
      language: i18n.language,
    });
  }, [theme, i18n.language, form]);

  const handleLogLevelChange = async (value: string) => {
    setLogLevel(value);

    // Save to backend
    try {
      await settingsService.updateGlobalSetting("logLevel", value);
      notification.success(
        t("settings.notifications.logLevelChanged", { level: value }),
      );
    } catch (error) {
      notification.error(t("settings.notifications.logLevelFailed"));
      console.error("[SettingsView] Failed to save log level:", error);
    }
  };

  const handleThemeChange = (value: ThemeMode) => {
    setTheme(value);
    const themeLabel =
      value === "auto"
        ? t("settings.theme.auto")
        : value === "light"
          ? t("settings.theme.light")
          : t("settings.theme.dark");
    notification.success(
      t("settings.notifications.themeChanged", { theme: themeLabel }),
    );
  };

  const handleLanguageChange = async (value: string) => {
    try {
      await changeLanguage(value);
      const selectedLang = AVAILABLE_LANGUAGES.find((l) => l.code === value);
      notification.success(
        t("settings.notifications.languageChanged", {
          language: selectedLang?.name || value,
        }),
      );
    } catch (error) {
      notification.error(t("settings.notifications.languageFailed"));
      console.error("[SettingsView] Failed to change language:", error);
    }
  };

  const handleThumbnailAlgorithmChange = async (value: string) => {
    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }

    try {
      // Import the service dynamically to avoid circular dependencies
      const { updateActiveProfileConfigField } =
        await import("../../profiles/services/profileConfigService");
      await updateActiveProfileConfigField(
        selectedProfileId,
        "thumbnailAlgorithm",
        value,
      );
      notification.success(
        t("settings.notifications.thumbnailAlgorithmUpdated"),
      );
    } catch (error) {
      notification.error(t("settings.notifications.thumbnailAlgorithmFailed"));
      console.error("Failed to update thumbnail algorithm:", error);
    }
  };

  const handleResetWindowState = async () => {
    try {
      await settingsService.resetWindowState();
      notification.success(t("settings.notifications.windowStateReset"));
    } catch (error) {
      notification.error(t("settings.notifications.windowStateResetFailed"));
      console.error("[SettingsView] Failed to reset window state:", error);
    }
  };

  return (
    <div className={"container"}>
      <div className={"content-wrapper"}>
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            theme: theme,
            language: i18n.language,
            thumbnailAlgorithm: thumbnailAlgorithm,
            migotoVersion: "3dmigoto",
          }}
        >
          <CompactCard
            title={
              <>
                <SettingOutlined /> {t("settings.global.title")}
              </>
            }
            className={"card-margin"}
          >
            <div className={"form-grid"}>
              <Form.Item
                label={t("settings.global.theme.label")}
                name="theme"
                tooltip={t("settings.global.theme.tooltip")}
              >
                <Select onChange={handleThemeChange}>
                  <Option value="light">{t("settings.theme.light")}</Option>
                  <Option value="dark">{t("settings.theme.dark")}</Option>
                  <Option value="auto">{t("settings.theme.auto")}</Option>
                </Select>
              </Form.Item>

              <Form.Item
                label={t("settings.global.language.label")}
                name="language"
                tooltip={t("settings.global.language.tooltip")}
              >
                <Select value={i18n.language} onChange={handleLanguageChange}>
                  {AVAILABLE_LANGUAGES.map((lang) => (
                    <Option key={lang.code} value={lang.code}>
                      {lang.name}
                    </Option>
                  ))}
                </Select>
              </Form.Item>

              <Form.Item
                label={t("settings.global.logLevel.label")}
                name="logLevel"
                tooltip={t("settings.global.logLevel.tooltip")}
              >
                <Select onChange={handleLogLevelChange}>
                  {Logger.getLevelOptions().map((option) => (
                    <Option
                      key={option.value}
                      value={option.value}
                      title={option.description}
                    >
                      {option.label}
                    </Option>
                  ))}
                </Select>
              </Form.Item>

              <Form.Item
                label={t("settings.global.resetWindowState")}
                tooltip={t("settings.global.resetWindowStateTooltip")}
              >
                <CompactWarningButton
                  icon={<ReloadOutlined />}
                  onClick={handleResetWindowState}
                  block
                >
                  {t("settings.global.resetWindowState")}
                </CompactWarningButton>
              </Form.Item>
            </div>
          </CompactCard>

          <CompactCard
            title={t("settings.profile.title")}
            className={"card-margin"}
          >
            <Form.Item
              label={t("settings.profile.thumbnailAlgorithm.label")}
              name="thumbnailAlgorithm"
              tooltip={t("settings.profile.thumbnailAlgorithm.tooltip")}
            >
              <Select onChange={handleThumbnailAlgorithmChange}>
                <Option value="key-in-only">
                  {t("settings.profile.thumbnailAlgorithm.keyInOnly")}
                </Option>
                <Option value="similarity-only">
                  {t("settings.profile.thumbnailAlgorithm.similarityOnly")}
                </Option>
                <Option value="similarity-threshold">
                  {t("settings.profile.thumbnailAlgorithm.similarityThreshold")}
                </Option>
                <Option value="similarity-keyin">
                  {t("settings.profile.thumbnailAlgorithm.similarityKeyin")}
                </Option>
              </Select>
            </Form.Item>
          </CompactCard>
        </Form>
      </div>
    </div>
  );
};
