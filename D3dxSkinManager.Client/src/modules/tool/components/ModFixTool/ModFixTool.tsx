import React, { useState, useCallback, useEffect } from 'react';
import { Button, Space, Tag, Table, Progress, Input, Empty, Popconfirm, Tooltip, Select, Dropdown } from 'antd';
import {
  CheckCircleOutlined, WarningOutlined, ThunderboltOutlined, FolderOpenOutlined,
  FileAddOutlined, DeleteOutlined, MinusCircleOutlined, DownOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import type { ColumnsType } from 'antd/es/table';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
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
  const [newName, setNewName] = useState('');
  const [busy, setBusy] = useState(false);
  const [running, setRunning] = useState(false);
  const [progress, setProgress] = useState<ModFixProgress>();
  const [result, setResult] = useState<ModFixResult>();

  const load = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      setTools(await api.tool.getFixTools(selectedProfileId));
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId]);

  useEffect(() => { void load(); }, [load]);

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
      const name = newName.trim() || sourcePath.replace(/[/\\]+$/, '').split(/[/\\]/).pop() || 'Fix';
      setBusy(true);
      await api.tool.importFixTool(selectedProfileId, { name, sourcePath, isFolder });
      notification.success(t('tools.modFix.added', { name }));
      setNewName('');
      await load();
    } catch (error) {
      handleError(error);
    } finally {
      setBusy(false);
    }
  }, [selectedProfileId, newName, t, load]);

  const remove = useCallback(async (tool: FixTool) => {
    if (!selectedProfileId) return;
    try {
      await api.tool.deleteFixTool(selectedProfileId, tool.id);
      await load();
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId, load]);

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

  const columns: ColumnsType<FixTool> = [
    { title: t('tools.modFix.columns.name'), dataIndex: 'name', key: 'name', ellipsis: true },
    {
      title: t('tools.modFix.columns.entry'),
      key: 'entries',
      render: (_: unknown, tool: FixTool) =>
        tool.candidates.length === 0 ? (
          // Loose single-file tool — nothing to choose.
          <code className="mod-fix__output">{tool.entries[0]?.name}</code>
        ) : (
          // Folder tool — pick one or MORE entries to expose.
          <Select<string[]>
            mode="multiple"
            size="small"
            placeholder={t('tools.modFix.selectEntry')}
            style={{ minWidth: 240 }}
            value={tool.entries.map((e) => e.name)}
            options={tool.candidates.map((c) => ({ label: c, value: c }))}
            onChange={(v) => void setEntries(tool, v)}
            notFoundContent={t('tools.modFix.noCandidates')}
            status={tool.entries.length === 0 ? 'warning' : undefined}
          />
        ),
    },
    {
      title: '',
      key: 'actions',
      width: 200,
      render: (_: unknown, tool: FixTool) => (
        <Space size={4}>
          {tool.entries.length <= 1 ? (
            <Tooltip title={tool.entries.length === 0 ? t('tools.modFix.setEntryFirst') : ''}>
              <Button
                size="small"
                icon={<ThunderboltOutlined />}
                onClick={() => tool.entries[0] && runEntry(tool, tool.entries[0].path)}
                disabled={running || tool.entries.length === 0}
              >
                {t('tools.modFix.runAll')}
              </Button>
            </Tooltip>
          ) : (
            <Dropdown
              disabled={running}
              menu={{ items: tool.entries.map((e) => ({ key: e.path, label: e.name, onClick: () => runEntry(tool, e.path) })) }}
            >
              <Button size="small" icon={<ThunderboltOutlined />}>
                {t('tools.modFix.runAll')} <DownOutlined />
              </Button>
            </Dropdown>
          )}
          <Popconfirm title={t('tools.modFix.deleteConfirm')} onConfirm={() => remove(tool)} okText={t('common.delete')} cancelText={t('common.cancel')}>
            <Button size="small" type="text" icon={<DeleteOutlined style={{ color: 'var(--color-error)' }} />} />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div className="mod-fix">
      <div className="mod-fix__description">{t('tools.modFix.description')}</div>

      {/* Add a fix tool */}
      <div className="mod-fix__field">
        <label className="mod-fix__label">{t('tools.modFix.addTool')}</label>
        <Space.Compact style={{ width: '100%' }}>
          <Input
            value={newName}
            placeholder={t('tools.modFix.namePlaceholder')}
            onChange={(e) => setNewName(e.target.value)}
            disabled={busy}
          />
          <Button icon={<FolderOpenOutlined />} loading={busy} onClick={() => addFrom(true)}>
            {t('tools.modFix.addFolder')}
          </Button>
          <Button icon={<FileAddOutlined />} loading={busy} onClick={() => addFrom(false)}>
            {t('tools.modFix.addFile')}
          </Button>
        </Space.Compact>
        <div className="mod-fix__hint">{t('tools.modFix.addHint')}</div>
      </div>

      {/* Library */}
      {tools.length === 0 ? (
        <Empty description={t('tools.modFix.empty')} image={Empty.PRESENTED_IMAGE_SIMPLE} />
      ) : (
        <Table dataSource={tools} columns={columns} rowKey="id" size="small" pagination={false} className="mod-fix__table" />
      )}

      {/* Bulk-run feedback */}
      {running && progress && (
        <div className="mod-fix__progress">
          <Progress percent={progress.total > 0 ? Math.round((progress.current / progress.total) * 100) : 0} size="small" />
          <span className="mod-fix__progress-text">{progress.current}/{progress.total}: {progress.modName}</span>
        </div>
      )}
      {result && !running && (
        <>
          <div className="mod-fix__summary">
            <Space size="middle">
              <Tag color="green">{t('tools.modFix.succeeded')}: {result.succeeded}</Tag>
              {result.failed > 0 && <Tag color="red">{t('tools.modFix.failed')}: {result.failed}</Tag>}
              {result.skipped > 0 && <Tag>{t('tools.modFix.skipped')}: {result.skipped}</Tag>}
            </Space>
          </div>
          <ResultTable items={result.results} />
        </>
      )}
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
        r.skipped ? <Tag icon={<MinusCircleOutlined />}>{t('tools.modFix.statusSkipped')}</Tag>
          : r.success ? <Tag icon={<CheckCircleOutlined />} color="success">{t('tools.modFix.statusOk')}</Tag>
          : <Tag icon={<WarningOutlined />} color="error">{t('tools.modFix.statusFailed')}</Tag>,
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
  return <Table dataSource={items} columns={columns} rowKey="modId" size="small" pagination={false} scroll={{ y: 300 }} className="mod-fix__table" />;
};
