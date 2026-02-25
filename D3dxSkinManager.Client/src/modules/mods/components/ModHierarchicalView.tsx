import React, { useState, useEffect, useCallback } from "react";
import { Layout } from "antd";
import { CategoryInfo } from "../../../shared/types/category.types";

import { ModPreviewPanel } from "./ModPreviewPanel";
import { CategoryPanel } from "./CategoryPanel";
import { ModListPanel } from "./ModListPanel";
import { ModEditScreen } from "./ModEditScreen/ModEditScreen";
import { ModManagementScreen } from "./ModManagementScreen";
import { useModsStore } from "../store/modsStore";
import { useMods } from "../hooks/useMods";
import { useProfile } from "../../../shared/context/ProfileContext";
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
  // Only subscribe to what THIS component uses for its coordination logic
  const mods = useModsStore(s => s.mods);
  const selectedCategory = useModsStore(s => s.selectedCategory);

  // Operations for coordination
  const {
    refreshMods,
    loadModsByCategory,
    loadUnclassifiedMods,
    setSelectedCategory,
    clearCategoryFilter,
    refreshCategoryTree,
    setAvailableTags,
  } = useMods();

  // Local state (not in global store)
  const [unclassifiedCount, setUnclassifiedCount] = useState<number>(0);
  const { state: profileState } = useProfile();

  // Load unclassified count
  useEffect(() => {
    const load = async () => {
      if (!profileState.selectedProfile?.id) return;
      try {
        const { modService } = await import("../services/modService");
        const count = await modService.getUnclassifiedCount(profileState.selectedProfile.id);
        setUnclassifiedCount(count);
      } catch (error) {
        console.error("Failed to load unclassified count:", error);
      }
    };
    load();
  }, [mods.length, profileState.selectedProfile?.id]);

  // Load available tags into store
  useEffect(() => {
    const load = async () => {
      if (!profileState.selectedProfile?.id) return;
      try {
        const { modService } = await import("../services/modService");
        const tags = await modService.getTags(profileState.selectedProfile.id);
        setAvailableTags(tags);
      } catch (error) {
        console.error("Failed to load tags:", error);
      }
    };
    load();
  }, [mods.length, profileState.selectedProfile?.id, setAvailableTags]);

  // Coordination: refresh after category change
  const handleModsRefreshAfterCategoryChange = useCallback(async () => {
    const p = refreshMods();
    if (p) await p;

    // Reload unclassified count
    if (profileState.selectedProfile?.id) {
      try {
        const { modService } = await import("../services/modService");
        const count = await modService.getUnclassifiedCount(profileState.selectedProfile.id);
        setUnclassifiedCount(count);
      } catch (error) {
        console.error("Failed to reload count:", error);
      }
    }

    // Reload Category filtered mods if needed
    if (selectedCategory?.id && selectedCategory.id !== "__unclassified__") {
      const p2 = loadModsByCategory(selectedCategory.id);
      if (p2) await p2;
    } else if (selectedCategory?.id === "__unclassified__") {
      const p3 = loadUnclassifiedMods();
      if (p3) await p3;
    }
  }, [refreshMods, selectedCategory, profileState.selectedProfile?.id, loadModsByCategory, loadUnclassifiedMods]);

  // Category selection handler
  const handleCategorieselect = useCallback(
    (node: CategoryInfo | undefined) => {
      setSelectedCategory(node);

      if (node) {
        if (node.id === "__unclassified__") {
          void loadUnclassifiedMods();
        } else {
          void loadModsByCategory(node.id);
        }
      } else {
        clearCategoryFilter();
      }
    },
    [setSelectedCategory, loadUnclassifiedMods, loadModsByCategory, clearCategoryFilter],
  );

  const handleUnclassifiedClick = useCallback(() => {
    const unclassifiedNode: CategoryInfo = {
      id: "__unclassified__",
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
      <Layout className="mod-hierarchical-view-layout">
        {/* Category Tree - subscribes to its own state inside */}
        <CategoryPanel
          onSelect={handleCategorieselect}
          onRefreshTree={async () => { const p = refreshCategoryTree(); if (p) await p; }}
          onModsRefresh={handleModsRefreshAfterCategoryChange}
          unclassifiedCount={unclassifiedCount}
          onUnclassifiedClick={handleUnclassifiedClick}
        />

        {/* Mods List - subscribes to its own state inside */}
        <ModListPanel />

        {/* Preview - subscribes to selectedMod inside */}
        <Layout.Content className="mod-hierarchical-view-preview">
          <ModPreviewPanel />
        </Layout.Content>
      </Layout>

      {/* All dialogs/screens now subscribe to their own state - no props needed! */}
      <ModEditScreen />
      <ModManagementScreen />
    </>
  );
};
