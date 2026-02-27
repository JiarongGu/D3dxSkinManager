/**
 * Mods Module - Centralized exports
 */

// Provider
export { ModsProvider } from './ModsProvider';

// Main hook
export { useMods, useModsState } from './hooks/useMods';

// Store (for advanced use cases)
export { useModsStore } from './store/modsStore';
export type { ModsStore, ModsState, ModsActions } from './store/modsStore';

// Selectors (for performance-optimized subscriptions)
export * from './store/selectors/modSelectors';
export * from './store/selectors/categorySelectors';

// Operations (for direct use without hook)
export * as modOperations from './operations/modOperations';
export * as loadOperations from './operations/loadOperations';
export * as categoryOperations from './operations/categoryOperations';
export * as importOperations from './operations/importOperations';

// Components
export { ModHierarchicalView } from './components/ModHierarchicalView';
