import React, { useState, useCallback, useEffect, useRef } from 'react';
import { Popconfirm, Tooltip, Collapse } from 'antd';
import {
  FolderOpenOutlined, FolderOutlined, FileOutlined,
  FileAddOutlined, DeleteOutlined, EditOutlined, InboxOutlined, SettingOutlined,
} from '@ant-design/icons';
import { FixToolSettingsCard } from './FixToolSettingsCard';
import { FormDialog } from '../../../../shared/components/dialogs/FormDialog';
import { CompactIconButton, CompactInput, CompactSelect, CompactButton } from '../../../../shared/components/compact';
import { useTranslation } from 'react-i18next';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useDropZone } from '../../../../shared/hooks/useDropZone';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { api } from '../../../../shared/services/ipc';
import { handleError } from '../../../../shared/utils/errorHandler';
import { notification } from '../../../../shared/utils/notification';
import { eventBus, Module, ToolsEventType } from '../../../../shared/services/eventBus';
import type { ModFixTool as FixTool } from '../../../../shared/types/modFix.types';
import './ModFixTool.css';

interface ModFixToolProps {
  visible: boolean;
  onClose: () => void;
  /** Compact = the narrow context view (opened from a mod's right-click "Manage fix tools"): just the
   * tool list, no settings. Default (full) = the Tools-grid setup: list + settings, wider panel. */
  compact?: boolean;
}

/**
 * Fix Tools LIBRARY MANAGER — maintain the per-profile collection of fix tools (each a folder/file
 * with a runnable entry): add, rename, delete, choose which entries a multi-script folder exposes.
 * It does NOT run fixes — per-mod / per-selection runs are launched from the mod right-click "Fix"
 * submenu (ModList), which lists these tools. Two modes: `compact` (right-click) vs full (Tools grid).
 */
export const ModFixTool: React.FC<ModFixToolProps> = ({ visible, onClose, compact = false }) => {
  const { t } = useTranslation();
  useSlideInScreen({
    visible,
    title: t('tools.modFix.title'),
    content: <ModFixManagerInner compact={compact} />,
    // Focused panel (see ModFixTool.css): compact = 560px list-only; full = 720px + settings.
    className: compact ? 'mod-fix-screen' : 'mod-fix-screen mod-fix-screen--full',
    onClose,
  });
  return null;
};

const ModFixManagerInner: React.FC<{ compact: boolean }> = ({ compact }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [tools, setTools] = useState<FixTool[]>([]);
  const [busy, setBusy] = useState(false);
  const [renaming, setRenaming] = useState<FixTool>();
  const [renameValue, setRenameValue] = useState('');

  const load = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      setTools(await api.tool.getFixTools(selectedProfileId));
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId]);

  useEffect(() => { void load(); }, [load]);

  // Live refresh when the fixtools/ folder changes on disk (watcher).
  useEffect(() => {
    return eventBus.subscribe(Module.TOOL, ToolsEventType.FIX_TOOLS_CHANGED, () => { void load(); });
  }, [load]);

  const addFrom = useCallback(async (isFolder: boolean) => {
    if (!selectedProfileId) return;
    try {
      const res = isFolder
        ? await api.system.openFolderDialog({ title: t('tools.modFix.pickFolder'), rememberPathKey: 'fixToolSrc' })
        : await api.system.openFileDialog({
            title: t('tools.modFix.pickScript'),
            filters: [{ name: t('tools.modFix.scriptFilter'), extensions: ['py', 'exe', 'bat', 'cmd'] }],
            rememberPathKey: 'fixToolSrc',
          });
      if (!res.success || !res.filePath) return;
      const sourcePath = res.filePath;
      const name = sourcePath.replace(/[/\\]+$/, '').split(/[/\\]/).pop() || 'Fix';
      setBusy(true);
      await api.tool.importFixTool(selectedProfileId, { name, sourcePath, isFolder });
      notification.success(t('tools.modFix.added', { name }));
      await load();
    } catch (error) {
      handleError(error);
    } finally {
      setBusy(false);
    }
  }, [selectedProfileId, t, load]);

  // Drag-drop import: drop file(s)/folder(s) anywhere on the manager to add them as fix tools.
  const dropRef = useRef<HTMLDivElement>(null);
  const handleDrop = useCallback(async (files: string[]) => {
    if (!selectedProfileId || files.length === 0) return;
    setBusy(true);
    try {
      for (const p of files) {
        const base = p.replace(/[/\\]+$/, '').split(/[/\\]/).pop() || 'Fix';
        const name = base.replace(/\.[^.]+$/, '') || base;
        await api.tool.importFixTool(selectedProfileId, { name, sourcePath: p, isFolder: false });
      }
      notification.success(
        files.length === 1
          ? t('tools.modFix.added', { name: files[0].replace(/[/\\]+$/, '').split(/[/\\]/).pop() })
          : t('tools.modFix.addedMultiple', { count: files.length }),
      );
      await load();
    } catch (error) {
      handleError(error);
    } finally {
      setBusy(false);
    }
  }, [selectedProfileId, t, load]);
  useDropZone({ targetRef: dropRef, onDrop: handleDrop });

  const remove = useCallback(async (tool: FixTool) => {
    if (!selectedProfileId) return;
    try {
      await api.tool.deleteFixTool(selectedProfileId, tool.id);
      await load();
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId, load]);

  const rename = useCallback(async () => {
    if (!selectedProfileId || !renaming) return;
    const newName = renameValue.trim();
    if (!newName || newName === renaming.name) { setRenaming(undefined); return; }
    try {
      await api.tool.renameFixTool(selectedProfileId, renaming.id, newName);
      setRenaming(undefined);
      await load();
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId, renaming, renameValue, load]);

  // Only folder-based tools can be renamed (a loose single-file tool is named by its file).
  const canRename = (tool: FixTool) => tool.candidates.length > 0;

  const setEntries = useCallback(async (tool: FixTool, entries: string[]) => {
    if (!selectedProfileId) return;
    try {
      await api.tool.setFixToolEntries(selectedProfileId, tool.id, entries);
      await load();
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId, load]);

  // One fix tool = one card row: [icon] name (+ entry sub-line / entry picker) … [rename][delete].
  // No run button — fixing a mod is launched from the mod right-click "Fix" submenu, not here.
  const renderTool = (tool: FixTool) => {
    const isFolder = tool.candidates.length > 0;
    const multi = tool.candidates.length > 1;
    const entryName = tool.entries[0]?.name;
    // Only show the entry when it actually differs from the name (single-file tools have name === entry).
    const showEntry = !multi && !!entryName && entryName !== tool.name;

    return (
      <div key={tool.id} className="mod-fix__tool">
        <span className="mod-fix__tool-icon">{isFolder ? <FolderOutlined /> : <FileOutlined />}</span>
        <div className="mod-fix__tool-info">
          <div className="mod-fix__tool-name" title={tool.name}>{tool.name}</div>
          {multi ? (
            // Folder tool with multiple scripts — pick which entries the right-click menu exposes.
            <CompactSelect<string[]>
              mode="multiple"
              size="small"
              className="mod-fix__tool-entries"
              placeholder={t('tools.modFix.selectEntry')}
              value={tool.entries.map((e) => e.name)}
              options={tool.candidates.map((c) => ({ label: c, value: c }))}
              onChange={(v) => void setEntries(tool, v)}
              notFoundContent={t('tools.modFix.noCandidates')}
              status={tool.entries.length === 0 ? 'warning' : undefined}
            />
          ) : showEntry ? (
            <code className="mod-fix__tool-entry">{entryName}</code>
          ) : null}
        </div>
        <div className="mod-fix__tool-actions">
          {canRename(tool) && (
            <Tooltip title={t('tools.modFix.rename')}>
              <CompactIconButton icon={<EditOutlined />} onClick={() => { setRenaming(tool); setRenameValue(tool.name); }} />
            </Tooltip>
          )}
          <Popconfirm title={t('tools.modFix.deleteConfirm')} onConfirm={() => remove(tool)} okText={t('common.delete')} cancelText={t('common.cancel')}>
            <Tooltip title={t('common.delete')}>
              <CompactIconButton tone="danger" icon={<DeleteOutlined />} />
            </Tooltip>
          </Popconfirm>
        </div>
      </div>
    );
  };

  const addButtons = (
    <div className="mod-fix__add">
      <CompactButton size="small" icon={<FolderOpenOutlined />} loading={busy} onClick={() => addFrom(true)}>
        {t('tools.modFix.addFolder')}
      </CompactButton>
      <CompactButton size="small" icon={<FileAddOutlined />} loading={busy} onClick={() => addFrom(false)}>
        {t('tools.modFix.addFile')}
      </CompactButton>
    </div>
  );

  return (
    <div className="mod-fix" ref={dropRef}>
      {/* Header: one-line purpose + add actions on the SAME row. */}
      <div className="mod-fix__header">
        <p className="mod-fix__desc">{t('tools.modFix.headerHint')}</p>
        {addButtons}
      </div>

      {/* Card list (or a centered empty CTA). The whole panel accepts drag-drop. */}
      {tools.length === 0 ? (
        <div className="mod-fix__empty">
          <InboxOutlined className="mod-fix__empty-icon" />
          <span className="mod-fix__empty-text">{t('tools.modFix.empty')}</span>
          {addButtons}
          <span className="mod-fix__drop-hint">{t('tools.modFix.dropHint')}</span>
        </div>
      ) : (
        <div className="mod-fix__list">{tools.map(renderTool)}</div>
      )}

      {/* Settings — only in the full (Tools-grid) view; the compact context view is list-only.
          Collapsible (click the label to open/close, no arrow), open by default. */}
      {!compact && (
        <Collapse
          ghost
          className="mod-fix__settings"
          expandIcon={() => null}
          defaultActiveKey={['settings']}
          items={[{
            key: 'settings',
            label: <span className="mod-fix__section-label"><SettingOutlined /> {t('tools.modFix.tabSettings')}</span>,
            children: <FixToolSettingsCard />,
          }]}
        />
      )}

      <FormDialog
        visible={!!renaming}
        title={t('tools.modFix.rename')}
        onCancel={() => setRenaming(undefined)}
        onOk={rename}
        width={420}
      >
        <CompactInput
          autoFocus
          value={renameValue}
          placeholder={t('tools.modFix.namePlaceholder')}
          onChange={(e) => setRenameValue(e.target.value)}
          onPressEnter={() => void rename()}
        />
      </FormDialog>
    </div>
  );
};
