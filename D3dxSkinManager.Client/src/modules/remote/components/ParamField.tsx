import React from 'react';
import { CompactInput, CompactSelect } from '../../../shared/components/compact';
import type { RemoteSourceParam } from '../../../shared/types/remote.types';

/**
 * One library-configurable source param, rendered as a text input or a select (remote-library-redesign.md).
 * The value goes into the library's paramValues and substitutes for `{param.<key>}` in the effective config.
 * Shared by the library ADD flow (LibraryList) and the edit Detail tab (LibraryEditView).
 * Pure L1 visual — value + onChange via props. Uses the `remote-lib-mgmt__param-*` styles.
 */
export const ParamField: React.FC<{ param: RemoteSourceParam; value: string; onChange: (v: string) => void }> = ({ param, value, onChange }) => (
  <div className="remote-lib-mgmt__param-field">
    <span className="remote-lib-mgmt__param-label">{param.label || param.key}{param.required ? ' *' : ''}</span>
    {param.type === 'select' ? (
      <CompactSelect
        className="remote-lib-mgmt__param-input"
        value={value || undefined}
        placeholder={param.label || param.key}
        options={param.options.map((o) => ({ value: o.value, label: o.label || o.value }))}
        onChange={(v) => onChange((v as string) ?? '')}
      />
    ) : (
      <CompactInput
        className="remote-lib-mgmt__param-input"
        value={value}
        placeholder={param.default ?? param.label ?? param.key}
        onChange={(e) => onChange(e.target.value)}
      />
    )}
  </div>
);
