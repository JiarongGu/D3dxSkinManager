import { describe, it, expect } from 'vitest';
import { parseModFixInfo, modNeedsRefix } from '../modFixRef';

describe('parseModFixInfo', () => {
  it('extracts lastFixedUtc from metadata.fix', () => {
    const meta = JSON.stringify({ fix: { lastFixedUtc: '2026-07-01T00:00:00Z' } });
    expect(parseModFixInfo(meta)?.lastFixedUtc).toBe('2026-07-01T00:00:00Z');
  });

  it('returns undefined for absent / empty / garbage metadata', () => {
    expect(parseModFixInfo(undefined)).toBeUndefined();
    expect(parseModFixInfo('')).toBeUndefined();
    expect(parseModFixInfo('{ not json')).toBeUndefined();
    expect(parseModFixInfo(JSON.stringify({ remote: { sourceId: 'x' } }))).toBeUndefined();
  });
});

describe('modNeedsRefix', () => {
  const fixedJan = JSON.stringify({ fix: { lastFixedUtc: '2026-01-01T00:00:00Z' } });

  it('flags a mod fixed BEFORE the game-updated watermark', () => {
    expect(modNeedsRefix(fixedJan, '2026-06-01T00:00:00Z')).toBe(true);
  });

  it('does NOT flag a mod fixed AFTER the watermark', () => {
    expect(modNeedsRefix(fixedJan, '2025-01-01T00:00:00Z')).toBe(false);
  });

  it('does NOT flag when there is no watermark', () => {
    expect(modNeedsRefix(fixedJan, undefined)).toBe(false);
  });

  it('does NOT flag a never-fixed mod (no basis to judge)', () => {
    expect(modNeedsRefix(undefined, '2026-06-01T00:00:00Z')).toBe(false);
    expect(modNeedsRefix(JSON.stringify({ remote: {} }), '2026-06-01T00:00:00Z')).toBe(false);
  });
});
