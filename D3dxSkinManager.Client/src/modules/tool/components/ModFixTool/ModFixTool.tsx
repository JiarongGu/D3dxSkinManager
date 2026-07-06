import React, { useState, useCallback, useEffect, useRef } from 'react';
import { Space, Progress, Popconfirm, Tooltip, Dropdown, Tabs, Collapse } from 'antd';
import {
  ThunderboltOutlined, FolderOpenOutlined, FolderOutlined, FileOutlined,
  FileAddOutlined, DeleteOutlined, EditOutlined, InboxOutlined, DownOutlined,
} from '@ant-design/icons';
import { StatusTag } from '../../../../shared/components/common/StatusTag';
import { DataTable } from '../../../../shared/components/common';
import { FixToolSettingsCard } from './FixToolSettingsCard';
import { FormDialog } from '../../../../shared/components/dialogs/FormDialog';
import { CompactIconButton, CompactInput, CompactSelect, CompactButton } from '../../../../shared/components/compact';
import { useTranslation } from 'react-i18next';
import type { ColumnsType } from 'antd/es/table';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useDropZone } from '../../../../shared/hooks/useDropZone';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { api } from '../../../../shared/services/ipc';
import { handleError } from '../../../../shared/utils/errorHandler';
import { notification } from '../../../../shared/utils/notification';
import { eventBus, Module, ToolsEventType } from '../../../../shared/services/eventBus';
import type { ModFixTool as FixTool, ModFixProgress, ModFixResult, ModFixItemResult } from '../../../../shared/types/modFix.types';
import './ModFixTool.css';

interface ModFixToolProps {
  visible: boolean;
  onClose: () => void;
}

/**
 * Fix Tools manager: maintain the per-profile collection of fix tools (each a folder with a runnable
 * entry), and run one against all mods. Per-mod / per-selection runs are launched from the mod
 * right-click "Fix" submenu; this screen is the library + bulk-run.
 */
export const ModFixTool: React.FC<ModFixToolProps> = ({ visible, onClose }) => {
  const { t } = useTranslation();
  useSlideInScreen({
    visible,
    title: t('tools.modFix.title'),
    content: <ModFixManagerInner />,
    width: '80%',
    onClose,
  });
  return null;
};

const ModFixManagerInner: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [tools, setTools] = useState<FixTool[]>([]);
  const [busy, setBusy] = useState(false);
  const [running, setRunning] = useState(false);
  const [progress, setProgress] = useState<ModFixProgress>();
  const [result, setResult] = useState<ModFixResult>();
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

  // Bulk-run feedback.
  useEffect(() => {
    const unsubP = eventBus.subscribe(Module.TOOL, ToolsEventType.MOD_FIX_PROGRESS, (e) => {
      if (e.payload) setProgress(e.payload);
    });
    const unsubC = eventBus.subscribe(Module.TOOL, ToolsEventType.MOD_FIX_COMPLETE, (e) => {
      if (!e.payload) return;
      setResult(e.payload);
      setRunning(false);
      setProgress(undefined);
      if (e.payload.succeeded > 0) notification.success(t('tools.modFix.fixSuccess', { count: e.payload.succeeded }));
      if (e.payload.failed > 0) notification.warning(t('tools.modFix.fixPartialFail', { failed: e.payload.failed }));
    });
    return () => { unsubP(); unsubC(); };
  }, [t]);

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

  // Run one entry of a toolset against all mods.
  const runEntry = useCallback(async (tool: FixTool, entryPath: string) => {
    if (!selectedProfileId) return;
    try {
      setRunning(true);
      setResult(undefined);
      setProgress(undefined);
      await api.tool.runModFix(selectedProfileId, { scriptPath: entryPath, modIds: [], recompress: tool.recompressDefault });
    } catch (error) {
      setRunning(false);
      handleError(error);
    }
  }, [selectedProfileId]);

  // One fix tool = one card row: [icon] name (+ entry sub-line only when it adds info) … [run][rename][delete].
  // A short list of scripts reads far better as cards than a wide data-grid with duplicate name/entry columns.
  const renderTool = (tool: FixTool) => {
    const isFolder = tool.candidates.length > 0;
    const multi = tool.candidates.length > 1;
    const entryName = tool.entries[0]?.name;
    // Only show the entry when it actually differs from the name (single-file tools have name === entry).
    const showEntry = !multi && !!entryName && entryName !== tool.name;

    const runButton =
      multi ? (
        <Dropdown
          disabled={running || tool.entries.length === 0}
          menu={{ items: tool.entries.map((e) => ({ key: e.path, label: e.name, onClick: () => runEntry(tool, e.path) })) }}
        >
          <CompactButton type="primary" size="small" icon={<ThunderboltOutlined />}>
            {t('tools.modFix.runAll')} <DownOutlined />
          </CompactButton>
        </Dropdown>
      ) : (
        <Tooltip title={tool.entries.length === 0 ? t('tools.modFix.setEntryFirst') : undefined}>
          <CompactButton
            type="primary"
            size="small"
            icon={<ThunderboltOutlined />}
            disabled={running || tool.entries.length === 0}
            onClick={() => tool.entries[0] && runEntry(tool, tool.entries[0].path)}
          >
            {t('tools.modFix.runAll')}
          </CompactButton>
        </Tooltip>
      );

    return (
      <div key={tool.id} className="mod-fix__tool">
        <span className="mod-fix__tool-icon">{isFolder ? <FolderOutlined /> : <FileOutlined />}</span>
        <div className="mod-fix__tool-info">
          <div className="mod-fix__tool-name" title={tool.name}>{tool.name}</div>
          {multi ? (
            // Folder tool with multiple scripts — pick which entries to expose.
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
          {runButton}
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

  // ── Tools tab: add-toolbar → management table → contained run feedback (progress + collapsible
  //    last-run results). Everything about MANAGING and RUNNING tools lives here. ─────────────────
  const addButtons = (
    <div className="mod-fix__add">
      <CompactButton icon={<FolderOpenOutlined />} loading={busy} onClick={() => addFrom(true)}>
        {t('tools.modFix.addFolder')}
      </CompactButton>
      <CompactButton icon={<FileAddOutlined />} loading={busy} onClick={() => addFrom(false)}>
        {t('tools.modFix.addFile')}
      </CompactButton>
    </div>
  );

  const toolsTab = (
    <div className="mod-fix__panel">
      {tools.length === 0 ? (
        // Centered empty CTA — the add actions live here (no floating input); whole screen accepts drop.
        <div className="mod-fix__empty">
          <InboxOutlined className="mod-fix__empty-icon" />
          <span className="mod-fix__empty-text">{t('tools.modFix.empty')}</span>
          {addButtons}
          <span className="mod-fix__drop-hint">{t('tools.modFix.dropHint')}</span>
        </div>
      ) : (
        <>
          {/* Header: add actions right-aligned. Rename/entry-pick happen on the cards themselves. */}
          <div className="mod-fix__panel-header">{addButtons}</div>
          <div className="mod-fix__list">{tools.map(renderTool)}</div>
        </>
      )}

      {/* Bulk-run feedback, contained (not dumped): a slim progress bar while running, then the
          per-mod results tucked into a collapsible panel (auto-open) so they don't crowd the list. */}
      {running && progress && (
        <div className="mod-fix__progress">
          <Progress percent={progress.total > 0 ? Math.round((progress.current / progress.total) * 100) : 0} size="small" />
          <span className="mod-fix__progress-text">{progress.current}/{progress.total}: {progress.modName}</span>
        </div>
      )}
      {result && !running && (
        <Collapse
          className="mod-fix__result"
          defaultActiveKey={['result']}
          items={[{
            key: 'result',
            label: (
              <Space size="small" className="mod-fix__result-summary">
                <span className="mod-fix__result-title">{t('tools.modFix.lastRun')}</span>
                <StatusTag tone="success" label={`${t('tools.modFix.succeeded')}: ${result.succeeded}`} />
                {result.failed > 0 && <StatusTag tone="error" label={`${t('tools.modFix.failed')}: ${result.failed}`} />}
                {result.skipped > 0 && <StatusTag tone="neutral" label={`${t('tools.modFix.skipped')}: ${result.skipped}`} />}
              </Space>
            ),
            children: <ResultTable items={result.results} />,
          }]}
        />
      )}
    </div>
  );

  return (
    <div className="mod-fix" ref={dropRef}>
      <Tabs
        className="mod-fix__tabs"
        items={[
          { key: 'tools', label: t('tools.modFix.tabTools'), children: toolsTab },
          {
            key: 'settings',
            label: t('tools.modFix.tabSettings'),
            children: <div className="mod-fix__settings-view"><FixToolSettingsCard /></div>,
          },
        ]}
      />

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

const ResultTable: React.FC<{ items: ModFixItemResult[] }> = ({ items }) => {
  const { t } = useTranslation();
  const columns: ColumnsType<ModFixItemResult> = [
    { title: t('tools.modFix.columns.modName'), dataIndex: 'modName', key: 'modName', ellipsis: true, width: 220 },
    {
      title: t('tools.modFix.columns.status'),
      key: 'status',
      width: 120,
      render: (_: unknown, r: ModFixItemResult) =>
        r.skipped ? <StatusTag tone="neutral" label={t('tools.modFix.statusSkipped')} />
          : r.success ? <StatusTag tone="success" label={t('tools.modFix.statusOk')} />
          : <StatusTag tone="error" label={t('tools.modFix.statusFailed')} />,
    },
    {
      title: t('tools.modFix.columns.output'),
      key: 'output',
      ellipsis: true,
      render: (_: unknown, r: ModFixItemResult) => {
        const text = r.error || r.output || '';
        return text ? (
          <Tooltip title={<pre className="mod-fix__output-pre">{text}</pre>} overlayStyle={{ maxWidth: 600 }}>
            <code className="mod-fix__output">{text}</code>
          </Tooltip>
        ) : null;
      },
    },
  ];
  return <DataTable dataSource={items} columns={columns} rowKey="modId" compact pagination={false} scroll={{ y: 300 }} className="mod-fix__table" />;
};
