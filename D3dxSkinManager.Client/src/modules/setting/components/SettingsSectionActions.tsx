import React from "react";
import { Space } from "antd";
import { useTranslation } from "react-i18next";
import { CompactButton } from "../../../shared/components/compact";

interface SettingsSectionActionsProps {
  /** Whether this section has unsaved changes. */
  dirty: boolean;
  saving?: boolean;
  onSave: () => void;
  onReset: () => void;
}

/**
 * L2: per-section Save + Reset buttons for a settings card header (`extra`). Each settings section
 * owns its own save/reset (cleaner than one global footer on a long page) — disabled until dirty.
 */
export const SettingsSectionActions: React.FC<SettingsSectionActionsProps> = ({ dirty, saving, onSave, onReset }) => {
  const { t } = useTranslation();
  return (
    <Space size="small">
      <CompactButton size="small" disabled={!dirty || saving} onClick={onReset}>
        {t("settings.section.reset")}
      </CompactButton>
      <CompactButton size="small" type="primary" loading={saving} disabled={!dirty} onClick={onSave}>
        {t("common.save")}
      </CompactButton>
    </Space>
  );
};
