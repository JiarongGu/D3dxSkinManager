import React, { useState, useEffect, useCallback } from "react";
import { Space, InputNumber } from "antd";
import { ToolOutlined, SearchOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import {
  CompactCard,
  CompactInput,
  CompactButton,
  CompactSwitch,
  CompactField,
} from "../../../shared/components/compact";
import { useProfile } from "../../../shared/context/ProfileContext";
import { profileService, toolService } from "../../../shared/services/ipc";
import { notification } from "../../../shared/utils/notification";
import { handleError } from "../../../shared/utils/errorHandler";
import { SettingsSectionActions } from "./SettingsSectionActions";

const DEFAULT_EXTENSIONS = ".py, .exe, .bat, .cmd";

/**
 * L3 settings card for the fix-tool runner — surfaces the previously hard-coded ModFixOptions as
 * editable per-profile config: Python interpreter (with auto-detect), per-mod timeout, accepted
 * script extensions, and stdin auto-confirm. Self-contained: loads + saves its own slice via
 * updateProfileConfig (independent of the page's main Save). See .claude/rules/xxmi-integration.md sibling work.
 */
export const FixToolSettingsCard: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [pythonPath, setPythonPath] = useState("");
  const [timeoutMinutes, setTimeoutMinutes] = useState(5);
  const [extensions, setExtensions] = useState(DEFAULT_EXTENSIONS);
  const [autoConfirm, setAutoConfirm] = useState(true);
  const [saving, setSaving] = useState(false);
  const [detecting, setDetecting] = useState(false);
  const [saved, setSaved] = useState<{ pythonPath: string; timeoutMinutes: number; extensions: string; autoConfirm: boolean }>();

  const load = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      const cfg = await profileService.getProfileConfig(selectedProfileId);
      const fx = cfg?.fixTools;
      const p = fx?.pythonPath ?? "";
      const tm = fx?.timeoutMinutes ?? 5;
      const ext = fx?.supportedExtensions?.length ? fx.supportedExtensions.join(", ") : DEFAULT_EXTENSIONS;
      const ac = fx?.autoConfirm ?? true;
      setPythonPath(p); setTimeoutMinutes(tm); setExtensions(ext); setAutoConfirm(ac);
      setSaved({ pythonPath: p, timeoutMinutes: tm, extensions: ext, autoConfirm: ac });
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId]);
  useEffect(() => { void load(); }, [load]);

  const dirty = !!saved && (
    pythonPath.trim() !== saved.pythonPath ||
    timeoutMinutes !== saved.timeoutMinutes ||
    extensions.trim() !== saved.extensions ||
    autoConfirm !== saved.autoConfirm
  );

  const reset = useCallback(() => {
    if (!saved) return;
    setPythonPath(saved.pythonPath);
    setTimeoutMinutes(saved.timeoutMinutes);
    setExtensions(saved.extensions);
    setAutoConfirm(saved.autoConfirm);
  }, [saved]);

  const detect = useCallback(async () => {
    if (!selectedProfileId) return;
    setDetecting(true);
    try {
      const py = await toolService.detectPython(selectedProfileId);
      if (py) { setPythonPath(py); notification.success(t("settings.profile.fixTools.python.found", { python: py })); }
      else notification.info(t("settings.profile.fixTools.python.notFound"));
    } catch (error) {
      handleError(error);
    } finally {
      setDetecting(false);
    }
  }, [selectedProfileId, t]);

  const save = useCallback(async () => {
    if (!selectedProfileId) return;
    setSaving(true);
    try {
      const exts = extensions.split(",").map((e) => e.trim()).filter((e) => e.length > 0);
      await profileService.updateProfileConfig({
        profileId: selectedProfileId,
        fixToolsPythonPath: pythonPath.trim(),
        fixToolsTimeoutMinutes: timeoutMinutes,
        fixToolsExtensions: exts,
        fixToolsAutoConfirm: autoConfirm,
      });
      setSaved({ pythonPath: pythonPath.trim(), timeoutMinutes, extensions: extensions.trim(), autoConfirm });
      notification.success(t("settings.profile.fixTools.saved"));
    } catch (error) {
      handleError(error);
    } finally {
      setSaving(false);
    }
  }, [selectedProfileId, pythonPath, timeoutMinutes, extensions, autoConfirm, t]);

  return (
    <CompactCard
      title={<><ToolOutlined /> {t("settings.profile.fixTools.title")}</>}
      extra={<SettingsSectionActions dirty={dirty} saving={saving} onSave={save} onReset={reset} />}
    >
      <div className="settings-view-profile-form-grid">
        <CompactField label={t("settings.profile.fixTools.python.label")} description={t("settings.profile.fixTools.python.hint")}>
          <Space.Compact style={{ width: "100%" }}>
            <CompactInput
              value={pythonPath}
              placeholder={t("settings.profile.fixTools.python.placeholder")}
              onChange={(e) => setPythonPath(e.target.value)}
            />
            <CompactButton icon={<SearchOutlined />} loading={detecting} onClick={detect}>
              {t("settings.profile.fixTools.python.detect")}
            </CompactButton>
          </Space.Compact>
        </CompactField>

        <CompactField label={t("settings.profile.fixTools.timeout.label")}>
          <InputNumber
            min={1}
            max={120}
            value={timeoutMinutes}
            onChange={(v) => setTimeoutMinutes(v ?? 5)}
            style={{ width: "120px" }}
            // antd v6 deprecated InputNumber `addonAfter`; `suffix` shows the unit inside the field.
            suffix={t("settings.profile.fixTools.timeout.minutes")}
          />
        </CompactField>

        <CompactField label={t("settings.profile.fixTools.extensions.label")} description={t("settings.profile.fixTools.extensions.hint")}>
          <CompactInput value={extensions} onChange={(e) => setExtensions(e.target.value)} placeholder={DEFAULT_EXTENSIONS} />
        </CompactField>

        <CompactField label={t("settings.profile.fixTools.autoConfirm.label")} description={t("settings.profile.fixTools.autoConfirm.hint")}>
          <CompactSwitch
            checked={autoConfirm}
            onChange={setAutoConfirm}
            checkedChildren={t("common.enable")}
            unCheckedChildren={t("common.disable")}
          />
        </CompactField>
      </div>
    </CompactCard>
  );
};
