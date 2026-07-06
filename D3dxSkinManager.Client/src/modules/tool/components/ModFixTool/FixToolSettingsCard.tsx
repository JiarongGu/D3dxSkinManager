import React, { useState, useEffect, useCallback } from "react";
import { Space, InputNumber } from "antd";
import { SearchOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import {
  CompactInput,
  CompactButton,
  CompactSwitch,
} from "../../../../shared/components/compact";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { profileService, toolService } from "../../../../shared/services/ipc";
import { notification } from "../../../../shared/utils/notification";
import { handleError } from "../../../../shared/utils/errorHandler";

const DEFAULT_EXTENSIONS = ".py, .exe, .bat, .cmd";

/**
 * L3 settings card for the fix-tool runner — surfaces the ModFixOptions as editable per-profile
 * config: Python interpreter (with auto-detect), per-mod timeout, accepted script extensions, and
 * stdin auto-confirm. Self-contained: loads + saves its own slice via updateProfileConfig. Lives
 * INSIDE the ModFixTool screen (moved out of global Settings — config belongs with the tool it drives).
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

  // Compact 1–2 row form (label above a small control), wraps to fit; Save/Reset pushed to the right.
  // Persists to the per-profile fix-tool config (fixTools) — the same store the backend runner reads.
  return (
    <div className="mod-fix__settings-body">
      <label className="mod-fix__setting mod-fix__setting--grow">
        <span className="mod-fix__setting-label">{t("settings.profile.fixTools.python.label")}</span>
        <Space.Compact style={{ width: "100%" }}>
          <CompactInput
            size="small"
            value={pythonPath}
            placeholder={t("settings.profile.fixTools.python.placeholder")}
            onChange={(e) => setPythonPath(e.target.value)}
          />
          <CompactButton size="small" icon={<SearchOutlined />} loading={detecting} onClick={detect}>
            {t("settings.profile.fixTools.python.detect")}
          </CompactButton>
        </Space.Compact>
      </label>

      <label className="mod-fix__setting">
        <span className="mod-fix__setting-label">{t("settings.profile.fixTools.timeout.label")}</span>
        <InputNumber
          size="small"
          min={1}
          max={120}
          value={timeoutMinutes}
          onChange={(v) => setTimeoutMinutes(v ?? 5)}
          style={{ width: 110 }}
          suffix={t("settings.profile.fixTools.timeout.minutes")}
        />
      </label>

      <label className="mod-fix__setting mod-fix__setting--grow">
        <span className="mod-fix__setting-label">{t("settings.profile.fixTools.extensions.label")}</span>
        <CompactInput size="small" value={extensions} onChange={(e) => setExtensions(e.target.value)} placeholder={DEFAULT_EXTENSIONS} />
      </label>

      <label className="mod-fix__setting">
        <span className="mod-fix__setting-label">{t("settings.profile.fixTools.autoConfirm.label")}</span>
        <CompactSwitch
          checked={autoConfirm}
          onChange={setAutoConfirm}
          checkedChildren={t("common.enable")}
          unCheckedChildren={t("common.disable")}
        />
      </label>

      <div className="mod-fix__settings-actions">
        <CompactButton size="small" disabled={!dirty || saving} onClick={reset}>
          {t("settings.section.reset")}
        </CompactButton>
        <CompactButton size="small" type="primary" loading={saving} disabled={!dirty} onClick={save}>
          {t("common.save")}
        </CompactButton>
      </div>
    </div>
  );
};
