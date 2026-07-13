import React, { useCallback, useRef, useState } from 'react';
import classNames from 'classnames';
import { Space } from 'antd';
import { useTranslation } from 'react-i18next';
import { Chord, baseFromEvent, buildRaw, buildDisplay, rawToDisplay } from '../../utils/keyChord';
import { XboxButtonPicker } from './XboxButtonPicker';
import './KeyCaptureInput.css';
import { CompactInput } from '../compact';

interface KeyCaptureInputProps {
  /** Raw 3DMigoto key value (e.g. "no_ctrl no_shift no_alt j"). */
  value: string;
  /** Called with the new raw value when a chord is captured. */
  onChange: (raw: string) => void;
  disabled?: boolean;
  className?: string;
}

/**
 * L2 control: focus it and press a key (with modifiers) to capture a 3DMigoto hotkey chord. Shows a
 * friendly display ("Ctrl + J") of the current value, emits the raw value (with `no_*` defaults) on
 * capture. Controller buttons (XB_*) can't be captured (no KeyboardEvent) — the XB picker sets them.
 * Reused by the keybinding editor and the config editor. Pure UI — parent persists onChange.
 */
export const KeyCaptureInput: React.FC<KeyCaptureInputProps> = ({ value, onChange, disabled, className }) => {
  const { t } = useTranslation();
  const [recording, setRecording] = useState(false);
  const [draftDisplay, setDraftDisplay] = useState('');
  const held = useRef<Set<string>>(new Set());

  const onKeyDown = (e: React.KeyboardEvent) => {
    e.preventDefault();
    held.current.add(e.code);
    const base = baseFromEvent(e.code, e.key);
    if (!base) return; // only a modifier held so far — keep waiting
    const chord: Chord = { base, ctrl: e.ctrlKey, shift: e.shiftKey, alt: e.altKey };
    setDraftDisplay(buildDisplay(chord));
    onChange(buildRaw(chord));
  };

  // Stable so the XboxButtonPicker's Dropdown menu keeps a steady identity (a new onPick each render
  // rebuilt the menu → the open popup flickered).
  const handlePick = useCallback((raw: string) => { setDraftDisplay(rawToDisplay(raw)); onChange(raw); }, [onChange]);

  const display = recording ? (draftDisplay || t('keyCapture.recording')) : rawToDisplay(value);

  return (
    <Space.Compact className={classNames('key-capture-input-group', className)}>
      <CompactInput
        size="small"
        readOnly
        disabled={disabled}
        className={classNames('key-capture-input', { 'key-capture-input--recording': recording })}
        value={display}
        placeholder={t('keyCapture.placeholder')}
        onFocus={() => { setRecording(true); setDraftDisplay(''); held.current.clear(); }}
        onBlur={() => setRecording(false)}
        onKeyDown={onKeyDown}
        onKeyUp={(e) => { e.preventDefault(); held.current.delete(e.code); }}
      />
      <XboxButtonPicker disabled={disabled} onPick={handlePick} />
    </Space.Compact>
  );
};
