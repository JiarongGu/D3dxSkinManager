import React, { useState, useCallback, useEffect } from 'react';
import { Space, Spin, Tag, Table, Progress } from 'antd';
import { CheckCircleOutlined, SyncOutlined } from '@ant-design/icons';
import { StatusTag } from '../../../../shared/components/common/StatusTag';
import { toPercent } from '../../../../shared/utils/toPercent';
import { useTranslation } from 'react-i18next';
import type { ColumnsType } from 'antd/es/table';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { api } from '../../../../shared/services/ipc';
import { handleError } from '../../../../shared/utils/errorHandler';
import { notification } from '../../../../shared/utils/notification';
import { eventBus, Module, ToolsEventType } from '../../../../shared/services/eventBus';
import type {
  ModIdMigrationScanResult,
  ModIdMigrationItem,
  ModIdMigrationProgress,
  ModIdMigrationResult,
  ModIdMigrationItemResult,
} from '../../../../shared/types/modIdMigration.types';
import './ModIdMigrationTool.css';
import { CompactButton } from '../../../../shared/components/compact';

interface ModIdMigrationToolProps {
  visible: boolean;
  onClose: () => void;
  onMigrationComplete?: () => void;
}

export const ModIdMigrationTool: React.FC<ModIdMigrationToolProps> = ({
  visible,
  onClose,
  onMigrationComplete,
}) => {
  const { t } = useTranslation();

  const content = <ModIdMigrationToolInner onMigrationComplete={onMigrationComplete} />;

  useSlideInScreen({
    visible,
    title: t('tools.modIdMigration.title'),
    content,
    width: '85%',
    onClose,
  });

  return null;
};

interface InnerProps {
  onMigrationComplete?: () => void;
}

const ModIdMigrationToolInner: React.FC<InnerProps> = ({ onMigrationComplete }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [scanResult, setScanResult] = useState<ModIdMigrationScanResult>();
  const [migrationResult, setMigrationResult] = useState<ModIdMigrationResult>();
  const [progress, setProgress] = useState<ModIdMigrationProgress>();
  const [scanning, setScanning] = useState(false);
  const [migrating, setMigrating] = useState(false);

  // Subscribe to backend events
  useEffect(() => {
    const unsubScanComplete = eventBus.subscribe(
      Module.TOOL,
      ToolsEventType.MOD_ID_MIGRATION_SCAN_COMPLETE,
      (e) => {
        if (!e.payload) return;
        setScanResult(e.payload);
        setScanning(false);
      },
    );

    const unsubProgress = eventBus.subscribe(
      Module.TOOL,
      ToolsEventType.MOD_ID_MIGRATION_PROGRESS,
      (e) => {
        if (!e.payload) return;
        setProgress(e.payload);
      },
    );

    const unsubComplete = eventBus.subscribe(
      Module.TOOL,
      ToolsEventType.MOD_ID_MIGRATION_COMPLETE,
      (e) => {
        if (!e.payload) return;
        setMigrationResult(e.payload);
        setScanResult(undefined);
        setMigrating(false);
        setProgress(undefined);

        if (e.payload.succeeded > 0) {
          notification.success(
            t('tools.modIdMigration.migrationSuccess', {
              count: e.payload.succeeded,
            })
          );
          onMigrationComplete?.();
        }
        if (e.payload.failed > 0) {
          notification.warning(
            t('tools.modIdMigration.migrationPartialFail', {
              failed: e.payload.failed,
            })
          );
        }
      },
    );

    return () => {
      unsubScanComplete();
      unsubProgress();
      unsubComplete();
    };
  }, [t, onMigrationComplete]);

  const scan = useCallback(async () => {
    if (!selectedProfileId) return;

    try {
      setScanning(true);
      setMigrationResult(undefined);
      await api.tool.scanModIdMigration(selectedProfileId);
      // Result arrives via MOD_ID_MIGRATION_SCAN_COMPLETE event
    } catch (error: unknown) {
      setScanning(false);
      handleError(error);
    }
  }, [selectedProfileId]);

  const migrate = useCallback(async () => {
    if (!selectedProfileId) return;

    try {
      setMigrating(true);
      await api.tool.executeModIdMigration(selectedProfileId);
      // Progress via MOD_ID_MIGRATION_PROGRESS, result via MOD_ID_MIGRATION_COMPLETE
    } catch (error: unknown) {
      setMigrating(false);
      handleError(error);
    }
  }, [selectedProfileId, t]);

  // Auto-scan on mount
  useEffect(() => {
    void scan();
  }, [scan]);

  if (scanning && !scanResult) {
    return (
      <div className="mod-id-migration__loading">
        <Spin />
      </div>
    );
  }

  return (
    <div className="mod-id-migration">
      <div className="mod-id-migration__description">
        {t('tools.modIdMigration.description')}
      </div>

      {/* Migration in progress */}
      {migrating && progress && (
        <div className="mod-id-migration__progress">
          <Progress
            percent={toPercent(progress.current, progress.total)}
            size="small"
          />
          <span className="mod-id-migration__progress-text">
            {progress.current}/{progress.total}: {progress.modName}
          </span>
        </div>
      )}

      {/* Scan results */}
      {scanResult && !migrating && (
        <>
          <div className="mod-id-migration__summary">
            <span>
              {t('tools.modIdMigration.totalMods')}: {scanResult.totalMods}
            </span>
            <span className="mod-id-migration__divider">|</span>
            <span>
              {t('tools.modIdMigration.needsMigration')}: {scanResult.modsNeedingMigration}
            </span>
          </div>

          {scanResult.modsNeedingMigration === 0 ? (
            <div className="mod-id-migration__all-good">
              <CheckCircleOutlined className="mod-id-migration__all-good-icon" />
              <span>{t('tools.modIdMigration.allModsGuid')}</span>
            </div>
          ) : (
            <>
              <ScanTable items={scanResult.items} />
              <div className="mod-id-migration__actions">
                <CompactButton
                  type="primary"
                  onClick={migrate}
                  loading={migrating}
                  icon={<SyncOutlined />}
                >
                  {t('tools.modIdMigration.migrateAll')}
                </CompactButton>
                <CompactButton onClick={scan} disabled={migrating}>
                  {t('tools.modIdMigration.rescan')}
                </CompactButton>
              </div>
            </>
          )}
        </>
      )}

      {/* Migration results */}
      {migrationResult && !migrating && (
        <>
          <div className="mod-id-migration__summary">
            <Space size="middle">
              <StatusTag tone="success" label={`${t('tools.modIdMigration.succeeded')}: ${migrationResult.succeeded}`} />
              {migrationResult.failed > 0 && (
                <StatusTag tone="error" label={`${t('tools.modIdMigration.failed')}: ${migrationResult.failed}`} />
              )}
            </Space>
          </div>
          <ResultTable items={migrationResult.results} />
          <div className="mod-id-migration__actions">
            <CompactButton onClick={scan}>
              {t('tools.modIdMigration.rescan')}
            </CompactButton>
          </div>
        </>
      )}
    </div>
  );
};

const ScanTable: React.FC<{ items: ModIdMigrationItem[] }> = ({ items }) => {
  const { t } = useTranslation();

  const columns: ColumnsType<ModIdMigrationItem> = [
    {
      title: t('tools.modIdMigration.columns.modName'),
      dataIndex: 'modName',
      key: 'modName',
      ellipsis: true,
      width: 200,
    },
    {
      title: t('tools.modIdMigration.columns.oldId'),
      dataIndex: 'oldId',
      key: 'oldId',
      ellipsis: true,
      render: (id: string) => <code className="mod-id-migration__id">{id}</code>,
    },
    {
      title: t('tools.modIdMigration.columns.newId'),
      dataIndex: 'newId',
      key: 'newId',
      ellipsis: true,
      render: (id: string) => <code className="mod-id-migration__id">{id}</code>,
    },
    {
      title: t('tools.modIdMigration.columns.artifacts'),
      key: 'artifacts',
      width: 200,
      render: (_: unknown, record: ModIdMigrationItem) => (
        <Space size={4}>
          {record.hasArchive && <Tag>{t('tools.modIdMigration.archive')}</Tag>}
          {record.hasCache && <Tag>{t('tools.modIdMigration.cache')}</Tag>}
          {record.hasPreview && <Tag>{t('tools.modIdMigration.preview')}</Tag>}
        </Space>
      ),
    },
  ];

  return (
    <Table
      dataSource={items}
      columns={columns}
      rowKey="oldId"
      size="small"
      pagination={false}
      scroll={{ y: 400 }}
      className="mod-id-migration__table"
    />
  );
};

const ResultTable: React.FC<{ items: ModIdMigrationItemResult[] }> = ({ items }) => {
  const { t } = useTranslation();

  const columns: ColumnsType<ModIdMigrationItemResult> = [
    {
      title: t('tools.modIdMigration.columns.modName'),
      dataIndex: 'modName',
      key: 'modName',
      ellipsis: true,
      width: 200,
    },
    {
      title: t('tools.modIdMigration.columns.oldId'),
      dataIndex: 'oldId',
      key: 'oldId',
      ellipsis: true,
      render: (id: string) => <code className="mod-id-migration__id">{id}</code>,
    },
    {
      title: t('tools.modIdMigration.columns.newId'),
      dataIndex: 'newId',
      key: 'newId',
      ellipsis: true,
      render: (id: string) => <code className="mod-id-migration__id">{id}</code>,
    },
    {
      title: t('tools.modIdMigration.columns.status'),
      key: 'status',
      width: 120,
      render: (_: unknown, record: ModIdMigrationItemResult) =>
        record.success ? (
          <StatusTag tone="success" label={t('tools.modIdMigration.success')} />
        ) : (
          <StatusTag tone="error" title={record.error} label={t('tools.modIdMigration.error')} />
        ),
    },
  ];

  return (
    <Table
      dataSource={items}
      columns={columns}
      rowKey="oldId"
      size="small"
      pagination={false}
      scroll={{ y: 400 }}
      className="mod-id-migration__table"
    />
  );
};
