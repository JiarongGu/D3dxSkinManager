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

export interface CompactIconButtonProps
  extends Omit<React.ComponentProps<typeof Button>, 'type' | 'icon' | 'size' | 'style'> {
  icon: React.ReactNode;
  tone?: IconButtonTone;
  /** Square size in px (default 26). */
  size?: number;
}

// Rest props are forwarded so antd Tooltip/Popconfirm (which inject hover/focus handlers via
// cloneElement) work when wrapping this atom.
export const CompactIconButton: React.FC<CompactIconButtonProps> = ({
  icon,
  tone = 'default',
  size = 26,
  className,
  ...rest
}) => (
  <Button
    {...rest}
    type="text"
    icon={icon}
    className={classNames('compact-icon-btn', `compact-icon-btn--${tone}`, className)}
    style={{ width: size, height: size, minWidth: size }}
  />
);
