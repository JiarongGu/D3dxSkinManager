/**
 * Mods Module - Centralized exports
 */

// Provider
export { ModProvider } from './ModProvider';

// Main hook
export { useMods, useModsState } from './hooks/useMods';

// Store (for advanced use cases)
export { useModsStore } from './store/modsStore';
export type { ModsStore, ModsState, ModsActions } from './store/modsStore';

// Operations (for direct use without hook)
export * as modOperations from './operations/modOperations';
export * as categoryOperations from './operations/categoryOperations';

// Components
export { ModHierarchicalView } from './components/ModHierarchicalView';
