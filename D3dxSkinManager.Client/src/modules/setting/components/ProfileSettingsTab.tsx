import React, { useState } from "react";
import { Space, InputNumber, Select, Segmented } from "antd";
import { ThunderboltOutlined, FolderOpenOutlined } from "@ant-design/icons";
import {
  CompactCard,
  CompactButton,
  CompactInput,
  CompactSelect,
  CompactSwitch,
  CompactField,
} from "../../../shared/components/compact";
import { useTranslation } from "react-i18next";
import { useProfile } from "../../../shared/context/ProfileContext";
import { useSettingsStore } from "../store/settingsStore";
import { handleError } from "../../../shared/utils/errorHandler";
import { notification } from "../../../shared/utils/notification";
import { ModImportConfiguration, ModWorkConfiguration, profileService, systemService } from "../../../shared/services/ipc";
import { logger } from "../../../shared/utils/logger";
import { ConfirmDialog } from "../../../shared/components/dialogs/ConfirmDialog";
import { XxmiImporterPicker } from "./XxmiImporterPicker";
import { FixToolSettingsCard } from "./FixToolSettingsCard";
import { SettingsSectionActions } from "./SettingsSectionActions";

const { Option } = Select;

/**
 * Per-profile settings. Every row uses the CompactField L1 atom (label + optional description + control)
 * for a single, consistent form style across sections; controls are store-controlled (no antd Form).
 * Each card owns its Save/Reset (SettingsSectionActions) gated on its own dirty state.
 */
export const ProfileSettingsTab: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const {
    workMode,
    workDirectory,
    internalWorkPath,
    cleanupEnabled,
    cleanupMaxCaches,
    compressionType,
    compressionMode,
    initialProfileConfig,
    initialModImportConfig,
    setWorkMode,
    setWorkDirectory,
    setCleanupEnabled,
    setCleanupMaxCaches,
    setCompressionType,
    setCompressionMode,
    setInitialProfileConfig,
    setInitialModImportConfig,
  } = useSettingsStore();

  const [savingWork, setSavingWork] = useState(false);
  const [savingImport, setSavingImport] = useState(false);

  // Per-section dirty (each card saves/resets independently).
  const workDirty =
    workMode !== initialProfileConfig.mode ||
    workDirectory !== initialProfileConfig.directory ||
    cleanupEnabled !== initialProfileConfig.cleanupEnabled ||
    cleanupMaxCaches !== initialProfileConfig.cleanupMaxCaches;
  const importDirty =
    compressionType !== initialModImportConfig.compressionType ||
    compressionMode !== initialModImportConfig.compressionMode;

  // Mod location is persisted as the work mode itself (internal / external / xxmi); the segmented
  // selector drives workMode directly.
  const handleWorkModeChange = (mode: ModWorkConfiguration['mode']) => setWorkMode(mode);

  // XXMI bind is a two-step confirm (B5 UX fix): picking an importer used to apply instantly from a
  // dropdown change with no summary or busy feedback. Now the pick is staged and a ConfirmDialog
  // shows exactly what will be bound (work dir, deploy target, launcher, launch command) before the
  // save runs — the dialog's async onOk gives the applying-spinner the user was missing, and the
  // hint notes every value stays manually adjustable in this section afterwards.
  const [pendingXxmi, setPendingXxmi] = useState<{
    importerDir: string;
    modsDir: string;
    launcherExe?: string;
    importerName?: string;
  } | undefined>(undefined);

  const handleSelectXxmiImporter = (importerDir: string, modsDir: string, launcherExe?: string, importerName?: string) => {
    setPendingXxmi({ importerDir, modsDir, launcherExe, importerName });
  };

  // The boot command is auto-derived — `XXMI Launcher.exe --nogui --xxmi <IMPORTER>` headlessly
  // launches that importer's game (see xxmi-integration.md), so the user never types a launch option.
  const applyXxmiImporter = async () => {
    const pick = pendingXxmi;
    if (!pick) return;
    if (!selectedProfileId) { notification.error(t("errors.noProfileSelected")); return; }
    try {
      await profileService.updateProfileConfig({
        profileId: selectedProfileId,
        workMode: "xxmi",
        workDirectory: pick.importerDir,
        ...(pick.launcherExe ? { launchPath: pick.launcherExe } : {}),
        ...(pick.importerName ? { launchArgs: `--nogui --xxmi ${pick.importerName}` } : {}),
      });
      // Sync the live field AND the baseline so the section isn't left dirty after the bind.
      setWorkDirectory(pick.importerDir);
      setInitialProfileConfig({ mode: "xxmi", directory: pick.importerDir, cleanupEnabled, cleanupMaxCaches });
      notification.success(t("settings.profile.modWork.xxmi.applied"));
    } catch (error: unknown) {
      notification.error(t("settings.notifications.profileConfigSaveFailed"));
      logger.error("[ProfileSettingsTab] Failed to apply XXMI importer:", error);
    } finally {
      setPendingXxmi(undefined);
    }
  };

  const handleBrowseWorkDirectory = async () => {
    if (!selectedProfileId) { notification.error(t("errors.noProfileSelected")); return; }
    try {
      const result = await systemService.openFolderDialog({
        title: t("settings.profile.modWork.directory.dialogTitle"),
        rememberPathKey: "mod-work",
      });
      if (result.success && result.filePath) setWorkDirectory(result.filePath);
    } catch (error: unknown) {
      notification.error(t("settings.notifications.workDirectoryFailed"));
      logger.error("[ProfileSettingsTab] Failed to browse work directory:", error);
    }
  };

  // --- Mod Work section save/reset ---
  const handleSaveWork = async () => {
    if (!selectedProfileId) { notification.error(t("errors.noProfileSelected")); return; }
    const usesCustomDir = workMode !== "internal";
    if (usesCustomDir && !workDirectory.trim()) {
      notification.error(t("settings.notifications.workDirectoryInvalid"));
      return;
    }
    setSavingWork(true);
    try {
      await profileService.updateProfileConfig({
        profileId: selectedProfileId,
        workMode,
        workDirectory: usesCustomDir ? workDirectory : undefined,
        cleanupEnabled,
        cleanupMaxCaches: Math.max(1, Math.min(100, cleanupMaxCaches)),
      });
      setInitialProfileConfig({ mode: workMode, directory: workDirectory, cleanupEnabled, cleanupMaxCaches });
      notification.success(t("settings.notifications.profileConfigSaved"));
    } catch (error) {
      handleError(error);
    } finally {
      setSavingWork(false);
    }
  };

  const handleResetWork = () => {
    setWorkMode(initialProfileConfig.mode);
    setWorkDirectory(initialProfileConfig.directory);
    setCleanupEnabled(initialProfileConfig.cleanupEnabled);
    setCleanupMaxCaches(initialProfileConfig.cleanupMaxCaches);
  };

  // --- Mod Import section save/reset ---
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
        title={<><ThunderboltOutlined /> {t("settings.profile.modWork.title")}</>}
        extra={<SettingsSectionActions dirty={workDirty} saving={savingWork} onSave={handleSaveWork} onReset={handleResetWork} />}
      >
        <div className="settings-view-profile-form-grid">
          <CompactField
            label={t("settings.profile.modWork.location.label")}
            description={t("settings.profile.modWork.location.tooltip")}
          >
            <Segmented
              value={workMode}
              onChange={(v) => handleWorkModeChange(v as ModWorkConfiguration['mode'])}
              options={[
                { label: t("settings.profile.modWork.location.internal"), value: "internal" },
                { label: t("settings.profile.modWork.location.xxmi"), value: "xxmi" },
                { label: t("settings.profile.modWork.location.custom"), value: "external" },
              ]}
            />
            <div style={{ marginTop: 8 }}>
              {workMode === "internal" && (
                <CompactInput value={internalWorkPath} disabled readOnly />
              )}
              {workMode === "xxmi" && (
                <>
                  <XxmiImporterPicker
                    profileId={selectedProfileId ?? undefined}
                    currentDirectory={workDirectory || undefined}
                    onSelect={handleSelectXxmiImporter}
                  />
                  <ConfirmDialog
                    visible={!!pendingXxmi}
                    title={t("settings.profile.modWork.xxmi.confirmTitle", { name: pendingXxmi?.importerName ?? "" })}
                    content={
                      <div className="xxmi-confirm">
                        <div>{t("settings.profile.modWork.xxmi.confirmIntro")}</div>
                        <div className="xxmi-confirm__row">
                          <span className="xxmi-confirm__label">{t("settings.profile.modWork.xxmi.confirmWorkDir")}</span>
                          <code>{pendingXxmi?.importerDir}</code>
                        </div>
                        <div className="xxmi-confirm__row">
                          <span className="xxmi-confirm__label">{t("settings.profile.modWork.xxmi.confirmDeploy")}</span>
                          <code>{pendingXxmi?.modsDir}</code>
                        </div>
                        {pendingXxmi?.launcherExe && (
                          <div className="xxmi-confirm__row">
                            <span className="xxmi-confirm__label">{t("settings.profile.modWork.xxmi.confirmLauncher")}</span>
                            <code>{pendingXxmi.launcherExe}</code>
                          </div>
                        )}
                        {pendingXxmi?.importerName && (
                          <div className="xxmi-confirm__row">
                            <span className="xxmi-confirm__label">{t("settings.profile.modWork.xxmi.confirmArgs")}</span>
                            <code>--nogui --xxmi {pendingXxmi.importerName}</code>
                          </div>
                        )}
                        <div className="xxmi-confirm__hint">{t("settings.profile.modWork.xxmi.confirmHint")}</div>
                      </div>
                    }
                    okText={t("common.apply")}
                    onOk={applyXxmiImporter}
                    onCancel={() => setPendingXxmi(undefined)}
                  />
                </>
              )}
              {workMode === "external" && (
                <Space.Compact style={{ width: "100%" }}>
                  <CompactInput
                    value={workDirectory}
                    onChange={(e) => setWorkDirectory(e.target.value)}
                    placeholder={t("settings.profile.modWork.directory.placeholder")}
                  />
                  <CompactButton icon={<FolderOpenOutlined />} onClick={handleBrowseWorkDirectory}>
                    {t("common.browse")}
                  </CompactButton>
                </Space.Compact>
              )}
            </div>
          </CompactField>

          <CompactField
            label={t("settings.profile.modWork.cleanup.title")}
            description={t("settings.profile.modWork.cleanup.hint")}
          >
            <Space align="center">
              <CompactSwitch
                checked={cleanupEnabled}
                onChange={setCleanupEnabled}
                checkedChildren={t("common.enable")}
                unCheckedChildren={t("common.disable")}
              />
              <span>{t("settings.profile.modWork.cleanup.maxCaches")}</span>
              <InputNumber
                min={1}
                max={100}
                value={cleanupMaxCaches}
                onChange={(v) => v !== null && setCleanupMaxCaches(v)}
                disabled={!cleanupEnabled}
                style={{ width: "80px" }}
              />
            </Space>
          </CompactField>
        </div>
      </CompactCard>

      <CompactCard
        style={{ marginTop: "16px" }}
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

      <FixToolSettingsCard />
    </div>
  );
};
