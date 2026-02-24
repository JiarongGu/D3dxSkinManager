/**
 * Thin ModsProvider - handles initialization and profile changes
 * No longer manages state (moved to Zustand store)
 */

import React, { useEffect } from 'react';
import { useProfile } from '../../shared/context/ProfileContext';
import { useModsStore } from './store/modsStore';
import { eventBus, EventType } from '../../shared/services/eventBus';
import * as modOps from './operations/modOperations';
import * as classificationOps from './operations/classificationOperations';

interface ModsProviderProps {
  children: React.ReactNode;
}

/**
 * ModsProvider - manages mods module lifecycle
 *
 * Responsibilities:
 * - REACTIVELY refresh mods when profile changes
 * - Subscribe to backend events
 * - Reset state when profile is cleared
 *
 * DESIGN PRINCIPLE:
 * The mods module listens to profile changes and refreshes itself automatically.
 * Other components (like ProfileSwitcher) don't need to manually trigger refresh.
 */
export const ModsProvider: React.FC<ModsProviderProps> = ({ children }) => {
  const { selectedProfileId } = useProfile();
  const reset = useModsStore((state) => state.reset);

  // Subscribe to backend events
  useEffect(() => {
    if (!selectedProfileId) return;

    // Subscribe to backend mod events
    const unsubscribeModsRefreshed = eventBus.on(EventType.ModsRefreshed, () => {
      modOps.refreshMods(selectedProfileId);
    });

    const unsubscribeClassificationTreeChanged = eventBus.on(
      EventType.ClassificationTreeChanged,
      () => {
        classificationOps.refreshClassificationTree(selectedProfileId);
      }
    );

    // Cleanup subscriptions on unmount or profile change
    return () => {
      unsubscribeModsRefreshed();
      unsubscribeClassificationTreeChanged();
    };
  }, [selectedProfileId]);

  // REACTIVE: Handle profile changes automatically
  useEffect(() => {
    if (selectedProfileId) {
      // Load/refresh data for the new profile
      void Promise.all([
        modOps.loadMods(selectedProfileId),
        classificationOps.loadClassificationTree(selectedProfileId),
      ]);
    } else {
      // Reset state when no profile is selected
      reset();
    }
  }, [selectedProfileId, reset]);

  // Provider doesn't need to pass anything via context
  // Components can use useMods() hook directly
  return <>{children}</>;
};
