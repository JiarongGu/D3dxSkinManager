// Thin ModsProvider - handles initialization and profile changes

import React, { useEffect, useCallback, useRef } from "react";
import { useProfile } from "../../shared/context/ProfileContext";
import { useModsStore } from "./store/modsStore";
import {
  eventBus,
  ModEventType,
  CategoryEventType,
  ProfileEventType,
  Module,
} from "../../shared/services/eventBus";
import { profileService } from "../../shared/services/ipc";
import * as modOps from "./operations/modOperations";
import * as categoryOps from "./operations/categoryOperations";
import { debounce } from "lodash-es";
import { logger } from "../../shared/utils/logger";
import { memoizeDebounce } from "../../shared/utils/memoizeDebounce";
import { useStableRef } from "../../shared/hooks/useStableRef";

interface ModsProviderProps {
  children: React.ReactNode;
}

/**
 * ModsProvider - manages mods module lifecycle
 *
 * Responsibilities:
 * - Initialize panel sizes from profile config for ModHierarchicalView
 * - REACTIVELY refresh mods when profile changes
 * - Subscribe to backend events
 * - Reset state when profile is cleared
 *
 * DESIGN PRINCIPLE:
 * The mods module listens to profile changes and refreshes itself automatically.
 * Other components (like ProfileSwitcher) don't need to manually trigger refresh.
 */
export const ModProvider: React.FC<ModsProviderProps> = ({ children }) => {
  const { selectedProfileId } = useProfile();
  const reset = useModsStore((state) => state.reset);
  const setPanelSizes = useModsStore((state) => state.setPanelSizes);
  const [selectedProfileIdRef, setPanelSizesRef, resetRef] = useStableRef(
    selectedProfileId,
    setPanelSizes,
    reset,
  );

  // Track the current work path to detect changes
  const workPathRef = useRef<string | null>(null);

  // Load panel sizes from profile config
  const loadPanelSizes = useCallback(async () => {
    if (!selectedProfileIdRef.current) {
      logger.info(
        "[ModProvider] No profile selected, using default panel sizes",
      );
      return;
    }

    try {
      const config = await profileService.getProfileConfiguration(
        selectedProfileIdRef.current,
      );
      const panelSize = config?.tabs?.mod?.panelSize;

      if (panelSize) {
        logger.info(
          "[ModProvider] Loading panel sizes from profile config:",
          panelSize,
        );
        const [category, modList] = panelSize.split(" ").map(Number);
        if (!isNaN(category) && !isNaN(modList)) {
          const sizes = {
            categoryWidth: category,
            modListWidth: modList,
            previewWidth: 100 - category - modList,
          };
          logger.info("[ModProvider] Setting panel sizes:", sizes);
          setPanelSizesRef.current(sizes);
        } else {
          logger.warn("[ModProvider] Invalid panel size format:", panelSize);
        }
      } else {
        logger.info(
          "[ModProvider] No panel sizes found in profile config, using defaults",
        );
      }
    } catch (error) {
      logger.error("[ModProvider] Failed to load panel sizes:", error);
    }
  }, []);

  // Check if work path changed and refresh mods/categories if needed
  const checkWorkPathChange = useCallback(async () => {
    if (!selectedProfileIdRef.current) return;

    const config = await profileService.getProfileConfiguration(
      selectedProfileIdRef.current,
    );
    const newWorkPath =
      config?.modWork?.mode === "external"
        ? config?.modWork?.directory
        : config?.modWork?.internalDirectory;

    const oldWorkPath = workPathRef.current;

    // Only refresh if work path actually changed
    if (oldWorkPath !== newWorkPath) {
      logger.info(
        "[ModProvider] Work path changed from",
        oldWorkPath,
        "to",
        newWorkPath,
        "- refreshing mods and categories",
      );
      workPathRef.current = newWorkPath || null;
      void modOps.refreshMods(selectedProfileIdRef.current);
      void categoryOps.refreshCategoryTree(selectedProfileIdRef.current);
    } else {
      logger.info(
        "[ModProvider] Config updated but work path unchanged - skipping refresh",
      );
    }
  }, []);

  const handleModListUpdate = useCallback(
    debounce(() => {
      if (!selectedProfileIdRef.current) return;
      void modOps.refreshMods(selectedProfileIdRef.current);
      void modOps.loadStatistics(selectedProfileIdRef.current);
    }, 20),
    [],
  );

  const handleSelectedModUpdate = useCallback(
    memoizeDebounce(
      (sha: string) => {
        if (!selectedProfileIdRef.current) return;
        const { selectedMod } = useModsStore.getState();
        if (selectedMod?.sha === sha) {
          void modOps.refreshSelectedMod(selectedProfileIdRef.current);
        }
      },
      (sha) => sha,
      20,
    ),
    [],
  );

  const debouncedStatsRefresh = useCallback(
    debounce(() => {
      if (!selectedProfileIdRef.current) return;
      void modOps.loadStatistics(selectedProfileIdRef.current);
    }, 20),
    [],
  );

  const handleModLoadStateChange = useCallback(
    memoizeDebounce(
      async (sha: string) => {
        if (!selectedProfileIdRef.current) return;
        await modOps.refreshMod(selectedProfileIdRef.current, sha);
      },
      (sha) => sha,
      20,
    ),
    [],
  );

  const handleCategoryTreeUpdate = useCallback(
    debounce(() => {
      if (!selectedProfileIdRef.current) return;
      void categoryOps.refreshCategoryTree(selectedProfileIdRef.current);
      void categoryOps.loadUnclassifiedCount(selectedProfileIdRef.current);
    }, 20),
    [],
  );

  // Subscribe to backend events
  useEffect(() => {
    if (!selectedProfileIdRef.current) return;

    const unsubscribeProfileConfigChanged = eventBus.subscribe(
      Module.PROFILE,
      ProfileEventType.CONFIG_UPDATED,
      () => {
        void loadPanelSizes();
        void checkWorkPathChange();
      },
    );

    const unsubscribeModListUpdated = eventBus.subscribe(
      Module.MOD,
      ModEventType.MOD_LIST_UPDATED,
      handleModListUpdate,
    );

    const unsubscribeCategoryTreeUpdated = eventBus.subscribe(
      Module.CATEGORY,
      CategoryEventType.CATEGORY_TREE_UPDATED,
      handleCategoryTreeUpdate,
    );

    const unsubscribePreviewImported = eventBus.subscribe(
      Module.MOD,
      ModEventType.PREVIEW_IMPORTED,
      (event) => {
        const sha = event.payload?.sha;
        if (sha) {
          handleSelectedModUpdate(sha);
        }
        void modOps.reloadCurrentPreview(selectedProfileIdRef.current!);
      },
    );

    const unsubscribeThumbnailUpdated = eventBus.subscribe(
      Module.MOD,
      ModEventType.THUMBNAIL_UPDATED,
      (event) => {
        const sha = event.payload?.sha;
        if (sha) {
          handleSelectedModUpdate(sha);
        }
        void modOps.reloadCurrentPreview(selectedProfileIdRef.current!);
      },
    );

    const unsubscribePreviewDeleted = eventBus.subscribe(
      Module.MOD,
      ModEventType.PREVIEW_DELETED,
      (event) => {
        const sha = event.payload?.sha;
        if (sha) {
          handleSelectedModUpdate(sha);
        }
        void modOps.reloadCurrentPreview(selectedProfileIdRef.current!);
      },
    );

    const unsubscribeModLoading = eventBus.subscribe(
      Module.MOD,
      ModEventType.LOADING,
      (event) => {
        const sha = event.payload?.sha;
        if (sha) modOps.setModLoading(sha, true);
      },
    );

    const unsubscribeModLoaded = eventBus.subscribe(
      Module.MOD,
      ModEventType.LOADED,
      (event) => {
        const sha = event.payload?.sha;
        if (sha) {
          handleSelectedModUpdate(sha);
          void handleModLoadStateChange(sha);
          debouncedStatsRefresh();
        }
      },
    );

    const unsubscribeModUnloaded = eventBus.subscribe(
      Module.MOD,
      ModEventType.UNLOADED,
      (event) => {
        const sha = event.payload?.sha;
        if (sha) {
          handleSelectedModUpdate(sha);
          void handleModLoadStateChange(sha);
          debouncedStatsRefresh();
        }
      },
    );

    const unsubscribeMetadataUpdated = eventBus.subscribe(
      Module.MOD,
      ModEventType.METADATA_UPDATED,
      (event) => {
        const sha = event.payload?.sha;
        if (sha) {
          handleSelectedModUpdate(sha);
        }
      },
    );

    const unsubscribeCacheChanged = eventBus.subscribe(
      Module.MOD,
      ModEventType.CACHE_CHANGED,
      (event) => {
        const sha = event.payload?.sha;
        if (sha) {
          handleSelectedModUpdate(sha);
        }
      },
    );

    return () => {
      handleModListUpdate.cancel();
      handleCategoryTreeUpdate.cancel();
      handleSelectedModUpdate.cancel();
      handleModLoadStateChange.cancel();
      debouncedStatsRefresh.cancel();

      unsubscribeProfileConfigChanged();
      unsubscribeModListUpdated();
      unsubscribeCategoryTreeUpdated();
      unsubscribePreviewImported();
      unsubscribeThumbnailUpdated();
      unsubscribePreviewDeleted();
      unsubscribeModLoading();
      unsubscribeModLoaded();
      unsubscribeModUnloaded();
      unsubscribeMetadataUpdated();
      unsubscribeCacheChanged();
    };
  }, []);

  // Handle profile changes - reset store and reload data
  useEffect(() => {
    if (selectedProfileId) {
      // Reset store with new profile (saves old state, restores new state if cached)
      resetRef.current(selectedProfileId);

      // Load profile-specific UI config (panel sizes, locked categories)
      void loadPanelSizes();

      // Load data for the new profile
      void categoryOps.loadCategoryTree(selectedProfileId);
      void categoryOps.loadUnclassifiedCount(selectedProfileId);
      void modOps.loadStatistics(selectedProfileId);
      void modOps.loadTags(selectedProfileId);
      // Explicitly refresh mods to reload selected category (e.g., UNCLASSIFIED)
      // This ensures that if a category was selected before reset, its mods are refreshed
      void modOps.refreshMods(selectedProfileId);
    } else {
      // No profile selected - reset to initial state
      resetRef.current(undefined);
    }
  }, [selectedProfileId]);

  return <>{children}</>;
};
