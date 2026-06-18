import React, { useEffect } from "react";
import { Form, Space, InputNumber, Select, Row, Col, Segmented } from "antd";
import { ThunderboltOutlined, FolderOpenOutlined } from "@ant-design/icons";
import {
  CompactCard,
  CompactButton,
  CompactInput,
  CompactSelect,
  CompactDangerButton,
  CompactSwitch,
} from "../../../shared/components/compact";
import { useTranslation } from "react-i18next";
import { useProfile } from "../../../shared/context/ProfileContext";
import { useSettingsStore } from "../store/settingsStore";
import * as settingsOps from "../operations/settingsOperations";
import { notification } from "../../../shared/utils/notification";
import { ModImportConfiguration, ModWorkConfiguration, profileService, systemService } from "../../../shared/services/ipc";
import { logger } from "../../../shared/utils/logger";
import { XxmiImporterPicker } from "./XxmiImporterPicker";
import { FixToolSettingsCard } from "./FixToolSettingsCard";

const { Option } = Select;

export const ProfileSettingsTab: React.FC = () => {
  const [form] = Form.useForm();
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
    profileConfigChanged,
    setWorkMode,
    setWorkDirectory,
    setCleanupEnabled,
    setCleanupMaxCaches,
    setCompressionType,
    setCompressionMode,
    setInitialProfileConfig,
    resetProfileConfig,
  } = useSettingsStore();

  // Sync form fields with store state whenever they change
  useEffect(() => {
    form.setFieldsValue({
      workMode: workMode,
      workDirectory: workDirectory,
      cleanupEnabled: cleanupEnabled,
      cleanupMaxCaches: cleanupMaxCaches,
      compressionType: compressionType,
      compressionMode: compressionMode,
    });
  }, [
    form,
    workMode,
    workDirectory,
    cleanupEnabled,
    cleanupMaxCaches,
    compressionType,
    compressionMode,
  ]);

  // Mod location is ONE choice persisted as the work mode itself: internal (app-managed), external
  // (manual custom folder), or xxmi (an XXMI importer folder). The segmented selector drives workMode
  // directly — no derivation; the saved mode IS the source.
  const handleWorkModeChange = (mode: ModWorkConfiguration['mode']) => {
    setWorkMode(mode);
    form.setFieldValue("workMode", mode);
    // 'external' keeps the current directory for manual editing; 'xxmi' sets it when an importer is picked.
  };

  // One-click XXMI bind: picking an importer sets BOTH the work directory (its folder) and the launch
  // target (the XXMI Launcher exe) in a single immediate save (mode = "xxmi"), then resets the baseline
  // so the form isn't left dirty. Later Saves don't touch launchPath (backend only overwrites when given).
  const handleSelectXxmiImporter = async (importerDir: string, _modsDir: string, launcherExe?: string) => {
    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }
    try {
      await profileService.updateProfileConfig({
        profileId: selectedProfileId,
        workMode: "xxmi",
        workDirectory: importerDir,
        ...(launcherExe ? { launchPath: launcherExe } : {}),
      });
      setInitialProfileConfig({
        mode: "xxmi",
        directory: importerDir,
        cleanupEnabled,
        cleanupMaxCaches,
      });
      form.setFieldValue("workMode", "xxmi");
      form.setFieldValue("workDirectory", importerDir);
      notification.success(t("settings.profile.modWork.xxmi.applied"));
    } catch (error: unknown) {
      notification.error(t("settings.notifications.profileConfigSaveFailed"));
      logger.error("[ProfileSettingsTab] Failed to apply XXMI importer:", error);
    }
  };

  const handleBrowseWorkDirectory = async () => {
    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }

    try {
      const result = await systemService.openFolderDialog({
        title: t("settings.profile.modWork.directory.dialogTitle"),
        rememberPathKey: "mod-work",
      });

      if (result.success && result.filePath) {
        setWorkDirectory(result.filePath);
        form.setFieldValue("workDirectory", result.filePath);
      }
    } catch (error: unknown) {
      notification.error(t("settings.notifications.workDirectoryFailed"));
      logger.error("[ProfileSettingsTab] Failed to browse work directory:", error);
    }
  };

  const handleWorkDirectoryChange = (
    e: React.ChangeEvent<HTMLInputElement>,
  ) => {
    const newPath = e.target.value;
    setWorkDirectory(newPath);
  };

  const handleCleanupToggle = (checked: boolean) => {
    setCleanupEnabled(checked);
  };

  const handleCleanupMaxCachesChange = (value: number | null) => {
    if (value !== null) {
      setCleanupMaxCaches(value);
    }
  };

  const handleCompressionTypeChange = (value: ModImportConfiguration['compressionType']) => {
    setCompressionType(value);
  };

  const handleCompressionModeChange = (value: ModImportConfiguration['compressionMode']) => {
    setCompressionMode(value);
  };

  const handleSaveProfileConfig = async () => {
    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }

    await settingsOps.saveProfileConfig(
      selectedProfileId,
      workMode,
      workDirectory,
      cleanupEnabled,
      cleanupMaxCaches,
      compressionType,
      compressionMode,
      t,
    );
  };

  const handleResetProfileConfig = () => {
    resetProfileConfig();
    const {
      workMode: mode,
      workDirectory: dir,
      cleanupEnabled: cleanup,
      cleanupMaxCaches: max,
      compressionType: compType,
      compressionMode: compMode,
    } = useSettingsStore.getState();
    form.setFieldValue("workMode", mode);
    form.setFieldValue("workDirectory", dir);
    form.setFieldValue("cleanupEnabled", cleanup);
    form.setFieldValue("cleanupMaxCaches", max);
    form.setFieldValue("compressionType", compType);
    form.setFieldValue("compressionMode", compMode);
  };

  return (
    <Form
      form={form}
      layout="vertical"
      initialValues={{
        workMode: workMode,
        workDirectory: workDirectory,
        cleanupEnabled: cleanupEnabled,
        cleanupMaxCaches: cleanupMaxCaches,
        compressionType: compressionType,
        compressionMode: compressionMode,
      }}
    >
      <CompactCard
        title={
          <>
            <ThunderboltOutlined /> {t("settings.profile.modWork.title")}
          </>
        }
      >
        <div className={"settings-view-profile-form-grid"}>
          <Form.Item
            label={t("settings.profile.modWork.location.label")}
            tooltip={t("settings.profile.modWork.location.tooltip")}
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
                <XxmiImporterPicker
                  profileId={selectedProfileId ?? undefined}
                  currentDirectory={workDirectory || undefined}
                  onSelect={handleSelectXxmiImporter}
                />
              )}

              {workMode === "external" && (
                <Space.Compact style={{ width: "100%" }}>
                  <CompactInput
                    value={workDirectory}
                    onChange={handleWorkDirectoryChange}
                    placeholder={t("settings.profile.modWork.directory.placeholder")}
                  />
                  <CompactButton
                    icon={<FolderOpenOutlined />}
                    onClick={handleBrowseWorkDirectory}
                  >
                    {t("common.browse")}
                  </CompactButton>
                </Space.Compact>
              )}
            </div>
          </Form.Item>

          <Form.Item
            label={t("settings.profile.modWork.cleanup.title")}
            tooltip={t("settings.profile.modWork.cleanup.tooltip")}
          >
            <Space style={{ alignItems: "center" }}>
              <Form.Item
                name="cleanupEnabled"
                valuePropName="checked"
                style={{ marginBottom: 0 }}
                noStyle
              >
                <CompactSwitch
                  checkedChildren={t("common.enable")}
                  unCheckedChildren={t("common.disable")}
                  onChange={handleCleanupToggle}
                />
              </Form.Item>
              <span>{t("settings.profile.modWork.cleanup.maxCaches")}</span>
              <Form.Item
                name="cleanupMaxCaches"
                style={{ marginBottom: 0 }}
                noStyle
              >
                <InputNumber
                  min={1}
                  max={100}
                  value={cleanupMaxCaches}
                  onChange={handleCleanupMaxCachesChange}
                  disabled={!cleanupEnabled}
                  style={{ width: "80px" }}
                />
              </Form.Item>
              <span
                style={{ color: "var(--text-secondary)", fontSize: "12px" }}
              >
                {t("settings.profile.modWork.cleanup.hint")}
              </span>
            </Space>
          </Form.Item>
        </div>
      </CompactCard>

      <CompactCard
        style={{ marginTop: "16px" }}
        title={
          <>
            <ThunderboltOutlined /> {t("settings.profile.modImport.title")}
          </>
        }
      >
        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              label={t("settings.profile.modImport.compressionType.label")}
              tooltip={t("settings.profile.modImport.compressionType.tooltip")}
            >
              <Form.Item name="compressionType" style={{ marginBottom: 0 }} noStyle>
                <CompactSelect
                  value={compressionType}
                  onChange={handleCompressionTypeChange}
                >
                  <Option value="7z">{t("settings.profile.modImport.compressionType.7z")}</Option>
                  <Option value="zip">{t("settings.profile.modImport.compressionType.zip")}</Option>
                  <Option value="rar">{t("settings.profile.modImport.compressionType.rar")}</Option>
                </CompactSelect>
              </Form.Item>
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item
              label={t("settings.profile.modImport.compressionMode.label")}
              tooltip={t("settings.profile.modImport.compressionMode.tooltip")}
            >
              <Form.Item name="compressionMode" style={{ marginBottom: 0 }} noStyle>
                <CompactSelect
                  value={compressionMode}
                  onChange={handleCompressionModeChange}
                >
                  <Option value="fast">{t("settings.profile.modImport.compressionMode.fast")}</Option>
                  <Option value="high">{t("settings.profile.modImport.compressionMode.high")}</Option>
                  <Option value="ultra">{t("settings.profile.modImport.compressionMode.ultra")}</Option>
                </CompactSelect>
              </Form.Item>
            </Form.Item>
          </Col>
        </Row>
      </CompactCard>

      <FixToolSettingsCard />

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
            {t("common.saveChanges")}
          </CompactButton>
        </Space>
      </Form.Item>
    </Form>
  );
};
