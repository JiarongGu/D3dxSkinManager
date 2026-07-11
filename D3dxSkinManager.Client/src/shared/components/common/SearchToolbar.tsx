import React from 'react';
import classNames from 'classnames';
import { SearchOutlined } from '@ant-design/icons';
import { CompactInput } from '../compact';

/**
 * SearchToolbar — L2 molecule: a search `CompactInput` (search-icon prefix + allowClear) plus an
 * optional trailing action (e.g. a "+" add button). Dedups the two hand-kept-in-sync search bars
 * (mod-list panel ↔ category grid) that `ui-design-rules.md` requires to stay identical. Pure
 * presentational — value/onChange flow in as props; no IPC/store.
 *
 * Pass the panel's existing bar class via `className` to keep its container layout (48px/padding/
 * border); the molecule only owns the input + action markup.
 */
export interface SearchToolbarProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  /** Node inside the input's suffix slot (e.g. a search-syntax help popover). */
  inputSuffix?: React.ReactNode;
  /** Extra class on the input itself. */
  inputClassName?: string;
  /** Trailing action beside the input (e.g. a "+" add button). */
  action?: React.ReactNode;
  /** Container class — pass the panel's existing bar class to preserve its layout. */
  className?: string;
}

export const SearchToolbar: React.FC<SearchToolbarProps> = ({
  value,
  onChange,
  placeholder,
  inputSuffix,
  inputClassName,
  action,
  className,
}) => (
  <div className={classNames('search-toolbar', className)}>
    <CompactInput
      placeholder={placeholder}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      prefix={<SearchOutlined />}
      allowClear
      suffix={inputSuffix}
      className={inputClassName}
    />
    {action}
  </div>
);
