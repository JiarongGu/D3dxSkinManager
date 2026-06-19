import { describe, it, expect, beforeEach } from 'vitest';
import { useSettingsStore } from '../settingsStore';

const s = () => useSettingsStore.getState();

describe('settingsStore', () => {
  beforeEach(() => s().reset());

  describe('profileConfigChanged dirty tracking', () => {
    it('is false at the initial baseline', () => {
      expect(s().profileConfigChanged).toBe(false);
    });

    it('flips true when work mode differs from baseline, false when it matches again', () => {
      s().setWorkMode('external');
      expect(s().profileConfigChanged).toBe(true);
      s().setWorkMode('internal'); // back to the baseline default → clean again
      expect(s().profileConfigChanged).toBe(false);
    });

    it('flips true on a cleanup change', () => {
      s().setCleanupEnabled(false);
      expect(s().profileConfigChanged).toBe(true);
    });

    it('flips true on a compression change', () => {
      s().setCompressionMode('ultra');
      expect(s().profileConfigChanged).toBe(true);
    });
  });

  describe('setInitialProfileConfig', () => {
    it('sets the baseline + current values and clears dirty', () => {
      s().setWorkMode('external');
      expect(s().profileConfigChanged).toBe(true);

      s().setInitialProfileConfig({ mode: 'xxmi', directory: 'D:\\X', cleanupEnabled: false, cleanupMaxCaches: 5 });

      expect(s().workMode).toBe('xxmi');
      expect(s().workDirectory).toBe('D:\\X');
      expect(s().cleanupEnabled).toBe(false);
      expect(s().cleanupMaxCaches).toBe(5);
      expect(s().profileConfigChanged).toBe(false); // saving a baseline = no longer dirty
    });
  });

  describe('resetProfileConfig', () => {
    it('reverts current values to the saved baseline and clears dirty', () => {
      s().setInitialProfileConfig({ mode: 'xxmi', directory: 'D:\\Base', cleanupEnabled: true, cleanupMaxCaches: 10 });
      s().setWorkMode('external');
      s().setWorkDirectory('D:\\Changed');
      s().setCompressionType('zip');
      expect(s().profileConfigChanged).toBe(true);

      s().resetProfileConfig();

      expect(s().workMode).toBe('xxmi');
      expect(s().workDirectory).toBe('D:\\Base');
      expect(s().compressionType).toBe('7z'); // mod-import baseline was never changed → default
      expect(s().profileConfigChanged).toBe(false);
    });
  });
});
