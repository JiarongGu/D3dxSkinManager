import React, { useState, useCallback, useEffect } from 'react';
import { Button, Space, Tag, Table, Progress, Radio, Checkbox, Tooltip, Input } from 'antd';
import { CheckCircleOutlined, WarningOutlined, ThunderboltOutlined, FolderOpenOutlined, MinusCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import type { ColumnsType } from 'antd/es/table';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { api } from '../../../../shared/services/ipc';
import { handleError } from '../../../../shared/utils/errorHandler';
import { notification } from '../../../../shared/utils/notification';
import { eventBus, Module, ToolsEventType } from '../../../../shared/services/eventBus';
import type { ModFixProgress, ModFixResult, ModFixItemResult } from '../../../../shared/types/modFix.types';
import './ModFixTool.css';

interface ModFixToolProps {
  visible: boolean;
  onClose: () => void;
  /** When opened from a mod context menu, the pre-selected mod IDs (enables the "Selected" target). */
  initialModIds?: string[];
  onFixComplete?: () => void;
}

export const ModFixTool: React.FC<ModFixToolProps> = ({ visible, onClose, initialModIds, onFixComplete }) => {
  const { t } = useTranslation();

  useSlideInScreen({
    visible,
    title: t('tools.modFix.title'),
    content: <ModFixToolInner initialModIds={initialModIds} onFixComplete={onFixComplete} />,
    width: '85%',
    onClose,
  });

  return null;
};

interface InnerProps {
  initialModIds?: string[];
  onFixComplete?: () => void;
}

const ModFixToolInner: React.FC<InnerProps> = ({ initialModIds, onFixComplete }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const hasSelection = !!initialModIds && initialModIds.length > 0;

  const [scriptPath, setScriptPath] = useState('');
  const [target, setTarget] = useState<'selected' | 'all'>(hasSelection ? 'selected' : 'all');
  const [recompress, setRecompress] = useState(true);
  const [running, setRunning] = useState(false);
  const [progress, setProgress] = useState<ModFixProgress>();
  const [result, setResult] = useState<ModFixResult>();

  useEffect(() => {
    const unsubProgress = eventBus.subscribe(Module.TOOL, ToolsEventType.MOD_FIX_PROGRESS, (e) => {
      if (e.payload) setProgress(e.payload);
    });
    const unsubComplete = eventBus.subscribe(Module.TOOL, ToolsEventType.MOD_FIX_COMPLETE, (e) => {
      if (!e.payload) return;
      setResult(e.payload);
      setRunning(false);
      setProgress(undefined);
      if (e.payload.succeeded > 0) {
        notification.success(t('tools.modFix.fixSuccess', { count: e.payload.succeeded }));
        onFixComplete?.();
      }
      if (e.payload.failed > 0) {
        notification.warning(t('tools.modFix.fixPartialFail', { failed: e.payload.failed }));
      }
    });
    return () => {
      unsubProgress();
      unsubComplete();
    };
  }, [t, onFixComplete]);

  const browse = useCallback(async () => {
    try {
      const res = await api.system.openFileDialog({
        title: t('tools.modFix.pickScript'),
        filters: [
          { name: t('tools.modFix.scriptFilter'), extensions: ['py', 'exe', 'bat', 'cmd'] },
        ],
        rememberPathKey: 'modFixScript',
      });
      if (res.success && res.filePath) setScriptPath(res.filePath);
    } catch (error) {
      handleError(error);
    }
  }, [t]);

  const run = useCallback(async () => {
    if (!selectedProfileId || !scriptPath) return;
    try {
      setRunning(true);
      setResult(undefined);
      setProgress(undefined);
      await api.tool.runModFix(selectedProfileId, {
        scriptPath,
        modIds: target === 'selected' ? initialModIds : [],
        recompress,
      });
      // Progress via MOD_FIX_PROGRESS, result via MOD_FIX_COMPLETE
    } catch (error) {
      setRunning(false);
      handleError(error);
    }
  }, [selectedProfileId, scriptPath, target, initialModIds, recompress]);

  return (
    <div className="mod-fix">
      <div className="mod-fix__description">{t('tools.modFix.description')}</div>

      {/* Script picker */}
      <div className="mod-fix__field">
        <label className="mod-fix__label">{t('tools.modFix.script')}</label>
        <Space.Compact style={{ width: '100%' }}>
          <Input
            value={scriptPath}
            placeholder={t('tools.modFix.scriptPlaceholder')}
            readOnly
            onClick={browse}
          />
          <Button icon={<FolderOpenOutlined />} onClick={browse}>
            {t('tools.modFix.browse')}
          </Button>
        </Space.Compact>
      </div>

      {/* Target */}
      <div className="mod-fix__field">
        <label className="mod-fix__label">{t('tools.modFix.target')}</label>
        <Radio.Group value={target} onChange={(e) => setTarget(e.target.value)} disabled={running}>
          {hasSelection && (
            <Radio value="selected">
              {t('tools.modFix.targetSelected', { count: initialModIds!.length })}
            </Radio>
          )}
          <Radio value="all">{t('tools.modFix.targetAll')}</Radio>
        </Radio.Group>
      </div>

      {/* Options */}
      <div className="mod-fix__field">
        <Checkbox checked={recompress} onChange={(e) => setRecompress(e.target.checked)} disabled={running}>
          {t('tools.modFix.recompress')}
        </Checkbox>
        <div className="mod-fix__hint">{t('tools.modFix.recompressHint')}</div>
      </div>

      {/* Run */}
      <div className="mod-fix__actions">
        <Button
          type="primary"
          icon={<ThunderboltOutlined />}
          onClick={run}
          loading={running}
          disabled={!scriptPath || !selectedProfileId}
        >
          {t('tools.modFix.run')}
        </Button>
      </div>

      {/* Progress */}
      {running && progress && (
        <div className="mod-fix__progress">
          <Progress
            percent={progress.total > 0 ? Math.round((progress.current / progress.total) * 100) : 0}
            size="small"
          />
          <span className="mod-fix__progress-text">
            {progress.current}/{progress.total}: {progress.modName}
          </span>
        </div>
      )}

      {/* Results */}
      {result && !running && (
        <>
          <div className="mod-fix__summary">
            <Space size="middle">
              <Tag color="green">{t('tools.modFix.succeeded')}: {result.succeeded}</Tag>
              {result.failed > 0 && <Tag color="red">{t('tools.modFix.failed')}: {result.failed}</Tag>}
              {result.skipped > 0 && <Tag>{t('tools.modFix.skipped')}: {result.skipped}</Tag>}
              {result.cancelled && <Tag color="orange">{t('tools.modFix.cancelled')}</Tag>}
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
    {
      title: t('tools.modFix.columns.modName'),
      dataIndex: 'modName',
      key: 'modName',
      ellipsis: true,
      width: 220,
    },
    {
      title: t('tools.modFix.columns.status'),
      key: 'status',
      width: 120,
      render: (_: unknown, r: ModFixItemResult) =>
        r.skipped ? (
          <Tag icon={<MinusCircleOutlined />}>{t('tools.modFix.statusSkipped')}</Tag>
        ) : r.success ? (
          <Tag icon={<CheckCircleOutlined />} color="success">{t('tools.modFix.statusOk')}</Tag>
        ) : (
          <Tag icon={<WarningOutlined />} color="error">{t('tools.modFix.statusFailed')}</Tag>
        ),
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

  return (
    <Table
      dataSource={items}
      columns={columns}
      rowKey="modId"
      size="small"
      pagination={false}
      scroll={{ y: 360 }}
      className="mod-fix__table"
    />
  );
};
