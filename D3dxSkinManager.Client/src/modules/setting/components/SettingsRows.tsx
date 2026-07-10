import React from "react";
import "./SettingsRows.css";

/**
 * Shared settings layout (L2, pure presentation): grouped SECTIONS of one-setting-per-ROW —
 * label + description on the left, the control right-aligned, hairline separators between rows.
 * Replaces the old 2-column grids of vertical fields across the settings tabs.
 */

export const SettingSection: React.FC<{ title?: string; children: React.ReactNode }> = ({
  title,
  children,
}) => (
  <section className="settings-rows__section">
    {title && <div className="settings-rows__section-title">{title}</div>}
    {children}
  </section>
);

export const SettingRow: React.FC<{ label: string; description?: string; children: React.ReactNode }> = ({
  label,
  description,
  children,
}) => (
  <div className="settings-rows__row">
    <div className="settings-rows__text">
      <div className="settings-rows__label">{label}</div>
      {description && <div className="settings-rows__desc">{description}</div>}
    </div>
    <div className="settings-rows__control">{children}</div>
  </div>
);

export const SettingsRows: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div className="settings-rows">{children}</div>
);
