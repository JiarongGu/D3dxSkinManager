import { notification } from "../../../shared/utils/notification";
import React, { useState, useEffect } from "react";
import { Form, Space, Select } from "antd";
import {
  SettingOutlined,
  ReloadOutlined,
  FolderOpenOutlined,
} from "@ant-design/icons";
import {
  CompactCard,
  CompactWarningButton,
  CompactButton,
  CompactInput,
  CompactSelect,
  CompactDangerButton,
} from "../../../shared/components/compact";
import { useTheme, ThemeMode } from "../../../shared/context/ThemeContext";
import { useTranslation } from "react-i18next";
import { changeLanguage } from "../../../i18n/i18n";
import { AVAILABLE_LANGUAGES } from "../../../shared/types/language.types";
import { logger, Logger } from "../../../shared/utils/logger";
import { settingsService } from "../services/settingsService";
import { useProfile } from "../../../shared/context/ProfileContext";
import "./SettingsView.css";
import { fileDialogService } from "../../../shared/services/systemService";

const { Option } = Select;

export const SettingsView: React.FC = () => {
  const [form] = Form.useForm();
  const { theme, setTheme } = useTheme();
  const { t, i18n } = useTranslation();
  const { selectedProfile, selectedProfileId } = useProfile();
  const [logLevel, setLogLevel] = useState<string>("info");
  const [modCacheMode, setModCacheMode] = useState<string>("internal");
  const [modCacheDirectory, setModCacheDirectory] = useState<string>("");
  const [internalModCachePath, setInternalModCachePath] = useState<string>("");
  const [profileConfigChanged, setProfileConfigChanged] =
    useState<boolean>(false);
  const [initialProfileConfig, setInitialProfileConfig] = useState<{
    mode: string;
    directory: string;
  }>({ mode: "internal", directory: "" });

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

  // Load mod cache settings from profile config
  useEffect(() => {
    const loadModCacheSettings = async () => {
      if (!selectedProfileId) {
        return;
      }

      try {
        const { getActiveProfileConfig } =
          await import("../../profiles/services/profileConfigService");
        const config = await getActiveProfileConfig(selectedProfileId);

        if (config) {
          // Use case-insensitive reading - normalize to lowercase
          const mode = config.modCache?.mode?.toLowerCase() || "internal";
          const directory = config.modCache?.directory || "";

          setModCacheMode(mode);
          setModCacheDirectory(directory);
          form.setFieldValue("modCacheMode", mode);
          form.setFieldValue("modCacheDirectory", directory);

          // Store initial config for change detection
          setInitialProfileConfig({ mode, directory });
          setProfileConfigChanged(false);

          // Calculate internal path for display (absolute path)
          if (selectedProfile?.dataDirectory) {
            const internalPath = `${selectedProfile.dataDirectory}\\work\\Mods`;
            setInternalModCachePath(internalPath);
          }
        }
      } catch (error) {
        console.error(
          "[SettingsView] Failed to load mod cache settings:",
          error,
        );
      }
    };
    loadModCacheSettings();
  }, [form, selectedProfileId, selectedProfile]);

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

  const handleResetWindowState = async () => {
    try {
      await settingsService.resetWindowState();
      notification.success(t("settings.notifications.windowStateReset"));
    } catch (error) {
      notification.error(t("settings.notifications.windowStateResetFailed"));
      console.error("[SettingsView] Failed to reset window state:", error);
    }
  };

  const handleModCacheModeChange = (value: string) => {
    setModCacheMode(value);

    // Check if config changed
    const hasChanged =
      value !== initialProfileConfig.mode ||
      modCacheDirectory !== initialProfileConfig.directory;
    setProfileConfigChanged(hasChanged);
  };

  const handleBrowseModCacheDirectory = async () => {
    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }

    try {
      const result = await fileDialogService.openFolderDialog({
        title: t("settings.profile.modCache.directory.dialogTitle"),
      });

      if (result.success && result.filePath) {
        setModCacheDirectory(result.filePath);
        form.setFieldValue("modCacheDirectory", result.filePath);

        // Check if config changed
        const hasChanged =
          modCacheMode !== initialProfileConfig.mode ||
          result.filePath !== initialProfileConfig.directory;
        setProfileConfigChanged(hasChanged);
      }
    } catch (error) {
      notification.error(t("settings.notifications.modCacheDirectoryFailed"));
      console.error("Failed to browse mod cache directory:", error);
    }
  };

  const handleModCacheDirectoryChange = (
    e: React.ChangeEvent<HTMLInputElement>,
  ) => {
    const newPath = e.target.value;
    setModCacheDirectory(newPath);

    // Check if config changed
    const hasChanged =
      modCacheMode !== initialProfileConfig.mode ||
      newPath !== initialProfileConfig.directory;
    setProfileConfigChanged(hasChanged);
  };

  const validateDirectoryExists = async (path: string): Promise<boolean> => {
    if (!path) return false;

    try {
      // Use fileDialogService or create a validation service call
      // For now, we'll just check if it's not empty
      return path.trim().length > 0;
    } catch (error) {
      return false;
    }
  };

  const handleSaveProfileConfig = async () => {
    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }

    // Validate external directory if external mode
    if (modCacheMode === "external") {
      const isValid = await validateDirectoryExists(modCacheDirectory);
      if (!isValid) {
        notification.error(
          t("settings.notifications.modCacheDirectoryInvalid"),
        );
        return;
      }
    }

    try {
      const { profileService } =
        await import("../../profiles/services/profileService");
      await profileService.updateProfileConfig({
        profileId: selectedProfileId,
        modCache: {
          mode: modCacheMode, // Already lowercase
          directory:
            modCacheMode === "external" ? modCacheDirectory : undefined,
        },
      });

      // Update initial config
      setInitialProfileConfig({
        mode: modCacheMode,
        directory: modCacheDirectory,
      });
      setProfileConfigChanged(false);

      notification.success(t("settings.notifications.profileConfigSaved"));
    } catch (error) {
      notification.error(t("settings.notifications.profileConfigSaveFailed"));
      console.error("Failed to save profile config:", error);
    }
  };

  const handleResetProfileConfig = () => {
    setModCacheMode(initialProfileConfig.mode);
    setModCacheDirectory(initialProfileConfig.directory);
    form.setFieldValue("modCacheMode", initialProfileConfig.mode);
    form.setFieldValue("modCacheDirectory", initialProfileConfig.directory);
    setProfileConfigChanged(false);
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
            modCacheMode: modCacheMode,
            modCacheDirectory: modCacheDirectory,
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
                <CompactSelect onChange={handleThemeChange}>
                  <Option value="light">{t("settings.theme.light")}</Option>
                  <Option value="dark">{t("settings.theme.dark")}</Option>
                  <Option value="auto">{t("settings.theme.auto")}</Option>
                </CompactSelect>
              </Form.Item>

              <Form.Item
                label={t("settings.global.language.label")}
                name="language"
                tooltip={t("settings.global.language.tooltip")}
              >
                <CompactSelect
                  value={i18n.language}
                  onChange={handleLanguageChange}
                >
                  {AVAILABLE_LANGUAGES.map((lang) => (
                    <Option key={lang.code} value={lang.code}>
                      {lang.name}
                    </Option>
                  ))}
                </CompactSelect>
              </Form.Item>

              <Form.Item
                label={t("settings.global.logLevel.label")}
                name="logLevel"
                tooltip={t("settings.global.logLevel.tooltip")}
              >
                <CompactSelect onChange={handleLogLevelChange}>
                  {Logger.getLevelOptions().map((option) => (
                    <Option
                      key={option.value}
                      value={option.value}
                      title={option.description}
                    >
                      {option.label}
                    </Option>
                  ))}
                </CompactSelect>
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
              label={t("settings.profile.modCache.directory.label")}
              tooltip={t("settings.profile.modCache.directory.tooltip")}
            >
              <Space.Compact style={{ width: "100%" }}>
                <CompactSelect
                  value={modCacheMode}
                  onChange={handleModCacheModeChange}
                  style={{ width: "140px" }}
                >
                  <Option value="internal">
                    {t("settings.profile.modCache.mode.internal")}
                  </Option>
                  <Option value="external">
                    {t("settings.profile.modCache.mode.external")}
                  </Option>
                </CompactSelect>
                <CompactInput
                  value={
                    modCacheMode === "internal"
                      ? internalModCachePath
                      : modCacheDirectory
                  }
                  disabled={modCacheMode === "internal"}
                  onChange={
                    modCacheMode === "external"
                      ? handleModCacheDirectoryChange
                      : undefined
                  }
                  placeholder={
                    modCacheMode === "external"
                      ? t("settings.profile.modCache.directory.placeholder")
                      : ""
                  }
                />
                {modCacheMode === "external" && (
                  <CompactButton
                    icon={<FolderOpenOutlined />}
                    onClick={handleBrowseModCacheDirectory}
                  >
                    {t("common.browse")}
                  </CompactButton>
                )}
              </Space.Compact>
            </Form.Item>

            <Form.Item>
              <Space style={{ width: "100%", justifyContent: "flex-end" }}>
                <CompactDangerButton
                  onClick={handleResetProfileConfig}
                  disabled={!profileConfigChanged}
                >
                  {t("settings.profile.discard")}
                </CompactDangerButton>
                <CompactButton
                  type="primary"
                  onClick={handleSaveProfileConfig}
                  disabled={!profileConfigChanged}
                >
                  {t("settings.profile.saveChanges")}
                </CompactButton>
              </Space>
            </Form.Item>
          </CompactCard>
        </Form>
      </div>
    </div>
  );
};
