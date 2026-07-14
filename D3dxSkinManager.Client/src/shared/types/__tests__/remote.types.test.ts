import { describe, it, expect } from 'vitest';
import { IMPORTABLE_DOWNLOAD_TYPES, isImportableDownloadType } from '../remote.types';

/**
 * Guards the FE import-vs-open decision against drift from the backend RemoteImportService.IsImportable.
 * A kodbox/mega option once opened the browser because this list lagged the backend (fixed 2026-07-14).
 */
describe('isImportableDownloadType', () => {
  it('imports every in-app resolver type (mirrors backend IsImportable)', () => {
    for (const type of ['cloudreve', 'quark', 'baidu', 'mega', 'kodbox', 'direct']) {
      expect(isImportableDownloadType(type)).toBe(true);
    }
  });

  it('kodbox, mega and baidu are importable (the regression these guard)', () => {
    expect(isImportableDownloadType('kodbox')).toBe(true);
    expect(isImportableDownloadType('mega')).toBe(true);
    expect(isImportableDownloadType('baidu')).toBe(true);
  });

  it('opens external / unknown types in the browser', () => {
    expect(isImportableDownloadType('external')).toBe(false);
    expect(isImportableDownloadType('')).toBe(false);
    expect(isImportableDownloadType('nonsense')).toBe(false);
  });

  it('IMPORTABLE_DOWNLOAD_TYPES has no external entry', () => {
    expect(IMPORTABLE_DOWNLOAD_TYPES).not.toContain('external');
  });
});
