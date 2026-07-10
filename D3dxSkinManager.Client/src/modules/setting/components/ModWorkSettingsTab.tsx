import React, { useCallback, useState } from "react";
import { Space, InputNumber, Segmented } from "antd";
import { FolderOutlined, FolderOpenOutlined, ExportOutlined } from "@ant-design/icons";
import {
  CompactCard,
  CompactButton,
  CompactInput,
  CompactSwitch,
  CompactField,
} from "../../../shared/components/compact";
import { KeyValueRows, type KeyValueRowItem } from "../../../shared/components/common";
import { useTranslation } from "react-i18next";
import { useProfile } from "../../../shared/context/ProfileContext";
import { useSettingsStore } from "../store/settingsStore";
import { handleError } from "../../../shared/utils/errorHandler";
import { notification } from "../../../shared/utils/notification";
import { ModWorkConfiguration, profileService, systemService, launchService } from "../../../shared/services/ipc";
import type { XxmiDetectResult } from "../../../shared/services/ipc/launchService";
import { logger } from "../../../shared/utils/logger";
import { ConfirmDialog } from "../../../shared/components/dialogs/ConfirmDialog";
import { XxmiImporterPicker } from "./XxmiImporterPicker";
import { SettingsSectionActions } from "./SettingsSectionActions";

/**
 * Mod Work settings tab: where loaded mods deploy (internal / XXMI importer / custom folder), the
 * game-launch command the status-bar Launch button runs, and disabled-cache cleanup. Controls are
 * store-controlled (no antd Form); the card owns its Save/Reset gated on its own dirty state.
 */
export const ModWorkSettingsTab: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const {
    workMode,
    workDirectory,
    internalWorkPath,
    cleanupEnabled,
    cleanupMaxCaches,
    launchPath,
    launchArgs,
    initialProfileConfig,
    initialLaunchConfig,
    setWorkMode,
    setWorkDirectory,
    setCleanupEnabled,
    setCleanupMaxCaches,
    setLaunchPath,
    setLaunchArgs,
    setLaunchConfig,
    setInitialProfileConfig,
  } = useSettingsStore();

  const [savingWork, setSavingWork] = useState(false);
  // Latest XXMI detect result — enriches the binding summary (game folder, config path).
  const [xxmiDetect, setXxmiDetect] = useState<XxmiDetectResult | undefined>(undefined);

  // Open the XXMI Launcher GUI (no --nogui): it updates itself AND the game importers there —
  // updating is XXMI's job, we just hand the user its window (xxmi-integration.md).
  const openXxmiLauncher = useCallback(async () => {
    if (!selectedProfileId || !launchPath) return;
    try {
      await launchService.launchCustomProgram(selectedProfileId, launchPath);
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId, launchPath]);

  const workDirty =
    workMode !== initialProfileConfig.mode ||
    workDirectory !== initialProfileConfig.directory ||
    cleanupEnabled !== initialProfileConfig.cleanupEnabled ||
    cleanupMaxCaches !== initialProfileConfig.cleanupMaxCaches ||
    launchPath !== initialLaunchConfig.path ||
    launchArgs !== initialLaunchConfig.args;

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

  const handleXxmiDetect = useCallback((result: XxmiDetectResult) => setXxmiDetect(result), []);

  // The boot command is auto-derived — `XXMI Launcher.exe --nogui --xxmi <IMPORTER>` headlessly
  // launches that importer's game (see xxmi-integration.md), so the user never types a launch option.
  const applyXxmiImporter = async () => {
    const pick = pendingXxmi;
    if (!pick) return;
    if (!selectedProfileId) { notification.error(t("errors.noProfileSelected")); return; }
    const newLaunchPath = pick.launcherExe ?? launchPath;
    const newLaunchArgs = pick.importerName ? `--nogui --xxmi ${pick.importerName}` : launchArgs;
    try {
      await profileService.updateProfileConfig({
        profileId: selectedProfileId,
        workMode: "xxmi",
        workDirectory: pick.importerDir,
        launchPath: newLaunchPath,
        launchArgs: newLaunchArgs,
      });
      // Sync the live fields AND the baselines so the section isn't left dirty after the bind.
      setWorkDirectory(pick.importerDir);
      setInitialProfileConfig({ mode: "xxmi", directory: pick.importerDir, cleanupEnabled, cleanupMaxCaches });
      setLaunchConfig(newLaunchPath, newLaunchArgs);
      notification.success(t("settings.profile.modWork.xxmi.applied"));
    } catch (error: unknown) {
      notification.error(t("settings.notifications.profileConfigSaveFailed"));
      logger.error("[ModWorkSettingsTab] Failed to apply XXMI importer:", error);
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
      logger.error("[ModWorkSettingsTab] Failed to browse work directory:", error);
    }
  };

  const handleBrowseLauncher = async () => {
    try {
      const result = await systemService.openFileDialog({
        title: t("settings.profile.launch.dialogTitle"),
        filters: [{ name: "Programs", extensions: ["exe", "bat", "cmd", "lnk"] }],
        rememberPathKey: "launch-target",
      });
      if (result.success && result.filePath) setLaunchPath(result.filePath);
    } catch (error: unknown) {
      handleError(error);
    }
  };

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
        // Only send launch values that actually changed — an omitted field is preserved by the backend.
        ...(launchPath !== initialLaunchConfig.path ? { launchPath } : {}),
        ...(launchArgs !== initialLaunchConfig.args ? { launchArgs } : {}),
      });
      setInitialProfileConfig({ mode: workMode, directory: workDirectory, cleanupEnabled, cleanupMaxCaches });
      setLaunchConfig(launchPath, launchArgs);
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
    setLaunchPath(initialLaunchConfig.path);
    setLaunchArgs(initialLaunchConfig.args);
  };

  // Detect-enriched info about the SAVED binding (only when the detect result covers it).
  const boundImporter = initialProfileConfig.mode === "xxmi"
    ? xxmiDetect?.importers.find((i) => i.importerDir === initialProfileConfig.directory)
    : undefined;

  const bindingRows: KeyValueRowItem[] = initialProfileConfig.directory
    ? [
        { label: t("settings.profile.modWork.xxmi.confirmWorkDir"), value: initialProfileConfig.directory },
        {
          label: t("settings.profile.modWork.xxmi.confirmDeploy"),
          value: `${initialProfileConfig.directory.replace(/[\\/]+$/, "")}\\Mods`,
        },
        ...(boundImporter?.gameFolder
          ? [{ label: t("launch.xxmi.gameFolder"), value: boundImporter.gameFolder }]
          : []),
        ...(boundImporter && xxmiDetect?.configPath
          ? [{ label: t("launch.xxmi.configPath"), value: xxmiDetect.configPath }]
          : []),
      ]
    : [];

  return (
    <div className="settings-view-profile">
      <CompactCard
        title={<><FolderOutlined /> {t("settings.profile.modWork.title")}</>}
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
                    boundDirectory={initialProfileConfig.mode === "xxmi" ? initialProfileConfig.directory : ""}
                    onSelect={handleSelectXxmiImporter}
                    onDetect={handleXxmiDetect}
                  />
                  {initialProfileConfig.mode === "xxmi" && bindingRows.length > 0 ? (
                    <KeyValueRows
                      boxed
                      className="modwork-binding-summary"
                      title={t("settings.profile.modWork.xxmi.summaryTitle")}
                      rows={bindingRows}
                      hint={t("settings.profile.modWork.xxmi.summaryHint")}
                    />
                  ) : (
                    <KeyValueRows
                      boxed
                      className="modwork-binding-summary"
                      rows={[]}
                      hint={t("settings.profile.modWork.xxmi.noBinding")}
                    />
                  )}
                  {/* Update affordance: XXMI's own GUI updates the launcher AND importers. */}
                  {!!launchPath && (
                    <div className="modwork-xxmi-update">
                      <CompactButton size="small" icon={<ExportOutlined />} onClick={() => void openXxmiLauncher()}>
                        {t("launch.xxmi.openLauncher")}
                      </CompactButton>
                      <span className="modwork-xxmi-update__hint">{t("launch.xxmi.openLauncherHint")}</span>
                    </div>
                  )}
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
            <ConfirmDialog
              visible={!!pendingXxmi}
              title={t("settings.profile.modWork.xxmi.confirmTitle", { name: pendingXxmi?.importerName ?? "" })}
              content={
                <div className="xxmi-confirm">
                  <div>{t("settings.profile.modWork.xxmi.confirmIntro")}</div>
                  <KeyValueRows
                    rows={[
                      { label: t("settings.profile.modWork.xxmi.confirmWorkDir"), value: pendingXxmi?.importerDir },
                      { label: t("settings.profile.modWork.xxmi.confirmDeploy"), value: pendingXxmi?.modsDir },
                      ...(pendingXxmi?.launcherExe
                        ? [{ label: t("settings.profile.modWork.xxmi.confirmLauncher"), value: pendingXxmi.launcherExe }]
                        : []),
                      ...(pendingXxmi?.importerName
                        ? [{ label: t("settings.profile.modWork.xxmi.confirmArgs"), value: `--nogui --xxmi ${pendingXxmi.importerName}` }]
                        : []),
                    ]}
                    hint={t("settings.profile.modWork.xxmi.confirmHint")}
                  />
                </div>
              }
              okText={t("common.apply")}
              onOk={applyXxmiImporter}
              onCancel={() => setPendingXxmi(undefined)}
            />
          </CompactField>

          <CompactField
            label={t("settings.profile.launch.label")}
            description={t("settings.profile.launch.tooltip")}
          >
            <Space.Compact style={{ width: "100%" }}>
              <CompactInput
                value={launchPath}
                onChange={(e) => setLaunchPath(e.target.value)}
                placeholder={t("launch.pathPlaceholder")}
              />
              <CompactButton icon={<FolderOpenOutlined />} onClick={handleBrowseLauncher}>
                {t("common.browse")}
              </CompactButton>
            </Space.Compact>
            <div style={{ marginTop: 8 }}>
              <CompactInput
                value={launchArgs}
                onChange={(e) => setLaunchArgs(e.target.value)}
                placeholder={t("settings.profile.launch.argsPlaceholder")}
              />
            </div>
          </CompactField>

          <CompactField
            label={t("settings.profile.modWork.cleanup.title")}
            description={t("settings.profile.modWork.cleanup.tooltip")}
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
              <span>{t("settings.profile.modWork.cleanup.hint")}</span>
            </Space>
          </CompactField>
        </div>
      </CompactCard>
    </div>
  );
};
