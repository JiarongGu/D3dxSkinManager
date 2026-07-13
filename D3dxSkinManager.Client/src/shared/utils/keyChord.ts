/**
 * 3DMigoto key-chord helpers — capture a keyboard chord and convert between the raw `.ini` value and a
 * friendly display. Shared by the keybinding editor (KeybindingPreview) and the config editor
 * (KeyCaptureInput). Raw format uses explicit `no_ctrl`/`no_shift`/`no_alt` defaults so a plain key
 * won't also fire while another binding's modifiers are held. See .claude/knowledge/3dmigoto-ini-interface.md.
 */

export interface Chord {
  base: string;
  ctrl: boolean;
  shift: boolean;
  alt: boolean;
}

const VK_MAP: Record<string, string> = {
  ArrowUp: 'VK_UP', ArrowDown: 'VK_DOWN', ArrowLeft: 'VK_LEFT', ArrowRight: 'VK_RIGHT',
  ' ': 'VK_SPACE', Enter: 'VK_RETURN', Tab: 'VK_TAB', Backspace: 'VK_BACK',
  Delete: 'VK_DELETE', Insert: 'VK_INSERT', Home: 'VK_HOME', End: 'VK_END',
  PageUp: 'VK_PRIOR', PageDown: 'VK_NEXT',
};

const VK_DISPLAY: Record<string, string> = {
  VK_UP: '↑', VK_DOWN: '↓', VK_LEFT: '←', VK_RIGHT: '→', VK_SPACE: 'Space', VK_RETURN: 'Enter',
  VK_TAB: 'Tab', VK_BACK: 'Backspace', VK_DELETE: 'Del', VK_INSERT: 'Ins', VK_HOME: 'Home',
  VK_END: 'End', VK_PRIOR: 'PgUp', VK_NEXT: 'PgDn',
};

/**
 * Xbox controller buttons 3DMigoto accepts as `key =` values (see the key doc — `XB_*`; a second
 * pad uses `XB2_*`, type it manually). Gamepad presses don't fire KeyboardEvents, so these are
 * PICKED from a menu instead of captured. `label` is the friendly display ("XB LB").
 */
export const XBOX_BUTTONS: { value: string; label: string }[] = [
  { value: 'XB_A', label: 'XB A' }, { value: 'XB_B', label: 'XB B' },
  { value: 'XB_X', label: 'XB X' }, { value: 'XB_Y', label: 'XB Y' },
  { value: 'XB_DPAD_UP', label: 'XB D-Pad ↑' }, { value: 'XB_DPAD_DOWN', label: 'XB D-Pad ↓' },
  { value: 'XB_DPAD_LEFT', label: 'XB D-Pad ←' }, { value: 'XB_DPAD_RIGHT', label: 'XB D-Pad →' },
  { value: 'XB_LEFT_SHOULDER', label: 'XB LB' }, { value: 'XB_RIGHT_SHOULDER', label: 'XB RB' },
  { value: 'XB_LEFT_TRIGGER', label: 'XB LT' }, { value: 'XB_RIGHT_TRIGGER', label: 'XB RT' },
  { value: 'XB_LEFT_THUMB', label: 'XB LS' }, { value: 'XB_RIGHT_THUMB', label: 'XB RS' },
  { value: 'XB_START', label: 'XB Start' }, { value: 'XB_BACK', label: 'XB Back' },
  { value: 'XB_GUIDE', label: 'XB Guide' },
];

/** True when a raw 3DMigoto key value is a CONTROLLER button (`XB_*` / `XB2_*`, incl. as a chord member).
 *  The keybinding editor rebinds KEYBOARD chords only — controller bindings are shown read-only and
 *  preserved (edit them via the mod's `.ini` editor). */
export function isControllerRaw(raw: string): boolean {
  return /\bXB2?_/i.test(raw ?? '');
}

const XB_DISPLAY: Record<string, string> = Object.fromEntries(
  XBOX_BUTTONS.map((b) => [b.value, b.label]),
);

/** The non-modifier base key for a 3DMigoto binding, or null while only modifiers are held. */
export function baseFromKey(key: string): string | null {
  if (key === 'Control' || key === 'Alt' || key === 'Shift' || key === 'Meta') return null;
  if (/^[a-zA-Z0-9]$/.test(key)) return key.toLowerCase();
  if (/^F([1-9]|1[0-2])$/.test(key)) return 'VK_' + key.toUpperCase();
  return VK_MAP[key] ?? null;
}

/** Punctuation/numpad KeyboardEvent.code → 3DMigoto value (raw chars + VK names, per the key doc). */
const CODE_MAP: Record<string, string> = {
  Minus: '-', Equal: '=', BracketLeft: '[', BracketRight: ']', Semicolon: ';',
  Quote: "'", Backquote: '`', Backslash: '\\', Comma: ',', Period: '.', Slash: '/',
  NumpadMultiply: 'VK_MULTIPLY', NumpadAdd: 'VK_ADD', NumpadSubtract: 'VK_SUBTRACT',
  NumpadDecimal: 'VK_DECIMAL', NumpadDivide: 'VK_DIVIDE',
  ArrowUp: 'VK_UP', ArrowDown: 'VK_DOWN', ArrowLeft: 'VK_LEFT', ArrowRight: 'VK_RIGHT',
  Space: 'VK_SPACE', Enter: 'VK_RETURN', Tab: 'VK_TAB', Backspace: 'VK_BACK',
  Delete: 'VK_DELETE', Insert: 'VK_INSERT', Home: 'VK_HOME', End: 'VK_END',
  PageUp: 'VK_PRIOR', PageDown: 'VK_NEXT',
};

/**
 * The base key from a keydown EVENT, preferring the physical `code` over the produced `key`.
 * `key` is layout/shift-dependent — Shift+1 yields '!', Ctrl+[ yields '[', neither of which the
 * key-based lookup recognised, so COMBOS with digits/symbols could never be captured (the reported
 * "combination key editing is not working"). `code` is stable: Digit1 stays Digit1 under Shift.
 */
export function baseFromEvent(code: string, key: string): string | null {
  let m = /^Key([A-Z])$/.exec(code);
  if (m) return m[1].toLowerCase();
  m = /^(?:Digit|Numpad)([0-9])$/.exec(code);
  if (m) return code.startsWith('Numpad') ? `VK_NUMPAD${m[1]}` : m[1];
  m = /^F([1-9]|1[0-9]|2[0-4])$/.exec(code);
  if (m) return 'VK_F' + m[1];
  if (CODE_MAP[code]) return CODE_MAP[code];
  // Unknown physical code (IME keys, media keys) — fall back to the produced key.
  return baseFromKey(key);
}

/**
 * Raw 3DMigoto value. Unheld modifiers default to `no_ctrl`/`no_shift`/`no_alt`. e.g. "j" →
 * "no_ctrl no_shift no_alt j"; Ctrl+J → "ctrl no_shift no_alt j".
 */
export function buildRaw(c: Chord): string {
  return [c.ctrl ? 'ctrl' : 'no_ctrl', c.shift ? 'shift' : 'no_shift', c.alt ? 'alt' : 'no_alt', c.base].join(' ');
}

function baseDisplay(base: string): string {
  const upper = base.toUpperCase();
  // XB2_* (second controller) shares the XB_* display with a "2" marker.
  if (upper.startsWith('XB2_')) {
    const xb = XB_DISPLAY['XB_' + upper.slice(4)];
    return xb ? xb.replace('XB ', 'XB2 ') : base;
  }
  if (upper.startsWith('XB_')) return XB_DISPLAY[upper] ?? base;
  return base.startsWith('VK_') ? (VK_DISPLAY[base] ?? base.replace('VK_', '')) : base.toUpperCase();
}

/** Friendly display of a captured chord (active modifiers only). e.g. "Ctrl + J", "F5". */
export function buildDisplay(c: Chord): string {
  const parts: string[] = [];
  if (c.ctrl) parts.push('Ctrl');
  if (c.shift) parts.push('Shift');
  if (c.alt) parts.push('Alt');
  parts.push(baseDisplay(c.base));
  return parts.join(' + ');
}

/**
 * Friendly display of an existing raw `.ini` key value (e.g. "no_ctrl no_shift no_alt j" → "J",
 * "ctrl no_shift no_alt j" → "Ctrl + J", "9" → "9", "VK_F1" → "F1"). `no_*` tokens are dropped.
 * Falls back to the trimmed raw string if it doesn't parse.
 */
export function rawToDisplay(raw: string): string {
  const tokens = (raw ?? '').trim().split(/\s+/).filter(Boolean);
  if (tokens.length === 0) return '';
  const mods: string[] = [];
  let base = '';
  for (const tok of tokens) {
    const low = tok.toLowerCase();
    if (low === 'ctrl') mods.push('Ctrl');
    else if (low === 'shift') mods.push('Shift');
    else if (low === 'alt') mods.push('Alt');
    else if (low === 'no_ctrl' || low === 'no_shift' || low === 'no_alt') continue;
    else base = tok; // last non-modifier token wins
  }
  const out = [...mods, base ? baseDisplay(base) : ''].filter(Boolean).join(' + ');
  return out || raw.trim();
}
