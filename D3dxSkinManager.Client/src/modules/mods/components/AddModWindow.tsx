import { notification } from '../../../shared/utils/notification';
import React, { useState } from 'react';
import { Modal, Table, Button, Space, Tag, Progress,  Divider } from 'antd';
import { CheckOutlined, CloseOutlined, LoadingOutlined, DeleteOutlined, EditOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../shared/types/mod.types';
import type { ColumnsType } from 'antd/es/table';
import { useTranslation } from 'react-i18next';
import { useModsStore } from '../store/modsStore';
import { useMods } from '../hooks/useMods';
import './AddModWindow.css';

export type TaskStatus = 'pending' | 'processing' | 'success' | 'error' | 'skipped';

export interface ImportTask {
  id: string; // TASK-1, TASK-2, etc.
  filePath: string; // Source file path
  fileName: string; // Display name
  fileType: 'archive' | 'folder'; // Archive (.zip, .rar, .7z) or folder
  status: TaskStatus;
  progress: number; // 0-100
  message?: string; // Status message or error
  modData: Partial<ModInfo>; // Mod properties to import
  thumbnailUrl?: string; // Preview image URL
}

/**
 * Import/Add Mod Window with task queue
 * Displays all pending mod imports in a table format
 * Supports batch operations and task management
 *
 * NEW ARCHITECTURE:
 * - Subscribes to its own state from useModsStore
 * - No props needed - gets everything from store
 */
export const AddModWindow: React.FC = () => {
  // Subscribe to state this component needs
  const visible = useModsStore(s => s.addModWindowVisible);
  const tasks = useModsStore(s => s.importTasks);
  const processing = useModsStore(s => s.importProcessing);

  // Get operations
  const {
    importMods,
    closeAddModWindow,
    openAddModUnit,
    removeImportTask,
    openBatchEditUnit,
  } = useMods();
  const { t } = useTranslation();
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  // Task status icon renderer
  const renderStatusIcon = (status: TaskStatus): React.ReactNode => {
    switch (status) {
      case 'pending':
        return <Tag color="default">{t('importWindow.pending')}</Tag>;
      case 'processing':
        return <Tag icon={<LoadingOutlined />} color="processing">{t('importWindow.processing')}</Tag>;
      case 'success':
        return <Tag icon={<CheckOutlined />} color="success">{t('importWindow.success')}</Tag>;
      case 'error':
        return <Tag icon={<CloseOutlined />} color="error">{t('importWindow.error')}</Tag>;
      case 'skipped':
        return <Tag color="warning">{t('importWindow.skipped')}</Tag>;
      default:
        return <Tag>{t('importWindow.unknown')}</Tag>;
    }
  };

  // Table columns configuration
  const columns: ColumnsType<ImportTask> = [
    {
      title: t('importWindow.taskId'),
      dataIndex: 'id',
      key: 'id',
      width: 100,
      fixed: 'left',
    },
    {
      title: t('importWindow.fileName'),
      dataIndex: 'fileName',
      key: 'fileName',
      width: 200,
      ellipsis: true,
    },
    {
      title: t('importWindow.type'),
      dataIndex: 'fileType',
      key: 'fileType',
      width: 100,
      render: (fileType: string) => (
        <Tag color={fileType === 'archive' ? 'blue' : 'green'}>
          {fileType === 'archive' ? t('importWindow.archive') : t('importWindow.folder')}
        </Tag>
      ),
    },
    {
      title: t('importWindow.modName'),
      key: 'modName',
      width: 180,
      ellipsis: true,
      render: (_, record) => record.modData.name || <span className="add-mod-window-not-set">{t('importWindow.notSet')}</span>,
    },
    {
      title: t('importWindow.category'),
      key: 'category',
      width: 150,
      ellipsis: true,
      render: (_, record) => record.modData.category || <span className="add-mod-window-not-set">{t('importWindow.notSet')}</span>,
    },
    {
      title: t('importWindow.author'),
      key: 'author',
      width: 120,
      ellipsis: true,
      render: (_, record) => record.modData.author || <span className="add-mod-window-not-set">{t('importWindow.notSet')}</span>,
    },
    {
      title: t('importWindow.status'),
      dataIndex: 'status',
      key: 'status',
      width: 120,
      render: renderStatusIcon,
    },
    {
      title: t('importWindow.progress'),
      dataIndex: 'progress',
      key: 'progress',
      width: 120,
      render: (progress: number, record) => {
        if (record.status === 'processing') {
          return <Progress percent={progress} size="small" />;
        }
        if (record.status === 'success') {
          return <Progress percent={100} size="small" status="success" />;
        }
        if (record.status === 'error') {
          return <Progress percent={progress} size="small" status="exception" />;
        }
        return <Progress percent={0} size="small" showInfo={false} />;
      },
    },
    {
      title: t('importWindow.actions'),
      key: 'actions',
      width: 120,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small">
          <Button
            type="text"
            icon={<EditOutlined />}
            size="small"
            onClick={() => openAddModUnit(record)}
            disabled={record.status === 'processing' || record.status === 'success'}
            title={t('importWindow.editTask')}
          />
          <Button
            type="text"
            danger
            icon={<DeleteOutlined />}
            size="small"
            onClick={() => handleRemoveTask(record.id)}
            disabled={record.status === 'processing'}
            title={t('importWindow.removeTask')}
          />
        </Space>
      ),
    },
  ];

  // Row selection configuration
  const rowSelection = {
    selectedRowKeys,
    onChange: (newSelectedRowKeys: React.Key[]) => {
      setSelectedRowKeys(newSelectedRowKeys);
    },
    getCheckboxProps: (record: ImportTask) => ({
      disabled: record.status === 'processing' || record.status === 'success',
    }),
  };

  // Handle remove task
  const handleRemoveTask = (taskId: string) => {
    removeImportTask(taskId);
    // Remove from selection if selected
    setSelectedRowKeys(prev => prev.filter(key => key !== taskId));
  };

  // Handle select all
  const handleSelectAll = () => {
    const selectableKeys = tasks
      .filter(task => task.status !== 'processing' && task.status !== 'success')
      .map(task => task.id);
    setSelectedRowKeys(selectableKeys);
  };

  // Handle clear selection
  const handleClearSelection = () => {
    setSelectedRowKeys([]);
  };

  // Handle batch edit
  const handleBatchEdit = () => {
    if (selectedRowKeys.length === 0) {
      notification.warning(t('importWindow.selectTaskToEdit'));
      return;
    }
    openBatchEditUnit(selectedRowKeys as string[]);
  };

  // Handle confirm
  const handleConfirm = async () => {
    // Validate all tasks have required fields
    const invalidTasks = tasks.filter(task =>
      task.status === 'pending' && (!task.modData.name || !task.modData.category)
    );

    if (invalidTasks.length > 0) {
      notification.error(t('importWindow.missingFields', { count: invalidTasks.length }));
      return;
    }

    const promise = importMods(tasks);
    if (promise) await promise;
  };

  // Calculate statistics
  const stats = {
    total: tasks.length,
    pending: tasks.filter(t => t.status === 'pending').length,
    processing: tasks.filter(t => t.status === 'processing').length,
    success: tasks.filter(t => t.status === 'success').length,
    error: tasks.filter(t => t.status === 'error').length,
  };

  return (
    <Modal
      title={t('importWindow.title')}
      open={visible}
      onCancel={closeAddModWindow}
      width={1200}
      className="add-mod-window-modal"
      footer={[
        <Space key="stats" className="add-mod-window-stats">
          <span>{t('importWindow.total')}: {stats.total}</span>
          <Divider type="vertical" />
          <span>{t('importWindow.pending')}: <Tag color="default">{stats.pending}</Tag></span>
          <span>{t('importWindow.success')}: <Tag color="success">{stats.success}</Tag></span>
          {stats.error > 0 && <span>{t('importWindow.error')}: <Tag color="error">{stats.error}</Tag></span>}
        </Space>,
        <Button key="cancel" onClick={closeAddModWindow} disabled={processing}>
          {t('importWindow.cancel')}
        </Button>,
        <Button
          key="confirm"
          type="primary"
          onClick={handleConfirm}
          loading={processing}
          disabled={tasks.length === 0 || stats.pending === 0}
        >
          {processing ? t('importWindow.processingStatus', { success: stats.success, total: stats.total }) : t('importWindow.import', { count: stats.pending })}
        </Button>,
      ]}
    >
      <Space orientation="vertical" className="add-mod-window-container" size="middle">
        {/* Toolbar */}
        <Space wrap>
          <Button onClick={handleSelectAll} disabled={processing}>
            {t('importWindow.selectAllPending')}
          </Button>
          <Button onClick={handleClearSelection} disabled={processing}>
            {t('importWindow.clearSelection')}
          </Button>
          <Button
            icon={<EditOutlined />}
            onClick={handleBatchEdit}
            disabled={processing || selectedRowKeys.length === 0}
          >
            {t('importWindow.batchEdit', { count: selectedRowKeys.length })}
          </Button>
        </Space>

        {/* Task Queue Table */}
        <Table
          columns={columns}
          dataSource={tasks}
          rowKey="id"
          rowSelection={rowSelection}
          pagination={false}
          scroll={{ x: 1000, y: 400 }}
          size="small"
          bordered
        />

        {/* Status Message */}
        {processing && (
          <div className="add-mod-window-processing">
            <LoadingOutlined className="add-mod-window-processing-icon" />
            {t('importWindow.processingMessage')}
          </div>
        )}
      </Space>
    </Modal>
  );
};
