/**
 * 3DMigoto key-chord helpers — capture a keyboard chord and convert between the raw `.ini` value and a
 * friendly display. Shared by the keybinding editor (KeybindingPreview) and the config editor
 * (KeyCaptureInput). Raw format uses explicit `no_ctrl`/`no_shift`/`no_alt` defaults so a plain key
 * won't also fire while another binding's modifiers are held. See .claude/rules/3dmigoto-ini-interface.md.
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

/** The non-modifier base key for a 3DMigoto binding, or null while only modifiers are held. */
export function baseFromKey(key: string): string | null {
  if (key === 'Control' || key === 'Alt' || key === 'Shift' || key === 'Meta') return null;
  if (/^[a-zA-Z0-9]$/.test(key)) return key.toLowerCase();
  if (/^F([1-9]|1[0-2])$/.test(key)) return 'VK_' + key.toUpperCase();
  return VK_MAP[key] ?? null;
}

/**
 * Raw 3DMigoto value. Unheld modifiers default to `no_ctrl`/`no_shift`/`no_alt`. e.g. "j" →
 * "no_ctrl no_shift no_alt j"; Ctrl+J → "ctrl no_shift no_alt j".
 */
export function buildRaw(c: Chord): string {
  return [c.ctrl ? 'ctrl' : 'no_ctrl', c.shift ? 'shift' : 'no_shift', c.alt ? 'alt' : 'no_alt', c.base].join(' ');
}

function baseDisplay(base: string): string {
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
