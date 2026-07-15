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

// ResizeObserver: antd's Select/Table/Tabs (rc-resize-observer) observe their DOM on mount.
if (!global.ResizeObserver) {
  global.ResizeObserver = class {
    observe(): void {}
    unobserve(): void {}
    disconnect(): void {}
  } as unknown as typeof ResizeObserver;
}

// IntersectionObserver: useDropZone observes its target to re-sync overlay bounds on visibility change.
if (!global.IntersectionObserver) {
  global.IntersectionObserver = class {
    observe(): void {}
    unobserve(): void {}
    disconnect(): void {}
    takeRecords(): IntersectionObserverEntry[] { return []; }
    root = null;
    rootMargin = '';
    thresholds = [];
  } as unknown as typeof IntersectionObserver;
}
