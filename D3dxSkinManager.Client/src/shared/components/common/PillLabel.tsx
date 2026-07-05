import React from 'react';
import classNames from 'classnames';
import { Tag as AntTag } from 'antd';
import './PillLabel.css';

/**
 * PillLabel — L1 atom: a small content tag (remote site tags, dates, counts) using the SAME visual
 * design as the mod TagChip (antd Tag + the shared `tag-chip` classes) so tags look identical across
 * the app. Pure visual — label via props, no IPC/store (TagChip itself is the mod-tag variant that
 * resolves per-tag colors from the Tags table; this one is the plain/uncolored variant).
 */
export interface PillLabelProps {
  label: React.ReactNode;
  title?: string;
  className?: string;
  onClick?: () => void;
}

export const PillLabel: React.FC<PillLabelProps> = ({ label, title, className, onClick }) => (
  <AntTag
    color="default"
    onClick={onClick}
    title={title}
    className={classNames('tag-chip', 'tag-chip-small', 'pill-label', { 'tag-chip-clickable': !!onClick }, className)}
  >
    {label}
  </AntTag>
);
