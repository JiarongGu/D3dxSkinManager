import React from "react";
import classNames from "classnames";
import { Tag, Space, Tooltip } from "antd";
import { PlayCircleOutlined, PauseCircleOutlined, EditOutlined, GlobalOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { ModInfo } from "../../../../shared/types/mod.types";
import { GradingTag } from "../GradingTag";
import { StatusTag } from "../../../../shared/components/common/StatusTag";
import { TagChip } from "../../../../shared/components/TagChip";
import { CompactIconButton } from "../../../../shared/components/compact";
import { modNeedsRefix } from "../../../../shared/utils/modFixRef";

export interface ModListItemProps {
  mod: ModInfo;
  /** This mod is the primary (single) selection. */
  isPrimarySelection: boolean;
  /** This mod is part of the multi-selection. */
  isInMultiSelection: boolean;
  /** A background op is running for this mod (spinner/disabled). */
  isBusy: boolean;
  /** Source archive is gone (can't load). */
  isUnavailable: boolean;
  /** The full multi-selection (drag payload when this row is part of it). */
  selectedModIds: string[];
  /** "Game updated" watermark — drives the needs-refix badge. */
  gameUpdatedUtc: string | undefined;
  onRowClick?: (mod: ModInfo, event?: React.MouseEvent) => void;
  onLoad: (id: string) => void;
  onUnload: (id: string) => void;
  onEdit?: (mod: ModInfo) => void;
  onContextMenu: (mod: ModInfo, event: React.MouseEvent) => void;
}

/**
 * One row in the mod list — dumb/presentational (props + i18n only, no store/IPC). Extracted verbatim
 * from ModList's `displayedMods.map` (behavior-preserving) so the list component stays lean and the row
 * is independently testable. The parent owns selection/busy state + the context-menu wiring.
 */
export const ModListItem: React.FC<ModListItemProps> = ({
  mod,
  isPrimarySelection,
  isInMultiSelection,
  isBusy,
  isUnavailable,
  selectedModIds,
  gameUpdatedUtc,
  onRowClick,
  onLoad,
  onUnload,
  onEdit,
  onContextMenu,
}) => {
  const { t } = useTranslation();

  return (
    <div
      data-mod-id={mod.id}
      draggable
      onDragStart={(e) => {
        // If this mod is part of multi-selection, drag all selected mods
        if (isInMultiSelection && selectedModIds.length > 1) {
          e.dataTransfer.setData(
            "application/mod-ids",
            JSON.stringify(selectedModIds),
          );
        } else {
          // Single mod drag
          e.dataTransfer.setData("application/mod-id", mod.id);
        }
        e.dataTransfer.effectAllowed = "move";
      }}
      className={classNames("mod-list-item", {
        "mod-list-item-selected": isPrimarySelection,
        "mod-list-item-multi-selected":
          isInMultiSelection && !isPrimarySelection,
        "mod-list-item--loaded": mod.isLoaded,
        "mod-list-item--unavailable": isUnavailable,
        "mod-list-item--orphaned": mod.isOrphaned,
      })}
      onClick={(e) => {
        onRowClick?.(mod, e);
      }}
      onContextMenu={(e) => {
        e.preventDefault();
        onContextMenu(mod, e);
      }}
      onDoubleClick={() => {
        if (!mod.isLoaded) {
          onLoad(mod.id);
        } else {
          onUnload(mod.id);
        }
      }}
    >
      <div className="mod-list-item-content">
        <div className="mod-list-item-header">
          <span className="mod-list-item-name">
            {mod.isOrphaned ? t('mods.list.unmanaged', { id: mod.name }) : mod.name}
          </span>
          {isBusy && !mod.isLoading && (
            <StatusTag tone="processing" icon={null} className="mod-list-item-loaded-tag" label={t("mods.list.busy")} />
          )}
          {mod.isLoading && (
            <StatusTag tone="warning" icon={null} className="mod-list-item-loaded-tag" label={t("mods.list.loading")} />
          )}
          {mod.isLoaded && !mod.isLoading && !isBusy && (
            <StatusTag tone="success" icon={null} className="mod-list-item-loaded-tag" label={t("mods.list.loaded")} />
          )}
          {isUnavailable && !isBusy && (
            <Tooltip title={t("mods.list.unavailableHint")}>
              <span>
                <StatusTag tone="error" icon={null} className="mod-list-item-loaded-tag" label={t("mods.list.unavailable")} />
              </span>
            </Tooltip>
          )}
          {modNeedsRefix(mod.metadata, gameUpdatedUtc) && (
            <Tooltip title={t("mods.list.needsRefixHint")}>
              <span>
                <StatusTag tone="warning" icon={null} className="mod-list-item-loaded-tag" label={t("mods.list.needsRefix")} />
              </span>
            </Tooltip>
          )}
        </div>
        <Space size={[8, 4]} wrap className="mod-list-item-tags">
          {mod.grading && <GradingTag grading={mod.grading} />}
          {mod.author && mod.author.trim() !== "" && (
            <Tag color="blue" className="mod-list-item-tag">
              {mod.author}
            </Tag>
          )}
          <Tag color="geekblue" className="mod-list-item-tag">
            {mod.categoryName || t("category.unclassified")}
          </Tag>
          {/* Remote-sourced mods: origin library name as a tag beside the category. */}
          {mod.libraryName && (
            <Tag color="cyan" className="mod-list-item-tag" icon={<GlobalOutlined />} title={mod.libraryName}>
              {mod.libraryName}
            </Tag>
          )}
          {mod.tags &&
            mod.tags.slice(0, 3).map((tagName) => {
              // Use pre-loaded tag data if available
              const tagData = mod.tagsWithMetadata?.find(
                (tg) => tg.name === tagName,
              );
              return (
                <TagChip
                  key={tagName}
                  tagName={tagName}
                  tag={tagData}
                  size="small"
                  className="mod-list-item-tag"
                />
              );
            })}
          {mod.tags && mod.tags.length > 3 && (
            <Tag className="mod-list-item-tag" color="default">
              +{mod.tags.length - 3} {t("mods.list.more")}
            </Tag>
          )}
        </Space>
      </div>
      <div className="mod-list-item-actions">
        <CompactIconButton
          tone={mod.isLoaded ? 'success' : 'default'}
          icon={
            mod.isLoaded ? (
              <PauseCircleOutlined className="mod-list-item-action-icon" />
            ) : (
              <PlayCircleOutlined className="mod-list-item-action-icon" />
            )
          }
          onClick={(e) => {
            e.stopPropagation();
            if (mod.isLoaded) {
              onUnload(mod.id);
            } else {
              onLoad(mod.id);
            }
          }}
          title={
            mod.isLoaded
              ? t("mods.list.unloadMod")
              : t("mods.list.loadMod")
          }
          className="mod-list-item-action-button"
        />
        <CompactIconButton
          icon={
            <EditOutlined className="mod-list-item-action-icon" />
          }
          onClick={(e) => {
            e.stopPropagation();
            onEdit?.(mod);
          }}
          title={t("mods.list.editMod")}
          className="mod-list-item-action-button"
        />
      </div>
    </div>
  );
};
