import React from 'react';
import { Button } from 'antd';
import classNames from 'classnames';
import './CompactIconButton.css';

/**
 * CompactIconButton — L1 atom: a clean, borderless, square icon-only button with a semantic tone.
 * Standardizes all inline icon actions (edit/confirm/cancel/etc.) so they look identical everywhere
 * instead of each component re-styling an antd button. Pure visual — no IPC/store.
 *   default → neutral · success → green · danger → red · primary → accent
 */
export type IconButtonTone = 'default' | 'success' | 'danger' | 'primary';

export interface CompactIconButtonProps {
  icon: React.ReactNode;
  tone?: IconButtonTone;
  /** Square size in px (default 26). */
  size?: number;
  title?: string;
  loading?: boolean;
  disabled?: boolean;
  className?: string;
  onClick?: (e: React.MouseEvent<HTMLElement>) => void;
  onMouseDown?: (e: React.MouseEvent<HTMLElement>) => void;
}

export const CompactIconButton: React.FC<CompactIconButtonProps> = ({
  icon,
  tone = 'default',
  size = 26,
  title,
  loading,
  disabled,
  className,
  onClick,
  onMouseDown,
}) => (
  <Button
    type="text"
    icon={icon}
    title={title}
    loading={loading}
    disabled={disabled}
    onClick={onClick}
    onMouseDown={onMouseDown}
    className={classNames('compact-icon-btn', `compact-icon-btn--${tone}`, className)}
    style={{ width: size, height: size, minWidth: size }}
  />
);
