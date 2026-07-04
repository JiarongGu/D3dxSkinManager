import React, { useState, useCallback, useEffect } from "react";
import { Button, Input } from "antd";
import {
  SaveOutlined,
  AppstoreOutlined,
  DeleteOutlined,
  PlayCircleOutlined,
  StopOutlined,
  SyncOutlined,
} from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { modService } from "../../../../shared/services/ipc";
import { handleError } from "../../../../shared/utils/errorHandler";
import { notification } from "../../../../shared/utils/notification";
import {
  ContextMenu,
  ContextMenuItem,
} from "../../../../shared/components/menu/ContextMenu";
import { useContextMenu } from "../../../../shared/components/menu/useContextMenu";
import { FormDialog } from "../../../../shared/components/dialogs/FormDialog";
import { ConfirmDialog } from "../../../../shared/components/dialogs/ConfirmDialog";
import { useEventSubscription } from "../../../../shared/hooks/useEventSubscription";
import { Module, ModEventType } from "../../../../shared/services/eventBus";
import type { ModPresetInfo } from "../../../../shared/types/mod.types";
import "./ModPresetMenu.css";

export const ModPresetMenu: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const menu = useContextMenu();

  const [presets, setPresets] = useState<ModPresetInfo[]>([]);
  const [saveModalVisible, setSaveModalVisible] = useState(false);
  const [presetName, setPresetName] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string }>();
  const [overwriteTarget, setOverwriteTarget] = useState<{ id: string; name: string }>();

  const loadPresets = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      const result = await modService.getPresets(selectedProfileId);
      setPresets(Array.isArray(result) ? result : []);
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId]);

  // Load presets on mount and profile change
  useEffect(() => {
    void loadPresets();
  }, [loadPresets]);

  // Refresh presets when preset events fire
  useEventSubscription(
    Module.MOD,
    ModEventType.PRESET_SAVED,
    () => void loadPresets(),
    [loadPresets],
  );

  useEventSubscription(
    Module.MOD,
    ModEventType.PRESET_DELETED,
    () => void loadPresets(),
    [loadPresets],
  );

  const handleSavePreset = useCallback(async () => {
    if (!selectedProfileId || !presetName.trim()) return;
    const result = await modService.savePreset(
      selectedProfileId,
      presetName.trim(),
    );
    notification.success(
      t("statusBar.presets.saved", { name: result.name }),
    );
    setSaveModalVisible(false);
    setPresetName("");
  }, [selectedProfileId, presetName, t]);

  const handleApplyPreset = useCallback(
    async (presetId: string) => {
      if (!selectedProfileId) return;
      try {
        const result = await modService.applyPreset(
          selectedProfileId,
          presetId,
        );
        notification.success(
          t("statusBar.presets.applied", {
            name: result.presetName,
            loaded: result.loadedCount,
            failed: result.failedCount,
          }),
        );
      } catch (error: unknown) {
        handleError(error);
      }
    },
    [selectedProfileId, t],
  );

  const handleDeletePreset = useCallback(
    (presetId: string, name: string) => {
      setDeleteTarget({ id: presetId, name });
    },
    [],
  );

  const confirmDeletePreset = useCallback(async () => {
    if (!selectedProfileId || !deleteTarget) return;
    await modService.deletePreset(selectedProfileId, deleteTarget.id);
    notification.success(t("statusBar.presets.deleted"));
    setDeleteTarget(undefined);
  }, [selectedProfileId, deleteTarget, t]);

  const confirmOverwritePreset = useCallback(async () => {
    if (!selectedProfileId || !overwriteTarget) return;
    try {
      const result = await modService.overwritePreset(selectedProfileId, overwriteTarget.id);
      notification.success(
        t("statusBar.presets.overwritten", { name: result.name, count: result.modCount }),
      );
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setOverwriteTarget(undefined);
    }
  }, [selectedProfileId, overwriteTarget, t]);

  const handleUnloadAll = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      await modService.unloadAllMods(selectedProfileId);
      notification.success(t("statusBar.presets.unloadedAll"));
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, t]);

  const handleButtonClick = useCallback(
    (e: React.MouseEvent) => {
      menu.show(e);
    },
    [menu],
  );

  // Build context menu items
  const menuItems: ContextMenuItem[] = [
    {
      key: "save",
      label: t("statusBar.presets.save"),
      icon: <SaveOutlined />,
      onClick: () => setSaveModalVisible(true),
    },
    {
      key: "unload-all",
      label: t("statusBar.presets.unloadAll"),
      icon: <StopOutlined />,
      onClick: () => void handleUnloadAll(),
    },
    { type: "divider" },
    // Preset entries
    ...(presets.length === 0
      ? [
          {
            key: "no-presets",
            label: t("statusBar.presets.noPresets"),
            disabled: true,
          },
        ]
      : presets.map((preset) => ({
          key: preset.id,
          label: (
            <div className="mod-preset-menu__item">
              <span className="mod-preset-menu__item-name">
                {preset.name}
              </span>
              <span className="mod-preset-menu__item-count">
                {t("statusBar.presets.modCount", { count: preset.modCount })}
              </span>
              <button
                className="mod-preset-menu__item-update"
                onClick={(e) => {
                  e.stopPropagation();
                  setOverwriteTarget({ id: preset.id, name: preset.name });
                }}
                title={t("statusBar.presets.overwrite")}
              >
                <SyncOutlined />
              </button>
              <button
                className="mod-preset-menu__item-delete"
                onClick={(e) => {
                  e.stopPropagation();
                  handleDeletePreset(preset.id, preset.name);
                }}
                title={t("statusBar.presets.delete")}
              >
                <DeleteOutlined />
              </button>
            </div>
          ),
          icon: <PlayCircleOutlined />,
          onClick: () => void handleApplyPreset(preset.id),
        }))),
  ];

  return (
    <>
      <Button
        type="text"
        size="small"
        className="mod-preset-menu__trigger"
        icon={<AppstoreOutlined />}
        onClick={handleButtonClick}
      >
        {t("statusBar.presets")}
      </Button>

      <ContextMenu
        items={menuItems}
        visible={menu.visible}
        position={menu.position}
        onClose={menu.hide}
      />

      <FormDialog
        visible={saveModalVisible}
        title={t("statusBar.presets.saveTitle")}
        onOk={handleSavePreset}
        onCancel={() => {
          setSaveModalVisible(false);
          setPresetName("");
        }}
        okText={t("common.save")}
        cancelText={t("common.cancel")}
        width={360}
      >
        <Input
          placeholder={t("statusBar.presets.savePrompt")}
          value={presetName}
          onChange={(e) => setPresetName(e.target.value)}
          onPressEnter={() => void handleSavePreset()}
          autoFocus
        />
      </FormDialog>

      <ConfirmDialog
        visible={!!deleteTarget}
        title={t("statusBar.presets.deleteConfirmTitle")}
        content={deleteTarget ? t("statusBar.presets.deleteConfirm", { name: deleteTarget.name }) : ""}
        onOk={confirmDeletePreset}
        onCancel={() => setDeleteTarget(undefined)}
        okText={t("common.delete")}
        cancelText={t("common.cancel")}
        okType="danger"
      />

      <ConfirmDialog
        visible={!!overwriteTarget}
        title={t("statusBar.presets.overwriteConfirmTitle")}
        content={overwriteTarget ? t("statusBar.presets.overwriteConfirm", { name: overwriteTarget.name }) : ""}
        onOk={confirmOverwritePreset}
        onCancel={() => setOverwriteTarget(undefined)}
        okText={t("statusBar.presets.overwrite")}
        cancelText={t("common.cancel")}
      />
    </>
  );
};
