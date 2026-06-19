import '@testing-library/jest-dom';
import { vi } from 'vitest';
import { enableMapSet } from 'immer';

// The app enables Immer's Map/Set plugin (AppWrapper) before any store is created; stores use a Set
// (e.g. modsStore.busyModIds), so tests need it too.
enableMapSet();

// jsdom is missing a few browser APIs the app touches during render — stub them so tests don't throw.
// scrollIntoView: used by ModListPanel / ScanView auto-scroll.
if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = vi.fn();
}

// matchMedia: ThemeContext reads system theme (prefers-color-scheme) on mount.
if (!window.matchMedia) {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    addListener: vi.fn(),
    removeListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })) as unknown as typeof window.matchMedia;
}
