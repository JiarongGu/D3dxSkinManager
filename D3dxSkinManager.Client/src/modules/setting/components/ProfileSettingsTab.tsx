import React, { useEffect } from "react";
import { Form, Space, InputNumber, Select, Row, Col } from "antd";
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
import { ModImportConfiguration, ModWorkConfiguration, systemService } from "../../../shared/services/ipc";
import { logger } from "../../../shared/utils/logger";

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

  const handleWorkModeChange = (value: ModWorkConfiguration['mode']) => {
    setWorkMode(value);
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
            label={t("settings.profile.modWork.directory.label")}
            tooltip={t("settings.profile.modWork.directory.tooltip")}
          >
            <Space.Compact style={{ width: "100%" }}>
              <CompactSelect
                value={workMode}
                onChange={handleWorkModeChange}
                style={{ width: "140px" }}
              >
                <Option value="internal">
                  {t("settings.profile.modWork.mode.internal")}
                </Option>
                <Option value="external">
                  {t("settings.profile.modWork.mode.external")}
                </Option>
              </CompactSelect>
              <CompactInput
                value={
                  workMode === "internal" ? internalWorkPath : workDirectory
                }
                disabled={workMode === "internal"}
                onChange={
                  workMode === "external"
                    ? handleWorkDirectoryChange
                    : undefined
                }
                placeholder={
                  workMode === "external"
                    ? t("settings.profile.modWork.directory.placeholder")
                    : ""
                }
              />
              {workMode === "external" && (
                <CompactButton
                  icon={<FolderOpenOutlined />}
                  onClick={handleBrowseWorkDirectory}
                >
                  {t("common.browse")}
                </CompactButton>
              )}
            </Space.Compact>
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
