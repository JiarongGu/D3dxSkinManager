import React, { useState, useEffect, useCallback } from "react";
import { Layout } from "antd";
import { ClassificationNode } from "../../../shared/types/classification.types";

import { ModPreviewPanel } from "./ModPreviewPanel";
import { ClassificationPanel } from "./ClassificationPanel";
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
  const selectedClassification = useModsStore(s => s.selectedClassification);

  // Operations for coordination
  const {
    refreshMods,
    loadModsByClassification,
    loadUnclassifiedMods,
    setSelectedClassification,
    clearClassificationFilter,
    refreshClassificationTree,
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

    // Reload classification filtered mods if needed
    if (selectedClassification?.id && selectedClassification.id !== "__unclassified__") {
      const p2 = loadModsByClassification(selectedClassification.id);
      if (p2) await p2;
    } else if (selectedClassification?.id === "__unclassified__") {
      const p3 = loadUnclassifiedMods();
      if (p3) await p3;
    }
  }, [refreshMods, selectedClassification, profileState.selectedProfile?.id, loadModsByClassification, loadUnclassifiedMods]);

  // Classification selection handler
  const handleClassificationSelect = useCallback(
    (node: ClassificationNode | undefined) => {
      setSelectedClassification(node);

      if (node) {
        if (node.id === "__unclassified__") {
          void loadUnclassifiedMods();
        } else {
          void loadModsByClassification(node.id);
        }
      } else {
        clearClassificationFilter();
      }
    },
    [setSelectedClassification, loadUnclassifiedMods, loadModsByClassification, clearClassificationFilter],
  );

  const handleUnclassifiedClick = useCallback(() => {
    const unclassifiedNode: ClassificationNode = {
      id: "__unclassified__",
      name: "Unclassified",
      parentId: undefined,
      priority: 0,
      children: [],
      thumbnail: undefined,
      description: undefined,
    };
    handleClassificationSelect(unclassifiedNode);
  }, [handleClassificationSelect]);

  return (
    <>
      <Layout className="mod-hierarchical-view-layout">
        {/* Classification Tree - subscribes to its own state inside */}
        <ClassificationPanel
          onSelect={handleClassificationSelect}
          onRefreshTree={async () => { const p = refreshClassificationTree(); if (p) await p; }}
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
