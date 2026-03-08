import React, { useMemo, useState, useCallback } from "react";
import { Layout, Empty, Input, Button } from "antd";
import { SearchOutlined, PlusOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";

import { ModInfo } from "../../../../shared/types/mod.types";
import { ModList } from "./ModList";
import { ModListStatusBar } from "./ModListStatusBar";
import { useModsStore } from "../../store/modsStore";
import { useMods } from "../../hooks/useMods";
import { useDropZone } from "../../../../shared/hooks/useDropZone";
import { useScrollPosition } from "../../../../shared/hooks/useScrollPosition";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { workflowService } from "../../../../shared/services/ipc";
import { handleError } from "../../../../shared/utils/errorHandler";
import "./ModListPanel.css";

const { Sider } = Layout;
const { Search } = Input;

/**
 * ModListPanel
 *
 * NEW ARCHITECTURE:
 * - Subscribes to its own state from useModsStore
 * - Gets operations from useMods()
 * - No props needed!
 */
export const ModListPanel: React.FC = () => {
  // Subscribe to state this component needs
  const loading = useModsStore((s) => s.modLoading); // Mod loading state
  const selectedMod = useModsStore((s) => s.selectedMod);
  const searchQuery = useModsStore((s) => s.searchQuery);
  const selectedCategory = useModsStore((s) => s.selectedCategory);
  const viewMode = useModsStore((s) => s.viewMode);
  const mods = useModsStore((s) => s.mods);

  // Get operations
  const {
    setSearchQuery,
    loadMod,
    unloadMod,
    deleteMod,
    openEditDialog,
    selectMod,
    openImportWorkflowScreen,
  } = useMods();
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const contentRef = React.useRef<HTMLDivElement>(null);

  // Scroll position persistence for mod list
  const { scrollRef, saveScrollPosition, restoreScrollPosition, resetScrollPosition } = useScrollPosition('mod-list');

  // Multi-selection state (local - not stored in mod store)
  const [selectedModShas, setSelectedModShas] = useState<string[]>([]);
  const [anchorSha, setAnchorSha] = useState<string | undefined>(undefined);

  // Enable drop zone for batch mod import
  // Allow dropping when there's a profile and either a category is selected or we're in all/loaded view
  useDropZone({
    targetRef: contentRef,
    enabled: !!selectedProfileId && (!!selectedCategory || viewMode === 'all' || viewMode === 'loaded'),
    onDrop: async (files: string[]) => {
      if (!selectedProfileId || files.length === 0) return;

      try {
        // Get selected category from store to pre-fill in workflow
        // Don't pass __unclassified__ placeholder - use undefined instead
        const categoryId = selectedCategory?.id === '__unclassified__'
          ? undefined
          : selectedCategory?.id;

        // Open the mod import workflow screen IMMEDIATELY with skeleton loading state
        // This gives instant feedback to the user
        openImportWorkflowScreen();

        // Start batch mod import workflows in background
        // Backend will validate each file/folder and reject invalid ones
        // The workflows will appear in the screen as they are created
        await workflowService.batchStartModImport(
          selectedProfileId,
          files,
          categoryId
        );
      } catch (error: unknown) {
                handleError(error);
      }
    },
    classes: {
      hover: 'mod-list-panel-drop-hover',
      drop: 'mod-list-panel-drop-active'
    }
  });

  // Compute filtered mods based on search and Category
  const filteredMods = useMemo(() => {
    // Always use Category-filtered mods (empty array if no category selected)
    let result: ModInfo[] = mods || [];

    // Apply mod search filter
    if (searchQuery) {
      const searchLower = searchQuery.toLowerCase();
      result = result.filter(
        (mod: ModInfo) =>
          mod.name.toLowerCase().includes(searchLower) ||
          (mod.author && mod.author.toLowerCase().includes(searchLower)) ||
          (mod.tags &&
            mod.tags.some((tag: string) =>
              tag.toLowerCase().includes(searchLower),
            )),
      );
    }

    return result;
  }, [ mods, searchQuery]);

  /**
   * Handle mod selection with multi-select support
   * @param mod - The mod being clicked
   * @param event - Mouse event (to check for Ctrl/Shift keys)
   */
  const handleModClick = useCallback(
    (mod: ModInfo, event?: React.MouseEvent) => {
      const ctrlKey = event?.ctrlKey || event?.metaKey; // metaKey for Mac
      const shiftKey = event?.shiftKey;

      if (ctrlKey) {
        // Ctrl+Click: Toggle selection
        const isSelected = selectedModShas.includes(mod.sha);
        if (isSelected) {
          // Remove from selection
          const newSelection = selectedModShas.filter((sha) => sha !== mod.sha);
          setSelectedModShas(newSelection);
          // Update anchor to last remaining item or undefined
          setAnchorSha(newSelection.length > 0 ? newSelection[newSelection.length - 1] : undefined);
          // Update primary selection to first item or undefined
          if (newSelection.length > 0) {
            const firstMod = filteredMods.find((m) => m.sha === newSelection[0]);
            if (firstMod) selectMod(firstMod);
          } else {
            selectMod(undefined);
          }
        } else {
          // Add to selection
          const newSelection = [...selectedModShas, mod.sha];
          setSelectedModShas(newSelection);
          setAnchorSha(mod.sha);
          // If this is the first selection, set it as primary
          if (selectedModShas.length === 0) {
            selectMod(mod);
          }
        }
      } else if (shiftKey && anchorSha) {
        // Shift+Click: Select range from anchor to current
        const anchorIndex = filteredMods.findIndex((m) => m.sha === anchorSha);
        const currentIndex = filteredMods.findIndex((m) => m.sha === mod.sha);

        if (anchorIndex !== -1 && currentIndex !== -1) {
          const start = Math.min(anchorIndex, currentIndex);
          const end = Math.max(anchorIndex, currentIndex);
          const rangeSelection = filteredMods
            .slice(start, end + 1)
            .map((m) => m.sha);
          setSelectedModShas(rangeSelection);
          // Keep anchor unchanged, primary selection is first in range
          const firstMod = filteredMods.find((m) => m.sha === rangeSelection[0]);
          if (firstMod) selectMod(firstMod);
        }
      } else {
        // Regular click: Single selection
        selectMod(mod);
        setSelectedModShas([mod.sha]);
        setAnchorSha(mod.sha);
      }
    },
    [selectedModShas, anchorSha, filteredMods, selectMod]
  );

  /**
   * Clear multi-selection when category changes and reset scroll position
   */
  React.useEffect(() => {
    setSelectedModShas([]);
    setAnchorSha(undefined);
    resetScrollPosition();
  }, [selectedCategory, resetScrollPosition]);

  const handleLoadedModClick = useCallback((mod: ModInfo) => {
    // Scroll to the loaded mod and select it
    selectMod(mod);

    // Find the mod's index in the filtered list
    const modIndex = filteredMods.findIndex(m => m.sha === mod.sha);
    if (modIndex === -1) return;

    // Scroll the mod into view
    // For lazy-loaded lists, we need to ensure the item is rendered first
    if (contentRef.current) {
      const modElement = contentRef.current.querySelector(
        `[data-mod-sha="${mod.sha}"]`,
      );

      if (modElement) {
        // Element is already rendered, scroll to it
        modElement.scrollIntoView({ behavior: "smooth", block: "center" });
      } else {
        // Element not rendered yet due to lazy loading
        // Scroll to approximate position to trigger lazy loading
        const listContainer = scrollRef.current;
        if (listContainer) {
          // Estimate item height (64px per item based on CSS)
          const estimatedItemHeight = 64;
          const estimatedScrollPosition = modIndex * estimatedItemHeight;

          // Scroll to estimated position (this will trigger lazy loading)
          listContainer.scrollTo({
            top: estimatedScrollPosition,
            behavior: "smooth"
          });

          // Wait for DOM to update after lazy loading, then scroll precisely
          setTimeout(() => {
            const modElement = contentRef.current?.querySelector(
              `[data-mod-sha="${mod.sha}"]`,
            );
            if (modElement) {
              modElement.scrollIntoView({ behavior: "smooth", block: "center" });
            }
          }, 300);
        }
      }
    }
  }, [selectMod, filteredMods, scrollRef]);

  // Show empty state only when in category mode without a selected category
  if (viewMode === 'category' && !selectedCategory) {
    return (
      <Sider width="100%" className="mod-list-panel">
        <div className="mod-list-panel-empty-container">
          <Empty
            description={t("mods.panel.selectCategory")}
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            className="mod-list-panel-empty"
          />
        </div>
      </Sider>
    );
  }

  return (
    <Sider width="100%" className="mod-list-panel-flex">
      {/* Search Bar with Add Button */}
      <div className="mod-list-panel-search-bar">
        <Search
          placeholder={t("mods.list.searchPlaceholder")}
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          allowClear
          prefix={<SearchOutlined />}
        />
        <Button
          type="default"
          icon={<PlusOutlined />}
          onClick={() => openImportWorkflowScreen()}
        />
      </div>

      {/* Mod List or Empty State */}
      <div
        className="mod-list-panel-content"
        ref={contentRef}
      >
        <div className="mod-list-panel-drop-message" data-drop-message={t("mods.panel.dropToImport")} />
        <div className="mod-list-panel-content-scrollable" ref={scrollRef}>
          {filteredMods.length > 0 ? (
            <ModList
              mods={filteredMods}
              loading={loading}
              onLoad={loadMod}
              onUnload={unloadMod}
              onDelete={deleteMod}
              onEdit={openEditDialog}
              onRowClick={handleModClick}
              selectedMod={selectedMod}
              selectedModShas={selectedModShas}
              onBeforeReload={saveScrollPosition}
              onAfterReload={restoreScrollPosition}
            />
          ) : (
            <div className="mod-list-panel-content-empty-container">
              <Empty
                description={
                  searchQuery
                    ? t("mods.panel.noModsMatchingSearch", { query: searchQuery })
                    : selectedCategory
                      ? t("mods.panel.noModsForCategory", {
                          name: selectedCategory.name,
                        })
                      : t("mods.panel.noModsAvailable")
                }
                image={Empty.PRESENTED_IMAGE_SIMPLE}
              />
            </div>
          )}
        </div>
      </div>

      {/* Status Bar at Bottom - fixed container like UnclassifiedItem */}
      <div className="mod-list-panel-status-bar-container">
        <ModListStatusBar
          mods={filteredMods}
          onLoadedModClick={handleLoadedModClick}
          selectedModCount={selectedModShas.length}
        />
      </div>
    </Sider>
  );
};
