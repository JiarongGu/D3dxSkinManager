import React, { useEffect } from "react";
import { Form, InputNumber, Button, Space, Input, Select } from "antd";
import {
  CameraOutlined,
  EyeOutlined,
  EyeInvisibleOutlined,
  SaveOutlined,
  DeleteOutlined,
  CheckOutlined,
  CloseOutlined,
} from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import "./ScreenCaptureTool.css";
import {
  useScreenCapture,
  ScreenCaptureProvider,
  NEW_PROFILE_ID,
} from "./ScreenCaptureContext";
import { CompactButton } from "../../../../shared/components/compact";

/**
 * Screen Capture Control Panel (Inner Component)
 * Uses ScreenCaptureContext for all state and operations
 */
const ScreenCaptureToolInner: React.FC = () => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const {
    profiles,
    selectedProfileId,
    isNewProfile,
    isDirty,
    isEditingName,
    editingName,
    setEditingName,
    showingBorder,
    setForm,
    handleProfileChange,
    handleSaveProfile,
    handleDeleteProfile,
    handleCancelEditName,
    handleToggleBorder,
    handleCapture,
    handleFormValuesChange,
  } = useScreenCapture();

  // Register form with context
  useEffect(() => {
    setForm(form);
  }, [form, setForm]);

  return (
    <div className="screen-capture-tool">
      {/* Row 1: Profile selection and management */}
      <Space style={{ width: "100%" }} size="small" orientation="vertical">
        {isEditingName ? (
          <Space size="small" style={{ width: "100%" }}>
            <Input
              size="small"
              style={{ flex: 1, minWidth: 188 }}
              placeholder={t("capture.enterProfileName")}
              value={editingName}
              onChange={(e) => setEditingName(e.target.value)}
              onPressEnter={handleSaveProfile}
              autoFocus
            />
            <Button
              size="small"
              icon={<CheckOutlined />}
              onClick={handleSaveProfile}
              type="primary"
              title={t("capture.button.save")}
            />
            <Button
              size="small"
              icon={<CloseOutlined />}
              onClick={handleCancelEditName}
              title={t("common.cancel")}
            />
          </Space>
        ) : (
          <Space size="small" style={{ width: "100%" }}>
            <Select
              style={{ flex: 1, minWidth: 188 }}
              placeholder={t("capture.selectProfile")}
              value={selectedProfileId ?? NEW_PROFILE_ID}
              onChange={handleProfileChange}
              size="small"
              showSearch
              listHeight={100}
              options={[
                { label: t("capture.newProfile"), value: NEW_PROFILE_ID },
                ...profiles.map((p) => ({
                  label: p.name,
                  value: p.id,
                }))
              ]}
              filterOption={(input, option) =>
                (option?.label ?? '').toString().toLowerCase().includes(input.toLowerCase())
              }
            />
            <Button
              size="small"
              icon={<SaveOutlined />}
              onClick={handleSaveProfile}
              disabled={!isNewProfile && !isDirty}
              type="primary"
              title={t("capture.button.save")}
            />
            <Button
              size="small"
              icon={<DeleteOutlined />}
              onClick={handleDeleteProfile}
              disabled={isNewProfile}
              danger
              title={t("common.delete")}
            />
          </Space>
        )}
      </Space>

      {/* Row 2: Position and Size - labels inline with inputs */}
      <Form
        className="screen-capture-tool-form"
        form={form}
        layout="inline"
        size="small"
        onValuesChange={handleFormValuesChange}
      >
        <Space size="small" style={{ width: "100%", gap: 16 }}>
          <Form.Item label="X" name="x" style={{ marginBottom: 0 }}>
            <InputNumber style={{ width: 96 }} min={-10000} max={10000} />
          </Form.Item>
          <Form.Item label="Y" name="y" style={{ marginBottom: 0 }}>
            <InputNumber style={{ width: 96 }} min={-10000} max={10000} />
          </Form.Item>
        </Space>
        <Space size="small" style={{ width: "100%", gap: 16 }}>
          <Form.Item label="W" name="width" style={{ marginBottom: 0 }}>
            <InputNumber style={{ width: 96 }} min={1} max={10000} />
          </Form.Item>
          <Form.Item label="H" name="height" style={{ marginBottom: 0 }}>
            <InputNumber style={{ width: 96 }} min={1} max={10000} />
          </Form.Item>
        </Space>
      </Form>

      {/* Row 3: Actions */}
      <Space size="small" style={{ justifyContent: "space-between" }}>
        <CompactButton.Success
          size="small"
          type="primary"
          style={{
            width: "120px"
          }}
          icon={<CameraOutlined />}
          onClick={handleCapture}
        >
          {t("capture.button.capture")}
        </CompactButton.Success>
        <CompactButton
          size="small"
          type={showingBorder ? "default" : "primary"}
          icon={showingBorder ? <EyeInvisibleOutlined /> : <EyeOutlined />}
          onClick={handleToggleBorder}
          style={{
            width: "120px"
          }}
        >
          {showingBorder
            ? t("capture.button.hideArea")
            : t("capture.button.showArea")}
        </CompactButton>
      </Space>
    </div>
  );
};

/**
 * Screen Capture Control Panel (Exported Component)
 * Wraps the inner component with ScreenCaptureProvider
 */
export const ScreenCaptureTool: React.FC = () => {
  return (
    <ScreenCaptureProvider>
      <ScreenCaptureToolInner />
    </ScreenCaptureProvider>
  );
};
