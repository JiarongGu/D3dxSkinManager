import React, { useCallback, useEffect, useState } from "react";
import { Progress } from "antd";
import {
  ApiOutlined,
  CloudDownloadOutlined,
  EyeInvisibleOutlined,
  FolderOpenOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import { CompactCard, CompactButton, CompactSwitch } from "../../../shared/components/compact";
import { StatusTag } from "../../../shared/components/common/StatusTag";
import { useTranslation } from "react-i18next";
import { useProfile } from "../../../shared/context/ProfileContext";
import { pluginService, systemService, PluginInfo } from "../../../shared/services/ipc";
import { useProcessStore } from "../../../shared/store/processStore";
import { handleError } from "../../../shared/utils/errorHandler";
import { notification } from "../../../shared/utils/notification";
import "./PluginSettingsTab.css";

/** Icon per capability — a plugin's role is legible at a glance. */
const CAPABILITY_ICON: Record<string, React.ReactNode> = {
  ImageReview: <EyeInvisibleOutlined />,
};

/** Downloadable official packs this UI surfaces (matches PluginInstallService.KnownPacks). */
const AVAILABLE_PACKS: { id: string; icon: React.ReactNode; capability: string }[] = [
  { id: "content-veil-ai", icon: <EyeInvisibleOutlined />, capability: "ImageReview" },
];

/**
 * Plugin management: cards for the plugins loaded in the current profile (generic plugin system —
 * dll packs in {profile}/plugins/) with enable toggles, plus an "available" section that
 * downloads official packs from GitHub with live progress. Loaded packs take effect immediately;
 * removals need a restart (assemblies can't unload).
 */
export const PluginSettingsTab: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [plugins, setPlugins] = useState<PluginInfo[]>();
  const [directory, setDirectory] = useState<string>();
  const [busyId, setBusyId] = useState<string>();

  const load = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      const [list, dir] = await Promise.all([
        pluginService.getAll(selectedProfileId),
        pluginService.getDirectory(selectedProfileId),
      ]);
      setPlugins(Array.isArray(list) ? list : []);
      setDirectory(dir?.path);
    } catch (error) {
      handleError(error);
      setPlugins([]);
    }
  }, [selectedProfileId]);

  useEffect(() => {
    void load();
  }, [load]);

  // Live pack-download process (ProcessRegistry mirror) — drives the progress bar + auto-refresh.
  const packDownload = useProcessStore((s) =>
    s.processes.find((p) => p.titleKey === "process.pluginDownload" && p.status === "running")
  );
  const completedDownloads = useProcessStore((s) =>
    s.processes.filter((p) => p.type === "download" && p.status === "completed").length
  );
  useEffect(() => {
    void load();
  }, [completedDownloads, load]);

  const handleToggle = async (plugin: PluginInfo, enabled: boolean) => {
    if (!selectedProfileId) return;
    setBusyId(plugin.id);
    try {
      await pluginService.setEnabled(selectedProfileId, plugin.id, enabled);
      await load();
    } catch (error) {
      handleError(error);
    } finally {
      setBusyId(undefined);
    }
  };

  const handleDownloadPack = async (packId: string) => {
    if (!selectedProfileId) return;
    try {
      await pluginService.downloadPack(selectedProfileId, packId);
      notification.info(t("settings.plugins.pack.started"));
    } catch (error) {
      handleError(error);
    }
  };

  // An installed official pack maps to its pack id by dropping the "d3dx." plugin-id prefix.
  const packIdOf = (pluginId: string) => pluginId.replace(/^d3dx\./, "");
  const isOfficialPack = (pluginId: string) => AVAILABLE_PACKS.some((p) => p.id === packIdOf(pluginId));

  // Update re-downloads the latest pack. The loaded dll is locked, so it stages and applies on restart.
  const handleUpdatePack = async (packId: string) => {
    if (!selectedProfileId) return;
    try {
      await pluginService.downloadPack(selectedProfileId, packId);
      notification.info(t("settings.plugins.update.staged"));
    } catch (error) {
      handleError(error);
    }
  };

  const handleOpenFolder = async () => {
    if (!directory) return;
    try {
      // The plugins path is a DIRECTORY — open it directly. openFileInExplorer validates File.Exists,
      // which is false for a directory, so it threw "File not found" on the (existing) plugins folder.
      await systemService.openDirectory(directory);
    } catch (error) {
      handleError(error);
    }
  };

  const installedIds = new Set((plugins ?? []).map((p) => p.id));
  const uninstalledPacks = AVAILABLE_PACKS.filter((pack) => !installedIds.has(`d3dx.${pack.id}`));

  return (
    <div className="settings-view-profile">
      <CompactCard
        title={<><ApiOutlined /> {t("settings.plugins.title")}</>}
        extra={
          <div className="plugin-tab__header-actions">
            <CompactButton icon={<FolderOpenOutlined />} onClick={() => void handleOpenFolder()}>
              {t("settings.plugins.folder.open")}
            </CompactButton>
            <CompactButton icon={<ReloadOutlined />} onClick={() => void load()}>
              {t("common.refresh")}
            </CompactButton>
          </div>
        }
      >
        {/* Installed plugins */}
        <div className="plugin-section-label">{t("settings.plugins.loadedSection")}</div>
        {plugins && plugins.length === 0 ? (
          <div className="plugin-empty">
            <ApiOutlined className="plugin-empty__icon" />
            <div className="plugin-empty__title">{t("settings.plugins.empty")}</div>
            <div className="plugin-empty__hint">{t("settings.plugins.emptyHint")}</div>
          </div>
        ) : (
          <div className="plugin-list">
            {(plugins ?? []).map((plugin) => (
              <div key={plugin.id} className={`plugin-card${plugin.isEnabled ? "" : " plugin-card--disabled"}`}>
                <div className="plugin-card__icon">
                  {plugin.capabilities.map((c) => CAPABILITY_ICON[c]).find(Boolean) ?? <ApiOutlined />}
                </div>
                <div className="plugin-card__body">
                  <div className="plugin-card__title-row">
                    <span className="plugin-card__name">{plugin.name}</span>
                    <span className="plugin-card__version">v{plugin.version}</span>
                    {plugin.capabilities.map((capability) => (
                      <StatusTag
                        key={capability}
                        tone="info"
                        icon={null}
                        label={t(`settings.plugins.capability.${capability}`, capability)}
                      />
                    ))}
                  </div>
                  <div className="plugin-card__desc">{plugin.description}</div>
                  <div className="plugin-card__meta">{t("settings.plugins.by", { author: plugin.author })}</div>
                </div>
                <div className="plugin-card__action">
                  {isOfficialPack(plugin.id) && (
                    <CompactButton
                      icon={<CloudDownloadOutlined />}
                      loading={packDownload?.titleArg === packIdOf(plugin.id)}
                      onClick={() => void handleUpdatePack(packIdOf(plugin.id))}
                    >
                      {t("settings.plugins.pack.update")}
                    </CompactButton>
                  )}
                  <CompactSwitch
                    checked={plugin.isEnabled}
                    loading={busyId === plugin.id}
                    onChange={(checked: boolean) => void handleToggle(plugin, checked)}
                    checkedChildren={t("common.enable")}
                    unCheckedChildren={t("common.disable")}
                  />
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Available packs (official downloads) */}
        {uninstalledPacks.length > 0 && (
          <>
            <div className="plugin-section-label">{t("settings.plugins.availableSection")}</div>
            <div className="plugin-list">
              {uninstalledPacks.map((pack) => {
                const downloading = packDownload?.titleArg === pack.id;
                return (
                  <div key={pack.id} className="plugin-card plugin-card--available">
                    <div className="plugin-card__icon">{pack.icon}</div>
                    <div className="plugin-card__body">
                      <div className="plugin-card__title-row">
                        <span className="plugin-card__name">{t(`settings.plugins.pack.${pack.id}.name`)}</span>
                        <StatusTag
                          tone="neutral"
                          icon={null}
                          label={t(`settings.plugins.capability.${pack.capability}`, pack.capability)}
                        />
                      </div>
                      <div className="plugin-card__desc">{t(`settings.plugins.pack.${pack.id}.hint`)}</div>
                      {downloading && (
                        <Progress
                          percent={packDownload?.progress ?? 0}
                          size="small"
                          status="active"
                          className="plugin-card__progress"
                        />
                      )}
                    </div>
                    <div className="plugin-card__action">
                      <CompactButton
                        type="primary"
                        icon={<CloudDownloadOutlined />}
                        loading={downloading}
                        onClick={() => void handleDownloadPack(pack.id)}
                      >
                        {t("settings.plugins.pack.download")}
                      </CompactButton>
                    </div>
                  </div>
                );
              })}
            </div>
          </>
        )}

        <div className="plugin-tab__footer-hint">
          {t("settings.plugins.folder.hint", { path: directory ?? "…" })}
        </div>
      </CompactCard>
    </div>
  );
};
