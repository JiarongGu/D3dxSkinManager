import React, { useMemo, useState, useCallback } from "react";
import { Layout, Empty, Spin, Popover, Tag, Input } from 'antd';
import { SearchOutlined, PlusOutlined, QuestionCircleOutlined } from "@ant-design/icons";
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
import { parseSearchQuery, matchesSearchQuery, SearchableRecord, SearchField } from "../../../../shared/utils/searchQueryParser";
import "./ModListPanel.css";
import { CompactButton } from '../../../../shared/components/compact';

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
  const [selectedModIds, setSelectedModIds] = useState<string[]>([]);
  const [anchorId, setAnchorId] = useState<string | undefined>(undefined);

  // Minimum display count passed to ModList so it renders items that are
  // not yet visible (e.g., scrolling to a loaded mod at index > 50)
  const [minDisplayCount, setMinDisplayCount] = useState(0);

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
    className: 'mod-list-panel-drop-active'
  });

  // Build localized field prefixes from i18n (e.g. 标签: → tag, 作者: → author)
  const localizedPrefixes = useMemo(() => ({
    [`${t('mods.search.syntaxTag').split(':')[0]}:`]: 'tag' as SearchField,
    [`${t('mods.search.syntaxAuthor').split(':')[0]}:`]: 'author' as SearchField,
    [`${t('mods.search.syntaxName').split(':')[0]}:`]: 'name' as SearchField,
  }), [t]);

  // Parse search query once (memoized)
  const parsedQuery = useMemo(() => parseSearchQuery(searchQuery, localizedPrefixes), [searchQuery, localizedPrefixes]);

  // Compute filtered mods based on search and Category
  const filteredMods = useMemo(() => {
    // Always use Category-filtered mods (empty array if no category selected)
    const result: ModInfo[] = mods || [];

    // Apply mod search filter using query parser
    if (parsedQuery.isEmpty) return result;

    return result.filter((mod: ModInfo) => {
      const record: SearchableRecord = {
        id: mod.id,
        name: mod.name,
        author: mod.author || undefined,
        tags: mod.tags,
        extra: mod.categoryName ? [mod.categoryName] : undefined,
      };
      return matchesSearchQuery(parsedQuery, record);
    });
  }, [mods, parsedQuery]);

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
        const isSelected = selectedModIds.includes(mod.id);
        if (isSelected) {
          // Remove from selection
          const newSelection = selectedModIds.filter((id) => id !== mod.id);
          setSelectedModIds(newSelection);
          // Update anchor to last remaining item or undefined
          setAnchorId(newSelection.length > 0 ? newSelection[newSelection.length - 1] : undefined);
          // Update primary selection to first item or undefined
          if (newSelection.length > 0) {
            const firstMod = filteredMods.find((m) => m.id === newSelection[0]);
            if (firstMod) selectMod(firstMod);
          } else {
            selectMod(undefined);
          }
        } else {
          // Add to selection
          const newSelection = [...selectedModIds, mod.id];
          setSelectedModIds(newSelection);
          setAnchorId(mod.id);
          // If this is the first selection, set it as primary
          if (selectedModIds.length === 0) {
            selectMod(mod);
          }
        }
      } else if (shiftKey && anchorId) {
        // Shift+Click: Select range from anchor to current
        const anchorIndex = filteredMods.findIndex((m) => m.id === anchorId);
        const currentIndex = filteredMods.findIndex((m) => m.id === mod.id);

        if (anchorIndex !== -1 && currentIndex !== -1) {
          const start = Math.min(anchorIndex, currentIndex);
          const end = Math.max(anchorIndex, currentIndex);
          const rangeSelection = filteredMods
            .slice(start, end + 1)
            .map((m) => m.id);
          setSelectedModIds(rangeSelection);
          // Keep anchor unchanged, primary selection is first in range
          const firstMod = filteredMods.find((m) => m.id === rangeSelection[0]);
          if (firstMod) selectMod(firstMod);
        }
      } else {
        // Regular click: Single selection
        selectMod(mod);
        setSelectedModIds([mod.id]);
        setAnchorId(mod.id);
      }
    },
    [selectedModIds, anchorId, filteredMods, selectMod]
  );

  /**
   * Clear multi-selection when category changes and reset scroll position.
   * Also reset minDisplayCount so lazy loading starts fresh.
   */
  React.useEffect(() => {
    setSelectedModIds([]);
    setAnchorId(undefined);
    resetScrollPosition();
    setMinDisplayCount(0);
  }, [selectedCategory, resetScrollPosition]);

  // Reset minDisplayCount when the filtered list length changes (search/category switch)
  // so stale forced renders don't persist across different mod lists
  const filteredModsLength = filteredMods.length;
  React.useEffect(() => {
    setMinDisplayCount(0);
  }, [filteredModsLength]);

  // #13: keep the selection in sync with what's visible. When a search/category filter hides mods,
  // drop those ids from the selection so bulk actions (apply/load/edit/delete/fix) never act on mods
  // the user can no longer see.
  React.useEffect(() => {
    setSelectedModIds((prev) => {
      if (prev.length === 0) return prev;
      const visible = new Set(filteredMods.map((m) => m.id));
      const pruned = prev.filter((id) => visible.has(id));
      return pruned.length === prev.length ? prev : pruned;
    });
  }, [filteredMods]);

  const handleLoadedModClick = useCallback((mod: ModInfo) => {
    // Clear multi-selection and select only the loaded mod
    setSelectedModIds([mod.id]);
    setAnchorId(mod.id);
    selectMod(mod);

    // Find the mod's index in the filtered list
    const modIndex = filteredMods.findIndex(m => m.id === mod.id);
    if (modIndex === -1) return;

    const existingElement = contentRef.current?.querySelector(`[data-mod-id="${mod.id}"]`);
    if (existingElement) {
      // Already rendered — scroll directly
      existingElement.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }

    // Item is beyond the current render window. Force ModList to render up to
    // (and including) this index via the minDisplayCount prop. The bottom spacer
    // in ModList ensures the scroll container already has the correct total height,
    // so no position estimate is needed — just wait for the render then scroll.
    setMinDisplayCount(modIndex + 1);

    // Double rAF: first frame schedules after React flushes the state update,
    // second frame fires after the browser has painted the new items.
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        const modEl = contentRef.current?.querySelector(`[data-mod-id="${mod.id}"]`);
        modEl?.scrollIntoView({ behavior: "smooth", block: "center" });
      });
    });
  }, [selectMod, filteredMods]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key !== "ArrowUp" && e.key !== "ArrowDown") return;
      if (filteredMods.length === 0) return;

      e.preventDefault(); // prevent page scroll

      const currentIndex = filteredMods.findIndex((m) => m.id === selectedMod?.id);
      const nextIndex =
        e.key === "ArrowUp"
          ? Math.max(0, currentIndex - 1)
          : Math.min(filteredMods.length - 1, currentIndex + 1);

      if (nextIndex === currentIndex) return;

      const nextMod = filteredMods[nextIndex];
      setSelectedModIds([nextMod.id]);
      setAnchorId(nextMod.id);
      handleLoadedModClick(nextMod);
    },
    [filteredMods, selectedMod, handleLoadedModClick]
  );

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
          suffix={
            <Popover
              content={
                <div className="mod-search-help">
                  <table className="mod-search-help__table">
                    <tbody>
                      <tr><td className="mod-search-help__syntax">hair skin</td><td>{t("mods.search.helpAnd")}</td></tr>
                      <tr><td className="mod-search-help__syntax">hair | skin</td><td>{t("mods.search.helpOr")}</td></tr>
                      <tr><td className="mod-search-help__syntax">-nsfw</td><td>{t("mods.search.helpNot")}</td></tr>
                      <tr><td className="mod-search-help__syntax">"blue hair"</td><td>{t("mods.search.helpExact")}</td></tr>
                      <tr><td className="mod-search-help__syntax">{t("mods.search.syntaxTag")}</td><td>{t("mods.search.helpFieldTag")}</td></tr>
                      <tr><td className="mod-search-help__syntax">{t("mods.search.syntaxAuthor")}</td><td>{t("mods.search.helpFieldAuthor")}</td></tr>
                      <tr><td className="mod-search-help__syntax">{t("mods.search.syntaxName")}</td><td>{t("mods.search.helpFieldName")}</td></tr>
                    </tbody>
                  </table>
                </div>
              }
              title={t("mods.search.helpTitle")}
              trigger="click"
              placement="bottomRight"
            >
              <QuestionCircleOutlined className="mod-list-panel-search-help-icon" />
            </Popover>
          }
        />
        <CompactButton
          type="default"
          icon={<PlusOutlined />}
          onClick={() => openImportWorkflowScreen()}
        />
      </div>

      {/* Active-filter status — shows why mods are hidden + a one-click clear */}
      {!parsedQuery.isEmpty && (
        <div className="mod-list-panel-filter-status">
          <span className="mod-list-panel-filter-status__count">
            {t("mods.list.showingCount", { shown: filteredMods.length, total: (mods || []).length })}
          </span>
          <Tag closable onClose={() => setSearchQuery("")} className="mod-list-panel-filter-status__chip">
            {searchQuery}
          </Tag>
        </div>
      )}

      {/* Mod List or Empty State */}
      <div
        className="mod-list-panel-content"
        ref={contentRef}
      >
        <div className="mod-list-panel-drop-message" data-drop-message={t("mods.panel.dropToImport")} />
        <div className="mod-list-panel-content-scrollable" ref={scrollRef} tabIndex={0} onKeyDown={handleKeyDown}>
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
              selectedModIds={selectedModIds}
              onClearSelection={() => setSelectedModIds([])}
              onBeforeReload={saveScrollPosition}
              onAfterReload={restoreScrollPosition}
              minDisplayCount={minDisplayCount}
            />
          ) : (
            <div className="mod-list-panel-content-empty-container">
              {loading && (
                <div className="mod-list-loading-overlay">
                  <Spin size="large" />
                </div>
              )}
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
          mods={mods ?? []}
          onLoadedModClick={handleLoadedModClick}
          selectedModCount={selectedModIds.length}
        />
      </div>
    </Sider>
  );
};
