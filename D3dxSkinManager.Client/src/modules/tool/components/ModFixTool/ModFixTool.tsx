import React, { useState, useCallback, useEffect, useRef } from 'react';
import { Tooltip } from 'antd';
import {
  FolderOpenOutlined, FolderOutlined, FileOutlined, CheckOutlined, CloseOutlined,
  FileAddOutlined, DeleteOutlined, EditOutlined, InboxOutlined,
} from '@ant-design/icons';
import { FixToolSettingsCard } from './FixToolSettingsCard';
import { CompactIconButton, CompactInput, CompactButton, CompactSwitch, CompactCheckbox } from '../../../../shared/components/compact';
import { ConfirmDialog } from '../../../../shared/components/dialogs/ConfirmDialog';
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
    // compact (right-click) = a narrow 560px focused panel (see .mod-fix-screen in ModFixTool.css).
    // full (Tools grid) = a full-width slide-in like every other tool. Both make the content a flex
    // column (mod-fix-screen / mod-fix-full) so the tool grid fills + scrolls and the settings block
    // pins to the bottom of the panel.
    className: compact ? 'mod-fix-screen' : 'mod-fix-full',
    onClose,
  });
  return null;
};

const ModFixManagerInner: React.FC<{ compact: boolean }> = ({ compact }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [tools, setTools] = useState<FixTool[]>([]);
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState<FixTool>();
  // Edit mode: a card is READ-ONLY until its edit button is clicked. Edits (toolset name + which
  // scripts are entries + each entry's friendly name) are held as a draft and only written on Save.
  const [editingId, setEditingId] = useState<string>();
  const [draftName, setDraftName] = useState('');
  const [draftEntries, setDraftEntries] = useState<string[]>([]);
  const [draftAliases, setDraftAliases] = useState<Record<string, string>>({});

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

  // Only folder-based tools are editable (a loose single-file tool is named by its file, one entry).
  const canRename = (tool: FixTool) => tool.candidates.length > 0;

  // Enter edit mode — seed the draft from the tool. The card is read-only until this is called.
  const startEdit = (tool: FixTool) => {
    setEditingId(tool.id);
    setDraftName(tool.name);
    setDraftEntries(tool.entries.map((e) => e.name));
    const aliases: Record<string, string> = {};
    tool.entries.forEach((e) => { if (e.displayName) aliases[e.name] = e.displayName; });
    setDraftAliases(aliases);
  };

  const cancelEdit = () => { setEditingId(undefined); setDraftName(''); setDraftEntries([]); setDraftAliases({}); };

  const setEnabled = useCallback(async (tool: FixTool, enabled: boolean) => {
    if (!selectedProfileId) return;
    try {
      await api.tool.setFixToolEnabled(selectedProfileId, tool.id, enabled);
      await load();
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId, load]);

  // Save all edits for one card at once: rename the toolset (a folder rename changes its id), then set
  // which scripts are entries + each entry's alias against the (possibly new) id.
  const saveEdit = useCallback(async (tool: FixTool) => {
    if (!selectedProfileId) return;
    let id = tool.id;
    try {
      if (tool.candidates.length > 0) {
        const newName = draftName.trim();
        if (newName && newName !== tool.name) {
          const res = await api.tool.renameFixTool(selectedProfileId, id, newName);
          id = res.id;
        }
        await api.tool.setFixToolEntries(selectedProfileId, id, draftEntries);
        for (const name of draftEntries) {
          await api.tool.setFixToolEntryAlias(selectedProfileId, id, name, (draftAliases[name] ?? '').trim());
        }
      }
      setEditingId(undefined);
      await load();
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId, draftName, draftEntries, draftAliases, load]);

  // One fix tool = one full-width ROW card. READ-ONLY by default (name + entry summary + toggle/edit/
  // delete). Clicking edit (folder tools only) switches THAT card into edit mode — name input + entry
  // picker + per-entry alias inputs — committed together on Save. No run button (a fix is launched from
  // the mod right-click "Fix" submenu).
  const renderCard = (tool: FixTool) => {
    const isFolder = tool.candidates.length > 0;
    const enabled = tool.enabled !== false;
    const editing = editingId === tool.id;
    // Read-only summary of the runnable entries (their friendly names, or filenames).
    const entrySummary = tool.entries.map((e) => e.displayName || e.name).join(', ');

    return (
      <div key={tool.id} className={`mod-fix__card${enabled ? '' : ' mod-fix__card--disabled'}`}>
        {/* Row: [icon] name (+ entry summary) … [toggle][edit][delete], or the edit input + save/cancel. */}
        <div className="mod-fix__card-top">
          <span className="mod-fix__card-icon">{isFolder ? <FolderOutlined /> : <FileOutlined />}</span>
          {editing ? (
            <CompactInput
              className="mod-fix__card-rename"
              autoFocus
              value={draftName}
              placeholder={t('tools.modFix.namePlaceholder')}
              onChange={(e) => setDraftName(e.target.value)}
              onPressEnter={() => void saveEdit(tool)}
              onKeyDown={(e) => { if (e.key === 'Escape') cancelEdit(); }}
            />
          ) : (
            <>
              <div className="mod-fix__card-name" title={tool.name}>{tool.name}</div>
              {entrySummary ? (
                <span className="mod-fix__card-entry" title={entrySummary}>{entrySummary}</span>
              ) : isFolder ? (
                <span className="mod-fix__card-entry mod-fix__card-entry--warn">{t('tools.modFix.setEntryFirst')}</span>
              ) : null}
            </>
          )}
          <div className="mod-fix__card-actions">
            {editing ? (
              <>
                <Tooltip title={t('common.save')}>
                  <CompactIconButton tone="success" icon={<CheckOutlined />} onClick={() => void saveEdit(tool)} />
                </Tooltip>
                <Tooltip title={t('common.cancel')}>
                  <CompactIconButton icon={<CloseOutlined />} onClick={cancelEdit} />
                </Tooltip>
              </>
            ) : (
              <>
                {/* Turn a tool off without removing it (hidden from the mod Fix menu when off). */}
                <Tooltip title={enabled ? t('common.disable') : t('common.enable')}>
                  <span className="mod-fix__card-toggle">
                    <CompactSwitch size="small" checked={enabled} onChange={(v) => void setEnabled(tool, v)} />
                  </span>
                </Tooltip>
                {canRename(tool) && (
                  <Tooltip title={t('common.edit')}>
                    <CompactIconButton icon={<EditOutlined />} onClick={() => startEdit(tool)} />
                  </Tooltip>
                )}
                <Tooltip title={t('common.delete')}>
                  <CompactIconButton tone="danger" icon={<DeleteOutlined />} onClick={() => setDeleting(tool)} />
                </Tooltip>
              </>
            )}
          </div>
        </div>

        {/* Edit mode (folder tools): one row per candidate script — a checkbox (is it an entry?) + its
            filename + a friendly-name input. A checkbox list (not a multi-select) keeps every control the
            same height/width and consistent with the name input above. */}
        {editing && isFolder && (
          <div className="mod-fix__card-body">
            {tool.candidates.map((c) => {
              const selected = draftEntries.includes(c);
              return (
                <div className="mod-fix__entry-row" key={c}>
                  <CompactCheckbox
                    checked={selected}
                    onChange={(e) =>
                      setDraftEntries((prev) => (e.target.checked ? [...prev, c] : prev.filter((x) => x !== c)))
                    }
                  />
                  <span className="mod-fix__entry-file" title={c}>{c}</span>
                  <CompactInput
                    className="mod-fix__entry-alias"
                    value={draftAliases[c] ?? ''}
                    placeholder={t('tools.modFix.namePlaceholder')}
                    disabled={!selected}
                    onChange={(ev) => setDraftAliases((prev) => ({ ...prev, [c]: ev.target.value }))}
                  />
                </div>
              );
            })}
          </div>
        )}
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
        <div className="mod-fix__list">{tools.map(renderCard)}</div>
      )}

      {/* Config — only in the full (Tools-grid) view; the compact context view is list-only. Just the
          config card (no collapse, no section title), pinned to the panel bottom. */}
      {!compact && (
        <div className="mod-fix__settings">
          <FixToolSettingsCard />
        </div>
      )}

      {/* Delete confirmation — the shared app dialog (not a native antd Popconfirm). */}
      <ConfirmDialog
        visible={!!deleting}
        title={t('common.delete')}
        content={<>{t('tools.modFix.deleteConfirm')}<div className="mod-fix__delete-name">{deleting?.name}</div></>}
        okText={t('common.delete')}
        okType="danger"
        onOk={async () => { const target = deleting; setDeleting(undefined); if (target) await remove(target); }}
        onCancel={() => setDeleting(undefined)}
      />
    </div>
  );
};
