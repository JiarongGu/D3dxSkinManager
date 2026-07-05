import React from 'react';
import classNames from 'classnames';
import './PillLabel.css';

/**
 * PillLabel — L1 atom: a small rounded content pill (site tags, dates, counts). Pure visual —
 * label via props, no IPC/store (usable in pure-UI Chrome). Distinct from StatusTag (semantic
 * status tones) and TagChip (mod tags with per-tag colors from the Tags table).
 */
export interface PillLabelProps {
  label: React.ReactNode;
  /** Visual tone: primary-tinted (default) or neutral gray. */
  tone?: 'primary' | 'neutral';
  title?: string;
  className?: string;
  onClick?: () => void;
}

export const PillLabel: React.FC<PillLabelProps> = ({ label, tone = 'primary', title, className, onClick }) => (
  <span
    className={classNames('pill-label', `pill-label--${tone}`, { 'pill-label--clickable': !!onClick }, className)}
    title={title}
    onClick={onClick}
  >
    {label}
  </span>
);
