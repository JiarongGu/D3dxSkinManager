import React from 'react';
import classNames from 'classnames';
import './KeyValueRows.css';

/**
 * L1 atom — aligned label/value rows for config summaries and confirmation dialogs (paths, commands,
 * bindings). Values render monospace and break anywhere so long Windows paths never overflow.
 * Pure visual: rows come in as props; no IPC/store. See .claude/rules/ui-component-layers.md.
 *
 * Usage:
 *   <KeyValueRows
 *     boxed
 *     title="Current binding"
 *     rows={[{ label: 'Work directory', value: 'E:\\Games\\...' }]}
 *     hint="Editable below."
 *   />
 */
export interface KeyValueRowItem {
  label: React.ReactNode;
  value: React.ReactNode;
}

export interface KeyValueRowsProps {
  rows: KeyValueRowItem[];
  /** Optional heading above the rows. */
  title?: React.ReactNode;
  /** Optional muted hint below the rows. */
  hint?: React.ReactNode;
  /** Renders the rows inside a bordered panel (summary-box style). */
  boxed?: boolean;
  className?: string;
}

export const KeyValueRows: React.FC<KeyValueRowsProps> = ({ rows, title, hint, boxed, className }) => (
  <div className={classNames('key-value-rows', { 'key-value-rows--boxed': boxed }, className)}>
    {title && <div className="key-value-rows__title">{title}</div>}
    {rows.map((row, i) => (
      <div className="key-value-rows__row" key={i}>
        <span className="key-value-rows__label">{row.label}</span>
        <span className="key-value-rows__value">{row.value}</span>
      </div>
    ))}
    {hint && <div className="key-value-rows__hint">{hint}</div>}
  </div>
);
