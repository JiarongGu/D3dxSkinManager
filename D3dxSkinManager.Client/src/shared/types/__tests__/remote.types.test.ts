import { describe, it, expect } from 'vitest';
import { IMPORTABLE_DOWNLOAD_TYPES, isImportableDownloadType } from '../remote.types';

/**
 * Guards the FE import-vs-open decision against drift from the backend RemoteImportService.IsImportable.
 * A kodbox/mega option once opened the browser because this list lagged the backend (fixed 2026-07-14).
 */
describe('isImportableDownloadType', () => {
  it('imports every in-app resolver type (mirrors backend IsImportable)', () => {
    for (const type of ['cloudreve', 'quark', 'mega', 'kodbox', 'direct']) {
      expect(isImportableDownloadType(type)).toBe(true);
    }
  });

  it('kodbox and mega are importable (the regression these guard)', () => {
    expect(isImportableDownloadType('kodbox')).toBe(true);
    expect(isImportableDownloadType('mega')).toBe(true);
  });

  it('opens external / unknown types in the browser', () => {
    expect(isImportableDownloadType('external')).toBe(false);
    expect(isImportableDownloadType('')).toBe(false);
    expect(isImportableDownloadType('baidu')).toBe(false);
  });

  it('IMPORTABLE_DOWNLOAD_TYPES has no external entry', () => {
    expect(IMPORTABLE_DOWNLOAD_TYPES).not.toContain('external');
  });
});
