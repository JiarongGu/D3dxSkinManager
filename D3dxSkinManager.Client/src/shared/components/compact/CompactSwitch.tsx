import React from "react";
import { Switch, SwitchProps } from "antd";
import "./CompactSwitch.css";

export interface CompactSwitchProps extends SwitchProps {
  /**
   * Optional additional className for custom styling
   */
  className?: string;
}

/**
 * CompactSwitch - A rectangular switch toggle component
 * Provides a compact, rectangular-styled switch instead of the default rounded one
 * Uses theme colors and follows BEM naming convention
 *
 * @example
 * // Controlled usage
 * <CompactSwitch checked={value} onChange={handleChange} />
 *
 * @example
 * // With Form.Item (automatic value binding)
 * <Form.Item name="mySwitch" valuePropName="checked">
 *   <CompactSwitch />
 * </Form.Item>
 */
export const CompactSwitch: React.FC<CompactSwitchProps> = ({
  className,
  ...switchProps
}) => {
  const combinedClassName = className
    ? `compact-switch ${className}`
    : 'compact-switch';

  return (
    <Switch
      className={combinedClassName}
      {...switchProps}
    />
  );
};
