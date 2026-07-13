import React from 'react';
import { Dropdown } from 'antd';
import { useTranslation } from 'react-i18next';
import { XBOX_BUTTONS } from '../../utils/keyChord';
import { CompactButton } from '../compact';

interface XboxButtonPickerProps {
  /** Called with the raw 3DMigoto value (e.g. "XB_LEFT_SHOULDER") when a button is picked. */
  onPick: (raw: string) => void;
  disabled?: boolean;
}

/**
 * L1 atom — controller-button dropdown for 3DMigoto key fields. Gamepad presses don't fire
 * KeyboardEvents, so `XB_*` bindings can't be CAPTURED like keyboard chords — they're picked from
 * this menu instead. Pairs with KeyCaptureInput and the keybinding-chip editor.
 */
export const XboxButtonPicker: React.FC<XboxButtonPickerProps> = ({ onPick, disabled }) => {
  const { t } = useTranslation();
  return (
    // Tooltip is NOT nested here: an antd Tooltip wrapping the Dropdown trigger fought over the same
    // child ref/handlers and made the menu FLICKER open/closed. The hint rides the button's native title.
    <Dropdown
      disabled={disabled}
      trigger={['click']}
      menu={{
        items: XBOX_BUTTONS.map((b) => ({ key: b.value, label: b.label })),
        onClick: ({ key }) => onPick(key),
      }}
    >
      {/* onMouseDown preventDefault: don't blur the capture field the picker sits next to. */}
      <CompactButton size="small" disabled={disabled} title={t('keyCapture.xbPicker')} onMouseDown={(e) => e.preventDefault()}>
        XB
      </CompactButton>
    </Dropdown>
  );
};
