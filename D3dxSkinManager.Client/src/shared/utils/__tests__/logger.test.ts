import { describe, it, expect } from 'vitest';
import { Logger, LogLevel } from '../logger';

// Locks the case-insensitive level handling: the settings UI (getLevelOptions) uses lowercase values
// while the LogLevel enum keys are uppercase, so a stored/selected lowercase level must still resolve.
describe('Logger.setLevel casing', () => {
  it('accepts a lowercase level name (as getLevelOptions emits)', () => {
    const l = new Logger();
    l.setLevel('info');
    expect(l.getLevel()).toBe(LogLevel.INFO);
  });

  it('accepts an uppercase level name', () => {
    const l = new Logger();
    l.setLevel('WARN');
    expect(l.getLevel()).toBe(LogLevel.WARN);
  });

  it('accepts a numeric LogLevel', () => {
    const l = new Logger();
    l.setLevel(LogLevel.ERROR);
    expect(l.getLevel()).toBe(LogLevel.ERROR);
  });
});
