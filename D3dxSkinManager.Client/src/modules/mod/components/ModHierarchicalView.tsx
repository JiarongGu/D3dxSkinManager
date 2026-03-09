import React, { useEffect } from "react";
import { Spin } from "antd";

import { ModPreviewPanel } from "./ModPreviewPanel";
import { CategoryPanel } from "./CategoryPanel";
import { ModListPanel } from "./ModListPanel";
import { ModEditScreen } from "./ModEditScreen/ModEditScreen";
import { ResizeHandle } from "./ResizeHandle";
import { useModsStore } from "../store/modsStore";
import { useMods } from "../hooks/useMods";
import { useResizablePanels } from "../hooks/useResizablePanels";
import { useSlideInScreen } from "../../../shared/hooks/useSlideInScreen";
import { ModImportWorkflowScreen } from "../../workflow/components";
import { useTranslation } from "react-i18next";
import "./ModHierarchicalView.css";

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
  const importWorkflowScreenVisible = useModsStore(
    (s) => s.importWorkflowScreenVisible,
  );

  // Operations for coordination
  const { closeImportWorkflowScreen } = useMods();

  // Resizable panels
  const { sizes, isResizing, startResize, containerRef } = useResizablePanels();

  // Mod Imports Slide-in Screen
  useSlideInScreen({
    visible: importWorkflowScreenVisible,
    title: t("modManagement.title.modImports"),
    content: <ModImportWorkflowScreen />,
    width: "85%",
    onClose: closeImportWorkflowScreen,
  });

  // Add/remove body class during resize for global cursor
  useEffect(() => {
    if (isResizing) {
      document.body.classList.add("resizing");
    } else {
      document.body.classList.remove("resizing");
    }
    return () => document.body.classList.remove("resizing");
  }, [isResizing]);

  // Show loading spinner while panel sizes are being loaded
  if (!sizes) {
    return (
      <Spin
        size="large"
        className="mod-hierarchical-view-container"
        style={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center"
        }}
      />
    );
  }

  return (
    <>
      <div ref={containerRef} className="mod-hierarchical-view-container">
        {/* Category Tree - fully self-contained, no props needed */}
        <div style={{ width: `${sizes.categoryWidth}%` }}>
          <CategoryPanel />
        </div>

        {/* Resize handle between category and mod list */}
        <ResizeHandle
          onMouseDown={(e) => startResize("category", e)}
          isResizing={isResizing === "category"}
        />

        {/* Mods List - subscribes to its own state inside */}
        <div style={{ width: `${sizes.modListWidth}%` }}>
          <ModListPanel />
        </div>

        {/* Resize handle between mod list and preview */}
        <ResizeHandle
          onMouseDown={(e) => startResize("modList", e)}
          isResizing={isResizing === "modList"}
        />

        {/* Preview - subscribes to selectedMod inside */}
        <div
          style={{ width: `${sizes.previewWidth}%` }}
          className="mod-hierarchical-view-preview"
        >
          <ModPreviewPanel />
        </div>
      </div>

      {/* All dialogs/screens now subscribe to their own state - no props needed! */}
      <ModEditScreen />
    </>
  );
};
