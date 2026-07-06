import React from 'react';
import { InputNumber, InputNumberProps } from 'antd';
import type { CompactInputSize } from './CompactInput';
import './CompactInputNumber.css';

/**
 * CompactInputNumber - InputNumber with consistent compact sizing (matches CompactInput heights).
 *
 * Layer: L1 atom (pure visual). Wraps antd InputNumber so numeric config fields share the app's
 * 24/32/40px control heights and 12/14px type instead of raw antd. Use this, never a raw InputNumber.
 *
 * Usage:
 *   <CompactInputNumber min={1} max={120} value={n} onChange={setN} />
 */
export interface CompactInputNumberProps extends Omit<InputNumberProps, 'size'> {
  /** Size - defaults to 'medium' (32px) for consistency with CompactInput. */
  size?: CompactInputSize;
}

export const CompactInputNumber = React.forwardRef<any, CompactInputNumberProps>(({
  size = 'medium',
  className = '',
  ...rest
}, ref) => {
  const antdSize = size === 'medium' ? 'middle' : size;
  const inputClassName = `compact-input-number compact-input-number-${size} ${className}`.trim();
  return <InputNumber ref={ref} size={antdSize} className={inputClassName} {...rest} />;
});
CompactInputNumber.displayName = 'CompactInputNumber';

export default CompactInputNumber;
