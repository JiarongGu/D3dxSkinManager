import React from 'react';
import './CompactField.css';

/**
 * CompactField - L1 atom: a standardized labeled-field row for config/tooling screens.
 *
 * Layer: L1 (pure visual). Label/description via props, control via children. No IPC/store/business
 * logic — usable in pure-UI Chrome. Replaces hand-rolled `*__label` + control patterns so every
 * config form has the same label/description/control rhythm and tokens.
 *
 * Usage:
 *   <CompactField label="Executable" description="Path to the game or launcher">
 *     <Input ... />
 *   </CompactField>
 */
export interface CompactFieldProps {
  /** Field label (above the control). */
  label?: React.ReactNode;
  /** Optional helper text below the control. */
  description?: React.ReactNode;
  /** Optional hint shown to the right of the label (e.g. "optional"). */
  hint?: React.ReactNode;
  /** Marks the field required (adds an accent asterisk). */
  required?: boolean;
  /** The control(s). */
  children: React.ReactNode;
  /** Additional class name on the wrapper. */
  className?: string;
  /** Additional inline styles on the wrapper. */
  style?: React.CSSProperties;
}

export const CompactField: React.FC<CompactFieldProps> = ({
  label,
  description,
  hint,
  required,
  children,
  className,
  style,
}) => {
  return (
    <div className={`compact-field${className ? ` ${className}` : ''}`} style={style}>
      {(label || hint) && (
        <div className="compact-field__label-row">
          {label && (
            <span className="compact-field__label">
              {label}
              {required && <span className="compact-field__required">*</span>}
            </span>
          )}
          {hint && <span className="compact-field__hint">{hint}</span>}
        </div>
      )}
      <div className="compact-field__control">{children}</div>
      {description && <div className="compact-field__description">{description}</div>}
    </div>
  );
};
