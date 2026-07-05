import React, { useState } from "react";
import { Select } from "antd";
import { ThunderboltOutlined } from "@ant-design/icons";
import {
  CompactCard,
  CompactSelect,
  CompactField,
} from "../../../shared/components/compact";
import { useTranslation } from "react-i18next";
import { useProfile } from "../../../shared/context/ProfileContext";
import { useSettingsStore } from "../store/settingsStore";
import { handleError } from "../../../shared/utils/errorHandler";
import { notification } from "../../../shared/utils/notification";
import { profileService } from "../../../shared/services/ipc";
import { SettingsSectionActions } from "./SettingsSectionActions";

const { Option } = Select;

/**
 * Mod Import settings tab: archive compression used when importing mods into the library.
 * Store-controlled; the card owns its Save/Reset gated on its own dirty state.
 */
export const ModImportSettingsTab: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const {
    compressionType,
    compressionMode,
    initialModImportConfig,
    setCompressionType,
    setCompressionMode,
    setInitialModImportConfig,
  } = useSettingsStore();

  const [savingImport, setSavingImport] = useState(false);

  const importDirty =
    compressionType !== initialModImportConfig.compressionType ||
    compressionMode !== initialModImportConfig.compressionMode;

  const handleSaveImport = async () => {
    if (!selectedProfileId) { notification.error(t("errors.noProfileSelected")); return; }
    setSavingImport(true);
    try {
      await profileService.updateProfileConfig({ profileId: selectedProfileId, compressionType, compressionMode });
      setInitialModImportConfig({ compressionType, compressionMode });
      notification.success(t("settings.notifications.profileConfigSaved"));
    } catch (error) {
      handleError(error);
    } finally {
      setSavingImport(false);
    }
  };

  const handleResetImport = () => {
    setCompressionType(initialModImportConfig.compressionType);
    setCompressionMode(initialModImportConfig.compressionMode);
  };

  return (
    <div className="settings-view-profile">
      <CompactCard
        title={<><ThunderboltOutlined /> {t("settings.profile.modImport.title")}</>}
        extra={<SettingsSectionActions dirty={importDirty} saving={savingImport} onSave={handleSaveImport} onReset={handleResetImport} />}
      >
        <div className="settings-view-form-grid">
          <CompactField
            label={t("settings.profile.modImport.compressionType.label")}
            description={t("settings.profile.modImport.compressionType.tooltip")}
          >
            <CompactSelect value={compressionType} onChange={setCompressionType}>
              <Option value="7z">{t("settings.profile.modImport.compressionType.7z")}</Option>
              <Option value="zip">{t("settings.profile.modImport.compressionType.zip")}</Option>
              <Option value="rar">{t("settings.profile.modImport.compressionType.rar")}</Option>
            </CompactSelect>
          </CompactField>
          <CompactField
            label={t("settings.profile.modImport.compressionMode.label")}
            description={t("settings.profile.modImport.compressionMode.tooltip")}
          >
            <CompactSelect value={compressionMode} onChange={setCompressionMode}>
              <Option value="fast">{t("settings.profile.modImport.compressionMode.fast")}</Option>
              <Option value="high">{t("settings.profile.modImport.compressionMode.high")}</Option>
              <Option value="ultra">{t("settings.profile.modImport.compressionMode.ultra")}</Option>
            </CompactSelect>
          </CompactField>
        </div>
      </CompactCard>
    </div>
  );
};
