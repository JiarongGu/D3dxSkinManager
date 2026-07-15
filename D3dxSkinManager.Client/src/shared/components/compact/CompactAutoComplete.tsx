import React from 'react';
import { AutoComplete, AutoCompleteProps } from 'antd';
// antd's AutoComplete renders as a Select (.ant-select root + .ant-select-content/.ant-select-selector),
// so it reuses CompactSelect's height rules — apply the same compact-select-<size> classes, no new CSS.
import './CompactSelect.css';

/**
 * CompactAutoComplete — antd AutoComplete with consistent compact sizing (default 'medium' = 32px),
 * matching CompactInput/CompactSelect. Use this instead of raw antd `AutoComplete` in L3 views
 * (ui-component-layers.md: no raw antd form controls in connected components).
 */
export type CompactAutoCompleteSize = 'small' | 'medium' | 'large';

export interface CompactAutoCompleteProps extends Omit<AutoCompleteProps, 'size'> {
  /** Control size — defaults to 'medium' (32px) for consistency. */
  size?: CompactAutoCompleteSize;
}

export const CompactAutoComplete: React.FC<CompactAutoCompleteProps> = ({
  size = 'medium',
  className = '',
  ...rest
}) => {
  const antdSize = size === 'medium' ? 'middle' : size;
  const cls = `compact-select compact-select-${size} compact-autocomplete ${className}`.trim();
  return <AutoComplete size={antdSize} className={cls} {...rest} />;
};

export default CompactAutoComplete;
