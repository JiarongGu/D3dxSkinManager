import React, { useState } from "react";
import { Select } from "antd";
import { ThunderboltOutlined } from "@ant-design/icons";
import {
  CompactCard,
  CompactSelect,
  CompactInputNumber,
} from "../../../shared/components/compact";
import { SettingsRows, SettingSection, SettingRow } from "./SettingsRows";
import { useTranslation } from "react-i18next";
import { useProfile } from "../../../shared/context/ProfileContext";
import { useSettingsStore } from "../store/settingsStore";
import { handleError } from "../../../shared/utils/errorHandler";
import { notification } from "../../../shared/utils/notification";
import { profileService } from "../../../shared/services/ipc";
import * as settingsOps from "../operations/settingsOperations";
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
    globalSettings,
  } = useSettingsStore();

  const [savingImport, setSavingImport] = useState(false);
  // Parallel download/import concurrency is a GLOBAL setting (immediate-save), shown here alongside the
  // profile compression config; it does not participate in this card's Save/Reset (that's compression only).
  const maxParallelImports = globalSettings?.maxParallelImports ?? 5;
  const handleMaxParallelImportsChange = async (value: number | null) => {
    if (value == null) return;
    await settingsOps.updateMaxParallelImports(value, t);
  };

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
        <SettingsRows>
          <SettingSection>
            <SettingRow
              label={t("settings.profile.modImport.compressionType.label")}
              description={t("settings.profile.modImport.compressionType.tooltip")}
            >
              <CompactSelect value={compressionType} onChange={setCompressionType}>
                <Option value="7z">{t("settings.profile.modImport.compressionType.7z")}</Option>
                <Option value="zip">{t("settings.profile.modImport.compressionType.zip")}</Option>
                <Option value="rar">{t("settings.profile.modImport.compressionType.rar")}</Option>
              </CompactSelect>
            </SettingRow>
            <SettingRow
              label={t("settings.profile.modImport.compressionMode.label")}
              description={t("settings.profile.modImport.compressionMode.tooltip")}
            >
              <CompactSelect value={compressionMode} onChange={setCompressionMode}>
                <Option value="fast">{t("settings.profile.modImport.compressionMode.fast")}</Option>
                <Option value="high">{t("settings.profile.modImport.compressionMode.high")}</Option>
                <Option value="ultra">{t("settings.profile.modImport.compressionMode.ultra")}</Option>
              </CompactSelect>
            </SettingRow>
            <SettingRow
              label={t("settings.profile.modImport.maxParallelImports.label")}
              description={t("settings.profile.modImport.maxParallelImports.tooltip")}
            >
              <CompactInputNumber
                min={1}
                max={8}
                value={maxParallelImports}
                onChange={(v) => void handleMaxParallelImportsChange(v as number | null)}
              />
            </SettingRow>
          </SettingSection>
        </SettingsRows>
      </CompactCard>
    </div>
  );
};
