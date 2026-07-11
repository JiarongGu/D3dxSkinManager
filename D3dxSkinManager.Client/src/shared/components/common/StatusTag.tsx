import React from 'react';
import { Tag } from 'antd';
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  LoadingOutlined,
  ExclamationCircleOutlined,
  MinusCircleOutlined,
  InfoCircleOutlined,
} from '@ant-design/icons';

/**
 * L1 atom — semantic status tag. Pure visual: a `tone` maps to a consistent antd Tag color + default
 * icon across the whole app (process status, fix results, mod states, health, …). No data/logic — the
 * caller maps its own status enum to a tone + label. See .claude/knowledge/ui-component-layers.md.
 */
export type StatusTone = 'success' | 'error' | 'warning' | 'processing' | 'neutral' | 'info';

const TONE_META: Record<StatusTone, { color: string; icon: React.ReactNode }> = {
  success: { color: 'success', icon: <CheckCircleOutlined /> },
  error: { color: 'error', icon: <CloseCircleOutlined /> },
  warning: { color: 'warning', icon: <ExclamationCircleOutlined /> },
  processing: { color: 'processing', icon: <LoadingOutlined spin /> },
  neutral: { color: 'default', icon: <MinusCircleOutlined /> },
  info: { color: 'blue', icon: <InfoCircleOutlined /> },
};

export interface StatusTagProps {
  tone: StatusTone;
  label: React.ReactNode;
  /** Override the tone's default icon; pass `null` to render no icon. */
  icon?: React.ReactNode | null;
  className?: string;
  title?: string;
}

export const StatusTag: React.FC<StatusTagProps> = ({ tone, label, icon, className, title }) => {
  const meta = TONE_META[tone];
  const resolvedIcon = icon === undefined ? meta.icon : (icon || undefined);
  return (
    <Tag color={meta.color} icon={resolvedIcon} className={className} title={title}>
      {label}
    </Tag>
  );
};
