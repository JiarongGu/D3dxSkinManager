import React from 'react';
import classNames from 'classnames';
import './CountChip.css';

/**
 * CountChip — L1 atom: a pill showing a label + a count, usable as a filter toggle. Standardizes the
 * "总计 3 / 失败 2"-style chips so the count digit ALWAYS aligns with the (often CJK) label — antd/CSS
 * `line-height: normal` resolves to different box heights for CJK vs Latin, which floated the digit
 * higher; the atom forces one line-height on both children so they line up. Pure visual — no state.
 *   default → neutral · running → accent · waiting → warning · completed → green · failed → red
 */
export type CountChipTone = 'default' | 'running' | 'waiting' | 'completed' | 'failed';

export interface CountChipProps {
  label: React.ReactNode;
  count: number;
  tone?: CountChipTone;
  /** Selected/active state (filter is on). */
  active?: boolean;
  /** Optional leading icon (e.g. a spinner on the running chip). */
  icon?: React.ReactNode;
  onClick?: () => void;
  className?: string;
}

export const CountChip: React.FC<CountChipProps> = ({
  label,
  count,
  tone = 'default',
  active = false,
  icon,
  onClick,
  className,
}) => (
  <button
    type="button"
    className={classNames('count-chip', `count-chip--${tone}`, { 'count-chip--active': active }, className)}
    onClick={onClick}
  >
    {icon && <span className="count-chip__icon">{icon}</span>}
    <span className="count-chip__label">{label}</span>
    <span className="count-chip__count">{count}</span>
  </button>
);
