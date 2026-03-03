import React, { useMemo, useRef } from "react";
import { Layout, Empty, Input, Button, Tooltip } from "antd";
import { SearchOutlined, PlusOutlined } from "@ant-design/icons";
import { ModInfo } from "../../../../shared/types/mod.types";
import { ModList } from "./ModList";
import { ModListStatusBar } from "./ModListStatusBar";
import { useModsStore } from "../../store/modsStore";
import { useMods } from "../../hooks/useMods";
import { useTranslation } from "react-i18next";
import { useDropZone } from "../../../../shared/hooks/useDropZone";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { workflowService } from "../../../workflow/services/workflowService";
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
  const mods = useModsStore((s) => s.mods);
  const loading = useModsStore((s) => s.modsLoading); // Mod list panel loading state
  const selectedMod = useModsStore((s) => s.selectedMod);
  const searchQuery = useModsStore((s) => s.searchQuery);
  const selectedCategory = useModsStore((s) => s.selectedCategory);
  const selectedObject = useModsStore((s) => s.selectedObject);
  const CategoryFilteredMods = useModsStore((s) => s.CategoryFilteredMods);

  // Get operations
  const {
    setSearchQuery,
    loadModInGame,
    unloadModFromGame,
    deleteMod,
    openEditDialog,
    selectMod,
    openModManagementScreen,
  } = useMods();
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const contentRef = React.useRef<HTMLDivElement>(null);

  // Enable drop zone for batch mod import
  useDropZone({
    targetRef: contentRef,
    enabled: !!selectedProfileId && (!!selectedCategory || !!selectedObject),
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
        openModManagementScreen();

        // Start batch mod import workflows in background
        // Backend will validate each file/folder and reject invalid ones
        // The workflows will appear in the screen as they are created
        await workflowService.batchStartModImport(
          selectedProfileId,
          files,
          categoryId
        );
      } catch (error) {
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
    // If a Category is selected, use Category-filtered mods
    let result: ModInfo[];
    if (selectedCategory) {
      result = CategoryFilteredMods || [];
    } else {
      result = mods;
    }

    // Filter by selected object (only if no Category is selected)
    if (!selectedCategory && selectedObject && selectedObject !== "all") {
      result = result.filter((mod: ModInfo) => mod.category === selectedObject);
    }

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

    // Add "Unload" option at the beginning if object is selected and has loaded mod
    if (selectedObject && selectedObject !== "all") {
      const hasLoadedMod = result.some((mod: ModInfo) => mod.isLoaded);
      if (hasLoadedMod) {
        const unloadOption: ModInfo = {
          sha: "__UNLOAD__",
          name: "- [X] Unload This Object -",
          category: selectedObject,
          author: "",
          tags: [],
          grading: "",
          description: "",
          disablePreview: false,
          isLoaded: false,
          type: "special",
          isAvailable: true,
          hasCache: false,
          hasPreviewFolder: false,
        };
        result = [unloadOption, ...result];
      }
    }

    return result;
  }, [
    mods,
    CategoryFilteredMods,
    selectedObject,
    selectedCategory,
    searchQuery,
  ]);

  const handleLoadedModClick = (mod: ModInfo) => {
    // Scroll to the loaded mod and select it
    selectMod(mod);

    // Scroll the mod into view
    if (contentRef.current) {
      // Find the mod element by its SHA
      const modElement = contentRef.current.querySelector(
        `[data-mod-sha="${mod.sha}"]`,
      );
      if (modElement) {
        modElement.scrollIntoView({ behavior: "smooth", block: "center" });
      }
    }
  };

  if (!selectedCategory && !selectedObject) {
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
          onClick={() => openModManagementScreen()}
        />
      </div>

      {/* Mod List or Empty State */}
      <div
        className="mod-list-panel-content"
        ref={contentRef}
      >
        <div className="mod-list-panel-drop-message" data-drop-message={t("mods.panel.dropToImport")} />
        <div className="mod-list-panel-content-scrollable">
          {filteredMods.length > 0 ? (
            <ModList
              mods={filteredMods}
              loading={loading}
              onLoad={loadModInGame}
              onUnload={unloadModFromGame}
              onDelete={deleteMod}
              onEdit={openEditDialog}
              onRowClick={selectMod}
              selectedMod={selectedMod}
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
                      : selectedObject
                        ? t("mods.panel.noModsForObject", {
                            object: selectedObject,
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
          selectedCategory={selectedCategory}
          selectedObject={selectedObject}
          onLoadedModClick={handleLoadedModClick}
        />
      </div>
    </Sider>
  );
};
