import React from 'react';
import { Input, InputProps } from 'antd';
import type { TextAreaProps } from 'antd/es/input';
import './CompactInput.css';

/**
 * CompactInput - Input component with consistent compact sizing
 *
 * Features:
 * - Consistent sizing across the application (default: 'medium' - 32px)
 * - Maintains all Ant Design Input props and functionality
 * - Centralized styling management
 *
 * Usage:
 * <CompactInput placeholder="Enter text" />
 * <CompactInput size="small" />
 */

export type CompactInputSize = 'small' | 'medium' | 'large';

export interface CompactInputProps extends Omit<InputProps, 'size'> {
  /** Input size - defaults to 'medium' (32px) for consistency */
  size?: CompactInputSize;
}

export const CompactInput: React.FC<CompactInputProps> = ({
  size = 'medium',
  className = '',
  ...rest
}) => {
  // Map our size to Ant Design size
  const antdSize = size === 'medium' ? 'middle' : size;

  // Build className with size-specific class
  const inputClassName = `compact-input compact-input-${size} ${className}`.trim();

  return (
    <Input size={antdSize} className={inputClassName} {...rest} />
  );
};

/**
 * CompactTextArea - TextArea component with consistent compact sizing
 */
export interface CompactTextAreaProps extends Omit<TextAreaProps, 'size'> {
  /** TextArea size - defaults to 'medium' for consistency */
  size?: CompactInputSize;
}

export const CompactTextArea: React.FC<CompactTextAreaProps> = ({
  size = 'medium',
  className = '',
  ...rest
}) => {
  // Build className with size-specific class
  const textAreaClassName = `compact-textarea compact-textarea-${size} ${className}`.trim();

  return (
    <Input.TextArea className={textAreaClassName} {...rest} />
  );
};

/**
 * CompactPassword - Password input component with consistent compact sizing
 */
export interface CompactPasswordProps extends Omit<InputProps, 'size'> {
  /** Input size - defaults to 'medium' (32px) for consistency */
  size?: CompactInputSize;
}

export const CompactPassword: React.FC<CompactPasswordProps> = ({
  size = 'medium',
  className = '',
  ...rest
}) => {
  // Map our size to Ant Design size
  const antdSize = size === 'medium' ? 'middle' : size;

  // Build className with size-specific class
  const inputClassName = `compact-input compact-input-${size} ${className}`.trim();

  return (
    <Input.Password size={antdSize} className={inputClassName} {...rest} />
  );
};

export default CompactInput;
