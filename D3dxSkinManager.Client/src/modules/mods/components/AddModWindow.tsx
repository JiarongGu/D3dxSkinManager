import { notification } from '../../../shared/utils/notification';
import React, { useState } from 'react';
import { Modal, Table, Button, Space, Tag, Progress,  Divider } from 'antd';
import { CheckOutlined, CloseOutlined, LoadingOutlined, DeleteOutlined, EditOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../shared/types/mod.types';
import type { ColumnsType } from 'antd/es/table';

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

interface AddModWindowProps {
  visible: boolean;
  tasks: ImportTask[];
  onConfirm: (tasks: ImportTask[]) => Promise<void>;
  onCancel: () => void;
  onEditTask: (task: ImportTask) => void;
  onRemoveTask: (taskId: string) => void;
  onBatchEdit: (taskIds: string[]) => void;
  processing: boolean;
}

/**
 * Import/Add Mod Window with task queue
 * Displays all pending mod imports in a table format
 * Supports batch operations and task management
 */
export const AddModWindow: React.FC<AddModWindowProps> = ({
  visible,
  tasks,
  onConfirm,
  onCancel,
  onEditTask,
  onRemoveTask,
  onBatchEdit,
  processing,
}) => {
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  // Task status icon renderer
  const renderStatusIcon = (status: TaskStatus): React.ReactNode => {
    switch (status) {
      case 'pending':
        return <Tag color="default">Pending</Tag>;
      case 'processing':
        return <Tag icon={<LoadingOutlined />} color="processing">Processing</Tag>;
      case 'success':
        return <Tag icon={<CheckOutlined />} color="success">Success</Tag>;
      case 'error':
        return <Tag icon={<CloseOutlined />} color="error">Error</Tag>;
      case 'skipped':
        return <Tag color="warning">Skipped</Tag>;
      default:
        return <Tag>Unknown</Tag>;
    }
  };

  // Table columns configuration
  const columns: ColumnsType<ImportTask> = [
    {
      title: 'Task ID',
      dataIndex: 'id',
      key: 'id',
      width: 100,
      fixed: 'left',
    },
    {
      title: 'File Name',
      dataIndex: 'fileName',
      key: 'fileName',
      width: 200,
      ellipsis: true,
    },
    {
      title: 'Type',
      dataIndex: 'fileType',
      key: 'fileType',
      width: 100,
      render: (fileType: string) => (
        <Tag color={fileType === 'archive' ? 'blue' : 'green'}>
          {fileType === 'archive' ? 'Archive' : 'Folder'}
        </Tag>
      ),
    },
    {
      title: 'Mod Name',
      key: 'modName',
      width: 180,
      ellipsis: true,
      render: (_, record) => record.modData.name || <span style={{ color: '#8c8c8c' }}>Not set</span>,
    },
    {
      title: 'Category',
      key: 'category',
      width: 150,
      ellipsis: true,
      render: (_, record) => record.modData.category || <span style={{ color: '#8c8c8c' }}>Not set</span>,
    },
    {
      title: 'Author',
      key: 'author',
      width: 120,
      ellipsis: true,
      render: (_, record) => record.modData.author || <span style={{ color: '#8c8c8c' }}>Not set</span>,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 120,
      render: renderStatusIcon,
    },
    {
      title: 'Progress',
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
      title: 'Actions',
      key: 'actions',
      width: 120,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small">
          <Button
            type="text"
            icon={<EditOutlined />}
            size="small"
            onClick={() => onEditTask(record)}
            disabled={record.status === 'processing' || record.status === 'success'}
            title="Edit Task"
          />
          <Button
            type="text"
            danger
            icon={<DeleteOutlined />}
            size="small"
            onClick={() => handleRemoveTask(record.id)}
            disabled={record.status === 'processing'}
            title="Remove Task"
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
    onRemoveTask(taskId);
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
      notification.warning('Please select at least one task to edit');
      return;
    }
    onBatchEdit(selectedRowKeys as string[]);
  };

  // Handle confirm
  const handleConfirm = async () => {
    // Validate all tasks have required fields
    const invalidTasks = tasks.filter(task =>
      task.status === 'pending' && (!task.modData.name || !task.modData.category)
    );

    if (invalidTasks.length > 0) {
      notification.error(`${invalidTasks.length} task(s) missing required fields (Name, Category)`);
      return;
    }

    await onConfirm(tasks);
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
      title="Import Mods - Task Queue"
      open={visible}
      onCancel={onCancel}
      width={1200}
      style={{ top: 20 }}
      footer={[
        <Space key="stats" style={{ float: 'left' }}>
          <span>Total: {stats.total}</span>
          <Divider type="vertical" />
          <span>Pending: <Tag color="default">{stats.pending}</Tag></span>
          <span>Success: <Tag color="success">{stats.success}</Tag></span>
          {stats.error > 0 && <span>Error: <Tag color="error">{stats.error}</Tag></span>}
        </Space>,
        <Button key="cancel" onClick={onCancel} disabled={processing}>
          Cancel
        </Button>,
        <Button
          key="confirm"
          type="primary"
          onClick={handleConfirm}
          loading={processing}
          disabled={tasks.length === 0 || stats.pending === 0}
        >
          {processing ? `Processing... (${stats.success}/${stats.total})` : `Import ${stats.pending} Mod(s)`}
        </Button>,
      ]}
    >
      <Space orientation="vertical" style={{ width: '100%' }} size="middle">
        {/* Toolbar */}
        <Space wrap>
          <Button onClick={handleSelectAll} disabled={processing}>
            Select All Pending
          </Button>
          <Button onClick={handleClearSelection} disabled={processing}>
            Clear Selection
          </Button>
          <Button
            icon={<EditOutlined />}
            onClick={handleBatchEdit}
            disabled={processing || selectedRowKeys.length === 0}
          >
            Batch Edit ({selectedRowKeys.length})
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
          <div style={{ textAlign: 'center', padding: '8px', background: '#f0f0f0', borderRadius: '4px' }}>
            <LoadingOutlined style={{ marginRight: '8px' }} />
            Processing tasks... Please wait.
          </div>
        )}
      </Space>
    </Modal>
  );
};
