import React, { useEffect } from "react";
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
import { useProfile } from "../../../shared/context/ProfileContext";
import { useSettingsStore } from "../store/settingsStore";
import * as settingsOps from "../operations/settingsOperations";
import { notification } from "../../../shared/utils/notification";
import "./SettingsView.css";
import { systemService } from "../../../shared/services/ipc";

const { Option } = Select;

export const SettingsView: React.FC = () => {
  const [form] = Form.useForm();
  const { theme, setTheme } = useTheme();
  const { t, i18n } = useTranslation();
  const { selectedProfile, selectedProfileId } = useProfile();

  // Zustand store - settings are preloaded by SettingsProvider
  // No need to load data here - it's already loaded and persisted
  const {
    logLevel,
    modCacheMode,
    modCacheDirectory,
    internalModCachePath,
    profileConfigChanged,
    setModCacheMode,
    setModCacheDirectory,
    resetProfileConfig,
  } = useSettingsStore();

  // Sync form fields with store state whenever they change
  // This ensures form reflects the latest store values
  useEffect(() => {
    form.setFieldsValue({
      theme: theme,
      language: i18n.language,
      logLevel: logLevel,
      modCacheMode: modCacheMode,
      modCacheDirectory: modCacheDirectory,
    });
  }, [form, theme, i18n.language, logLevel, modCacheMode, modCacheDirectory]);

  const handleLogLevelChange = async (value: string) => {
    await settingsOps.updateLogLevel(value, t);
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
    } catch (error: unknown) {
      notification.error(t("settings.notifications.languageFailed"));
      logger.error("[SettingsView] Failed to change language:", error);
    }
  };

  const handleResetWindowState = async () => {
    await settingsOps.resetWindowState(t);
  };

  const handleModCacheModeChange = (value: 'internal' | 'external') => {
    setModCacheMode(value);
  };

  const handleBrowseModCacheDirectory = async () => {
    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }

    try {
      const result = await systemService.openFolderDialog({
        title: t("settings.profile.modCache.directory.dialogTitle"),
      });

      if (result.success && result.filePath) {
        setModCacheDirectory(result.filePath);
        form.setFieldValue("modCacheDirectory", result.filePath);
      }
    } catch (error: unknown) {
      notification.error(t("settings.notifications.modCacheDirectoryFailed"));
      logger.error("[SettingsView] Failed to browse mod cache directory:", error);
    }
  };

  const handleModCacheDirectoryChange = (
    e: React.ChangeEvent<HTMLInputElement>,
  ) => {
    const newPath = e.target.value;
    setModCacheDirectory(newPath);
  };

  const handleSaveProfileConfig = async () => {
    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }

    await settingsOps.saveProfileConfig(
      selectedProfileId,
      modCacheMode,
      modCacheDirectory,
      t
    );
  };

  const handleResetProfileConfig = () => {
    resetProfileConfig();
    const { modCacheMode: mode, modCacheDirectory: dir } = useSettingsStore.getState();
    form.setFieldValue("modCacheMode", mode);
    form.setFieldValue("modCacheDirectory", dir);
  };

  return (
    <div className={"settings-view-container"}>
      <div className={"settings-view-content-wrapper"}>
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            theme: theme,
            language: i18n.language,
            logLevel: logLevel,
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
            className={"settings-view-card-margin"}
          >
            <div className={"settings-view-form-grid"}>
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
                <CompactSelect value={logLevel} onChange={handleLogLevelChange}>
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
            className={"settings-view-card-margin"}
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

            <Form.Item style={{ marginTop: "16px" }}>
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
