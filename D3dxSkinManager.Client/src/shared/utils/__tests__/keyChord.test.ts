import { describe, it, expect } from 'vitest';
import { baseFromKey, baseFromEvent, buildRaw, buildDisplay, rawToDisplay } from '../keyChord';

describe('keyChord', () => {
  describe('baseFromKey', () => {
    it('returns null for bare modifiers', () => {
      expect(baseFromKey('Control')).toBeNull();
      expect(baseFromKey('Alt')).toBeNull();
      expect(baseFromKey('Shift')).toBeNull();
      expect(baseFromKey('Meta')).toBeNull();
    });

    it('lowercases letters and keeps digits', () => {
      expect(baseFromKey('A')).toBe('a');
      expect(baseFromKey('j')).toBe('j');
      expect(baseFromKey('9')).toBe('9');
    });

    it('maps function keys to VK_F*', () => {
      expect(baseFromKey('F1')).toBe('VK_F1');
      expect(baseFromKey('F12')).toBe('VK_F12');
    });

    it('maps named keys via the VK table', () => {
      expect(baseFromKey('ArrowUp')).toBe('VK_UP');
      expect(baseFromKey('Enter')).toBe('VK_RETURN');
      expect(baseFromKey(' ')).toBe('VK_SPACE');
    });

    it('returns null for unmapped keys', () => {
      expect(baseFromKey('Escape')).toBeNull();
      expect(baseFromKey('Dead')).toBeNull();
    });
  });

  describe('baseFromEvent (B4: code-based, layout/shift-independent)', () => {
    it('resolves letters and digits from the physical code', () => {
      expect(baseFromEvent('KeyA', 'a')).toBe('a');
      expect(baseFromEvent('Digit1', '1')).toBe('1');
    });

    it('captures Shift+digit combos (key would be "!" and used to fail)', () => {
      expect(baseFromEvent('Digit1', '!')).toBe('1');
    });

    it('captures symbol keys 3DMigoto binds as raw chars', () => {
      expect(baseFromEvent('BracketLeft', '[')).toBe('[');
      expect(baseFromEvent('Minus', '-')).toBe('-');
      expect(baseFromEvent('Slash', '/')).toBe('/');
    });

    it('maps numpad and extended function keys to VK names', () => {
      expect(baseFromEvent('Numpad5', '5')).toBe('VK_NUMPAD5');
      expect(baseFromEvent('NumpadAdd', '+')).toBe('VK_ADD');
      expect(baseFromEvent('F13', 'F13')).toBe('VK_F13');
    });

    it('stays null for bare modifiers', () => {
      expect(baseFromEvent('ControlLeft', 'Control')).toBeNull();
      expect(baseFromEvent('ShiftRight', 'Shift')).toBeNull();
    });

    it('falls back to the produced key for unknown codes', () => {
      expect(baseFromEvent('SomeExoticCode', 'Enter')).toBe('VK_RETURN');
    });
  });

  describe('buildRaw', () => {
    it('emits no_* defaults for unheld modifiers', () => {
      expect(buildRaw({ base: 'j', ctrl: false, shift: false, alt: false }))
        .toBe('no_ctrl no_shift no_alt j');
    });

    it('emits active modifiers, no_* for the rest', () => {
      expect(buildRaw({ base: 'j', ctrl: true, shift: false, alt: false }))
        .toBe('ctrl no_shift no_alt j');
      expect(buildRaw({ base: 'VK_F1', ctrl: true, shift: true, alt: true }))
        .toBe('ctrl shift alt VK_F1');
    });
  });

  describe('buildDisplay', () => {
    it('shows only active modifiers, uppercases a char base', () => {
      expect(buildDisplay({ base: 'j', ctrl: false, shift: false, alt: false })).toBe('J');
      expect(buildDisplay({ base: 'j', ctrl: true, shift: false, alt: false })).toBe('Ctrl + J');
      expect(buildDisplay({ base: 'j', ctrl: true, shift: true, alt: true })).toBe('Ctrl + Shift + Alt + J');
    });

    it('renders VK bases via the display table (or stripped prefix)', () => {
      expect(buildDisplay({ base: 'VK_UP', ctrl: false, shift: false, alt: false })).toBe('↑');
      expect(buildDisplay({ base: 'VK_F1', ctrl: false, shift: false, alt: false })).toBe('F1');
    });
  });

  describe('rawToDisplay', () => {
    it('drops no_* tokens and uppercases a plain key', () => {
      expect(rawToDisplay('no_ctrl no_shift no_alt j')).toBe('J');
      expect(rawToDisplay('9')).toBe('9');
    });

    it('keeps active modifiers', () => {
      expect(rawToDisplay('ctrl no_shift no_alt j')).toBe('Ctrl + J');
      expect(rawToDisplay('ctrl shift alt VK_F1')).toBe('Ctrl + Shift + Alt + F1');
    });

    it('renders VK bases', () => {
      expect(rawToDisplay('VK_UP')).toBe('↑');
      expect(rawToDisplay('VK_F1')).toBe('F1');
    });

    it('round-trips buildRaw → rawToDisplay back to buildDisplay', () => {
      const chord = { base: 'k', ctrl: true, shift: false, alt: true };
      expect(rawToDisplay(buildRaw(chord))).toBe(buildDisplay(chord));
    });

    it('returns empty string for empty input', () => {
      expect(rawToDisplay('')).toBe('');
      expect(rawToDisplay('   ')).toBe('');
    });
  });
});
