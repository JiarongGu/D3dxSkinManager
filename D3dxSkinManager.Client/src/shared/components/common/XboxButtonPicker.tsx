import React, { useMemo } from 'react';
import { Dropdown } from 'antd';
import type { MenuProps } from 'antd';
import { useTranslation } from 'react-i18next';
import { XBOX_BUTTONS } from '../../utils/keyChord';
import { CompactButton } from '../compact';

interface XboxButtonPickerProps {
  /** Called with the raw 3DMigoto value (e.g. "XB_LEFT_SHOULDER") when a button is picked. */
  onPick: (raw: string) => void;
  disabled?: boolean;
}

// Static menu items, hoisted OUT of render: an inline `menu={{ items: XBOX_BUTTONS.map(...) }}` built a
// NEW items array every render, so each re-render of the parent (the capture input re-renders on
// focus/blur/keystroke) handed the Dropdown a fresh menu → rc-dropdown re-rendered the open popup → the
// menu FLASHED. A stable reference keeps the open popup steady.
const XB_ITEMS: MenuProps['items'] = XBOX_BUTTONS.map((b) => ({ key: b.value, label: b.label }));

/**
 * L1 atom — controller-button dropdown for 3DMigoto key fields. Gamepad presses don't fire
 * KeyboardEvents, so `XB_*` bindings can't be CAPTURED like keyboard chords — they're picked from
 * this menu instead. Pairs with KeyCaptureInput and the keybinding-chip editor.
 */
export const XboxButtonPicker: React.FC<XboxButtonPickerProps> = ({ onPick, disabled }) => {
  const { t } = useTranslation();
  // Stable menu object (items are hoisted; only onClick tracks onPick) — see XB_ITEMS above.
  const menu = useMemo<MenuProps>(() => ({ items: XB_ITEMS, onClick: ({ key }) => onPick(key) }), [onPick]);
  return (
    <Dropdown
      disabled={disabled}
      trigger={['click']}
      menu={menu}
      // Render the popup INSIDE the trigger's parent, not document.body: anchored to a body that
      // re-lays-out (Space.Compact / a scrolling form), the default body-mounted popup recomputed its
      // position on every parent render and flickered. The canonical antd fix for the click-Dropdown glitch.
      getPopupContainer={(node) => node.parentElement ?? document.body}
    >
      {/* onMouseDown preventDefault: don't blur the capture field the picker sits next to. The hint rides
          the button's native title — an antd Tooltip nested in the Dropdown fought the trigger ref. */}
      <CompactButton size="small" disabled={disabled} title={t('keyCapture.xbPicker')} onMouseDown={(e) => e.preventDefault()}>
        XB
      </CompactButton>
    </Dropdown>
  );
};
