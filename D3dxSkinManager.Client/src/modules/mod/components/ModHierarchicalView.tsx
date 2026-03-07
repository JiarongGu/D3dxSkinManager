import React, { useState, useEffect, useCallback } from "react";
import { CategoryInfo, CATEGORY_IDS } from "../../../shared/types/category.types";

import { ModPreviewPanel } from "./ModPreviewPanel";
import { CategoryPanel } from "./CategoryPanel";
import { ModListPanel } from "./ModListPanel";
import { ModEditScreen } from "./ModEditScreen/ModEditScreen";
import { ResizeHandle } from "./ResizeHandle";
import { useModsStore } from "../store/modsStore";
import { useMods } from "../hooks/useMods";
import { useProfile } from "../../../shared/context/ProfileContext";
import { useResizablePanels } from "../hooks/useResizablePanels";
import { useSlideInScreen } from "../../../shared/hooks/useSlideInScreen";
import { ModImportWorkflowScreen } from "../../workflow/components";
import { useTranslation } from 'react-i18next';
import './ModHierarchicalView.css';

/**
 * ModHierarchicalView - Main mods management view
 *
 * FINAL ARCHITECTURE - Minimal prop drilling:
 * - This component ONLY handles layout and local coordination logic
 * - Child components (dialogs, panels) subscribe to their own state via useModsStore
 * - Only passes down callbacks for complex coordination (like refresh after category change)
 * - Much cleaner, better performance, easier to maintain!
 */
export const ModHierarchicalView: React.FC = () => {
  const { t } = useTranslation();

  // Only subscribe to what THIS component uses for its coordination logic
  const mods = useModsStore(s => s.mods);
  const selectedCategory = useModsStore(s => s.selectedCategory);
  const importWorkflowScreenVisible = useModsStore(s => s.importWorkflowScreenVisible);
  const unclassifiedCount = useModsStore(s => s.unclassifiedCount);

  // Operations for coordination
  const {
    refreshMods,
    loadModsByCategory,
    loadUnclassifiedMods,
    setSelectedCategory,
    clearCategoryFilter,
    setAvailableTags,
    closeImportWorkflowScreen,
    setSearchQuery,
  } = useMods();

  // Resizable panels
  const { sizes, isResizing, startResize, containerRef } = useResizablePanels();

  // Local state (not in global store)
  const { state: profileState } = useProfile();

  // Mod Imports Slide-in Screen
  useSlideInScreen({
    visible: importWorkflowScreenVisible,
    title: t('modManagement.title.modImports'),
    content: <ModImportWorkflowScreen />,
    width: '85%',
    onClose: closeImportWorkflowScreen,
  });

  // Add/remove body class during resize for global cursor
  useEffect(() => {
    if (isResizing) {
      document.body.classList.add('resizing');
    } else {
      document.body.classList.remove('resizing');
    }
    return () => document.body.classList.remove('resizing');
  }, [isResizing]);

  // Load available tags into store
  useEffect(() => {
    const load = async () => {
      if (!profileState.selectedProfile?.id) return;
      try {
        const { modService } = await import("../../../shared/services/ipc");
        const tags = await modService.getTags(profileState.selectedProfile.id);
        setAvailableTags(tags);
      } catch (error: unknown) {
        // Silently fail - not critical
      }
    };
    load();
  }, [mods?.length, profileState.selectedProfile?.id, setAvailableTags]);

  // Coordination: refresh after category change
  const handleModsRefreshAfterCategoryChange = useCallback(async () => {
    const p = refreshMods();
    if (p) await p;

    // Note: Unclassified count is now managed by ModProvider via event subscriptions

    // Reload Category filtered mods if needed
    if (selectedCategory?.id && selectedCategory.id !== CATEGORY_IDS.UNCLASSIFIED) {
      const p2 = loadModsByCategory(selectedCategory.id);
      if (p2) await p2;
    } else if (selectedCategory?.id === CATEGORY_IDS.UNCLASSIFIED) {
      const p3 = loadUnclassifiedMods();
      if (p3) await p3;
    }
  }, [refreshMods, selectedCategory, profileState.selectedProfile?.id, loadModsByCategory, loadUnclassifiedMods]);

  // Category selection handler
  const handleCategorieselect = useCallback(
    (node: CategoryInfo | undefined) => {
      setSelectedCategory(node);

      // Clear search when category changes
      setSearchQuery('');

      if (node) {
        if (node.id === CATEGORY_IDS.UNCLASSIFIED) {
          void loadUnclassifiedMods();
        } else {
          void loadModsByCategory(node.id);
        }
      } else {
        clearCategoryFilter();
      }
    },
    [setSelectedCategory, setSearchQuery, loadUnclassifiedMods, loadModsByCategory, clearCategoryFilter],
  );

  const handleUnclassifiedClick = useCallback(() => {
    const unclassifiedNode: CategoryInfo = {
      id: CATEGORY_IDS.UNCLASSIFIED,
      name: "Unclassified",
      parentId: undefined,
      priority: 0,
      children: [],
      thumbnail: undefined,
      description: undefined,
    };
    handleCategorieselect(unclassifiedNode);
  }, [handleCategorieselect]);

  return (
    <>
      <div ref={containerRef} className="mod-hierarchical-view-container">
        {/* Category Tree - subscribes to its own state inside */}
        <div style={{ width: `${sizes.categoryWidth}%` }}>
          <CategoryPanel
            onSelect={handleCategorieselect}
            onModsRefresh={handleModsRefreshAfterCategoryChange}
            unclassifiedCount={unclassifiedCount}
            onUnclassifiedClick={handleUnclassifiedClick}
          />
        </div>

        {/* Resize handle between category and mod list */}
        <ResizeHandle
          onMouseDown={(e) => startResize('category', e)}
          isResizing={isResizing === 'category'}
        />

        {/* Mods List - subscribes to its own state inside */}
        <div style={{ width: `${sizes.modListWidth}%` }}>
          <ModListPanel />
        </div>

        {/* Resize handle between mod list and preview */}
        <ResizeHandle
          onMouseDown={(e) => startResize('modList', e)}
          isResizing={isResizing === 'modList'}
        />

        {/* Preview - subscribes to selectedMod inside */}
        <div style={{ width: `${sizes.previewWidth}%` }} className="mod-hierarchical-view-preview">
          <ModPreviewPanel />
        </div>
      </div>

      {/* All dialogs/screens now subscribe to their own state - no props needed! */}
      <ModEditScreen />
    </>
  );
};
