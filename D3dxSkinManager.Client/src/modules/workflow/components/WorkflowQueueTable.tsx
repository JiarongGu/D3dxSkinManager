/**
 * Workflow Queue Table
 * Download manager style table showing all active workflows
 * Now uses the shared DataTable component with expandable detail rows
 */

import React, { useState } from 'react';
import { Progress, Tag, Button, Space, Tooltip, Modal, Form, Input, Select, Descriptions } from 'antd';
import { DataTable, ColumnsType } from '../../../shared/components/common/DataTable';
import {
  FolderOutlined,
  ClockCircleOutlined,
  LoadingOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  DeleteOutlined,
  EditOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../shared/context/ProfileContext';
import {
  WorkflowInfo,
  WorkflowStatus,
  ModImportWorkflowContext,
  ModImportWorkflowSteps,
} from '../types/workflow.types';
import { workflowService } from '../services/workflowService';
import { handleError } from '../../../shared/utils/errorHandler';
import './WorkflowQueueTable.css';

const { TextArea } = Input;

interface WorkflowQueueTableProps {
  workflows: WorkflowInfo[];
  onRefresh?: () => void;
}

/**
 * Enhanced workflow info with parsed context for display
 */
interface WorkflowTableRow {
  workflow: WorkflowInfo;
  context: ModImportWorkflowContext | null;
  name: string;
  progress: number;
  statusText: string;
}

export const WorkflowQueueTable: React.FC<WorkflowQueueTableProps> = ({
  workflows,
  onRefresh,
}) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [form] = Form.useForm();
  const [metadataModalVisible, setMetadataModalVisible] = useState(false);
  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowInfo | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [selectedRowKeys, setSelectedRowKeys] = useState<string[]>([]);

  /**
   * Parse workflow context and prepare table data
   */
  const parseWorkflow = (workflow: WorkflowInfo): WorkflowTableRow => {
    let context: ModImportWorkflowContext | null = null;
    try {
      context = JSON.parse(workflow.context) as ModImportWorkflowContext;
      console.log('[WorkflowQueueTable] Workflow parsed:', {
        id: workflow.id,
        status: workflow.status,
        step: context?.step,
        progress: context?.progress,
      });
    } catch (error) {
      console.error('[WorkflowQueueTable] Failed to parse context:', error);
    }

    // Use progress from context (driven by backend)
    let progress = 0;
    if (workflow.status === WorkflowStatus.Completed) {
      progress = 100;
    } else if (workflow.status === WorkflowStatus.Failed || workflow.status === WorkflowStatus.Cancelled) {
      progress = 0;
    } else if (context && context.progress !== undefined) {
      progress = context.progress;
    }

    // Get display name
    const name = context?.name || context?.folderName || context?.folderPath?.split('\\').pop() || 'Unknown';

    // Get status text
    let statusText = '';
    switch (workflow.status) {
      case WorkflowStatus.Pending:
        statusText = t('workflow.status.pending');
        break;
      case WorkflowStatus.Processing:
        if (context?.step === ModImportWorkflowSteps.CompressFolder) {
          statusText = t('workflow.modImport.compressing');
        } else if (context?.step === ModImportWorkflowSteps.ImportMod) {
          statusText = t('workflow.modImport.importing');
        } else if (context?.step === ModImportWorkflowSteps.ExtractMetadata) {
          statusText = t('workflow.modImport.extracting');
        } else {
          statusText = t('workflow.status.processing');
        }
        break;
      case WorkflowStatus.WaitingForInput:
        statusText = t('workflow.status.waitingForInput');
        break;
      case WorkflowStatus.Completed:
        statusText = t('workflow.status.completed');
        break;
      case WorkflowStatus.Failed:
        statusText = t('workflow.status.failed');
        break;
      case WorkflowStatus.Cancelled:
        statusText = t('workflow.status.cancelled');
        break;
      default:
        statusText = `Unknown (${workflow.status})`; // Debug fallback
        break;
    }

    return { workflow, context, name, progress, statusText };
  };

  const tableData = workflows.map(parseWorkflow);

  /**
   * Handle metadata edit button click (works for any status, not just WaitingForInput)
   */
  const handleEditMetadata = (row: WorkflowTableRow) => {
    setSelectedWorkflow(row.workflow);
    // Pre-fill with existing context values
    form.setFieldsValue({
      name: row.context?.name || row.context?.folderName || '',
      author: row.context?.author || '',
      description: row.context?.description || '',
      category: row.context?.category || '',
      tags: row.context?.tags || [],
      grading: row.context?.grading || 'G',
    });
    setMetadataModalVisible(true);
  };

  /**
   * Handle confirm button click - directly continue workflow without opening modal
   */
  const handleConfirm = async (workflowId: string) => {
    if (!selectedProfileId) return;

    try {
      await workflowService.continueWorkflow(selectedProfileId, workflowId);
      onRefresh?.();
    } catch (error) {
      handleError(error);
    }
  };

  /**
   * Handle batch confirm for all selected rows waiting for input
   */
  const handleBatchConfirm = async () => {
    if (!selectedProfileId || selectedRowKeys.length === 0) return;

    try {
      const waitingWorkflows = tableData.filter(
        (row) =>
          selectedRowKeys.includes(row.workflow.id) &&
          row.workflow.status === WorkflowStatus.WaitingForInput
      );

      // Continue all selected workflows that are waiting for input
      await Promise.all(
        waitingWorkflows.map((row) =>
          workflowService.continueWorkflow(selectedProfileId, row.workflow.id)
        )
      );

      setSelectedRowKeys([]);
      onRefresh?.();
    } catch (error) {
      handleError(error);
    }
  };

  /**
   * Handle select all / deselect all
   */
  const handleSelectAll = () => {
    if (selectedRowKeys.length === tableData.length) {
      setSelectedRowKeys([]);
    } else {
      setSelectedRowKeys(tableData.map((row) => row.workflow.id));
    }
  };

  /**
   * Handle metadata update
   * If workflow is waiting for input, also continue to next step
   * If workflow is processing, just update the context (edit during compression)
   */
  const handleMetadataSubmit = async () => {
    if (!selectedWorkflow || !selectedProfileId) return;

    try {
      setSubmitting(true);
      const values = await form.validateFields();

      // Update context with metadata
      // JsonHelper on backend will handle camelCase (JS) to PascalCase (C#) conversion
      await workflowService.updateWorkflowContext(selectedProfileId, selectedWorkflow.id, {
        name: values.name,
        author: values.author || null,
        description: values.description || null,
        category: values.category || null,
        tags: values.tags || [],
        grading: values.grading || 'G',
      });

      // Only continue workflow if it's waiting for input (confirmation step)
      // If it's still processing (compression), just update context
      if (selectedWorkflow.status === WorkflowStatus.WaitingForInput) {
        await workflowService.continueWorkflow(selectedProfileId, selectedWorkflow.id);
      }

      setMetadataModalVisible(false);
      setSelectedWorkflow(null);
      form.resetFields();
      onRefresh?.();
    } catch (error) {
      console.error('[WorkflowQueueTable] Failed to submit metadata:', error);
      handleError(error);
    } finally {
      setSubmitting(false);
    }
  };

  /**
   * Handle pause workflow
   */
  const handlePauseWorkflow = async (workflowId: string) => {
    if (!selectedProfileId) return;

    try {
      await workflowService.pauseWorkflow(selectedProfileId, workflowId);
      onRefresh?.();
    } catch (error) {
      handleError(error);
    }
  };

  /**
   * Handle delete workflow
   */
  const handleDeleteWorkflow = async (workflowId: string) => {
    if (!selectedProfileId) return;

    try {
      await workflowService.deleteWorkflow(selectedProfileId, workflowId);
      onRefresh?.();
    } catch (error) {
      handleError(error);
    }
  };

  /**
   * Get status icon
   */
  const getStatusIcon = (status: WorkflowStatus) => {
    switch (status) {
      case WorkflowStatus.Pending:
        return <ClockCircleOutlined style={{ color: '#1890ff' }} />;
      case WorkflowStatus.Processing:
        return <LoadingOutlined style={{ color: '#1890ff' }} />;
      case WorkflowStatus.WaitingForInput:
        return <ExclamationCircleOutlined style={{ color: '#faad14' }} />;
      case WorkflowStatus.Completed:
        return <CheckCircleOutlined style={{ color: '#52c41a' }} />;
      case WorkflowStatus.Failed:
        return <CloseCircleOutlined style={{ color: '#ff4d4f' }} />;
      case WorkflowStatus.Cancelled:
        return <CloseCircleOutlined style={{ color: '#8c8c8c' }} />;
    }
  };

  /**
   * Get status tag color
   */
  const getStatusColor = (status: WorkflowStatus): string => {
    switch (status) {
      case WorkflowStatus.Pending:
        return 'blue';
      case WorkflowStatus.Processing:
        return 'processing';
      case WorkflowStatus.WaitingForInput:
        return 'warning';
      case WorkflowStatus.Completed:
        return 'success';
      case WorkflowStatus.Failed:
        return 'error';
      case WorkflowStatus.Cancelled:
        return 'default';
      default:
        return 'default';
    }
  };

  const ageRatingOptions = [
    { value: 'G', label: t('mods.edit.ageRating.general') },
    { value: 'P', label: t('mods.edit.ageRating.parentalGuidance') },
    { value: 'R', label: t('mods.edit.ageRating.restricted') },
    { value: 'X', label: t('mods.edit.ageRating.adultsOnly') },
  ];

  /**
   * Render expandable row content showing detailed workflow context
   */
  const renderExpandedRow = (row: WorkflowTableRow) => {
    const { workflow, context } = row;

    return (
      <Descriptions bordered size="small" column={2} style={{ marginLeft: 48 }}>
        <Descriptions.Item label={t('workflow.queue.workflowId')} span={2}>
          {workflow.id}
        </Descriptions.Item>
        <Descriptions.Item label={t('workflow.queue.step')}>
          {context?.step || 'N/A'}
        </Descriptions.Item>
        <Descriptions.Item label={t('workflow.queue.progress')}>
          {context?.progress || 0}%
        </Descriptions.Item>
        <Descriptions.Item label={t('workflow.queue.folderPath')} span={2}>
          {context?.folderPath || 'N/A'}
        </Descriptions.Item>
        {context?.tempArchivePath && (
          <Descriptions.Item label={t('workflow.queue.tempArchivePath')} span={2}>
            {context.tempArchivePath}
          </Descriptions.Item>
        )}
        <Descriptions.Item label={t('workflow.queue.fileCount')}>
          {context?.fileCount || 0}
        </Descriptions.Item>
        <Descriptions.Item label={t('workflow.queue.author')}>
          {context?.author || t('common.notSet')}
        </Descriptions.Item>
        <Descriptions.Item label={t('workflow.queue.category')}>
          {context?.category || t('common.notSet')}
        </Descriptions.Item>
        <Descriptions.Item label={t('workflow.queue.grading')}>
          {context?.grading || 'G'}
        </Descriptions.Item>
        <Descriptions.Item label={t('workflow.queue.tags')} span={2}>
          {context?.tags && context.tags.length > 0 ? context.tags.join(', ') : t('common.none')}
        </Descriptions.Item>
        {context?.description && (
          <Descriptions.Item label={t('workflow.queue.description')} span={2}>
            {context.description}
          </Descriptions.Item>
        )}
        <Descriptions.Item label={t('workflow.queue.createdAt')}>
          {new Date(workflow.createdAt).toLocaleString()}
        </Descriptions.Item>
        {workflow.completedAt && (
          <Descriptions.Item label={t('workflow.queue.completedAt')}>
            {new Date(workflow.completedAt).toLocaleString()}
          </Descriptions.Item>
        )}
        {workflow.errorMessage && (
          <Descriptions.Item label={t('workflow.queue.error')} span={2}>
            <span style={{ color: '#ff4d4f' }}>{workflow.errorMessage}</span>
          </Descriptions.Item>
        )}
        {context?.importedModSha && (
          <Descriptions.Item label={t('workflow.queue.importedModSha')} span={2}>
            {context.importedModSha}
          </Descriptions.Item>
        )}
      </Descriptions>
    );
  };

  const columns: ColumnsType<WorkflowTableRow> = [
    {
      title: t('workflow.queue.name'),
      dataIndex: 'name',
      key: 'name',
      width: '30%',
      ellipsis: true,
      render: (name: string, row: WorkflowTableRow) => (
        <div className="workflow-name-cell">
          <FolderOutlined style={{ marginRight: 8, color: '#1890ff' }} />
          <Tooltip title={row.context?.folderPath}>
            <span className="workflow-name">{name}</span>
          </Tooltip>
          {row.context?.fileCount && (
            <span className="workflow-file-count">
              ({row.context.fileCount} {t('common.files')})
            </span>
          )}
        </div>
      ),
    },
    {
      title: t('workflow.queue.status'),
      dataIndex: 'statusText',
      key: 'status',
      width: '20%',
      render: (statusText: string, row: WorkflowTableRow) => (
        <Space>
          {getStatusIcon(row.workflow.status)}
          <Tag color={getStatusColor(row.workflow.status)}>{statusText}</Tag>
        </Space>
      ),
    },
    {
      title: t('workflow.queue.progress'),
      dataIndex: 'progress',
      key: 'progress',
      width: '25%',
      render: (progress: number, row: WorkflowTableRow) => {
        const status =
          row.workflow.status === WorkflowStatus.Failed
            ? 'exception'
            : row.workflow.status === WorkflowStatus.Completed
            ? 'success'
            : 'active';

        return (
          <Progress
            percent={progress}
            status={status}
            size="small"
            strokeColor={
              row.workflow.status === WorkflowStatus.WaitingForInput ? '#faad14' : undefined
            }
          />
        );
      },
    },
    {
      title: t('workflow.queue.actions'),
      key: 'actions',
      width: '25%',
      render: (_: unknown, row: WorkflowTableRow) => {
        const { workflow } = row;
        const isActive = workflow.status !== WorkflowStatus.Completed &&
                         workflow.status !== WorkflowStatus.Failed &&
                         workflow.status !== WorkflowStatus.Cancelled;
        const isFinished = !isActive;

        return (
          <Space size="small">
            {/* Confirm/Edit button - always show for active workflows */}
            {isActive && (
              <Tooltip
                title={
                  workflow.status === WorkflowStatus.WaitingForInput
                    ? t('workflow.queue.confirm')
                    : t('workflow.queue.edit') || 'Edit Metadata'
                }
              >
                <Button
                  type={workflow.status === WorkflowStatus.WaitingForInput ? 'primary' : 'default'}
                  size="small"
                  icon={<EditOutlined />}
                  onClick={() =>
                    workflow.status === WorkflowStatus.WaitingForInput
                      ? handleConfirm(workflow.id)
                      : handleEditMetadata(row)
                  }
                >
                  {workflow.status === WorkflowStatus.WaitingForInput
                    ? t('workflow.queue.confirm')
                    : t('workflow.queue.edit') || 'Edit'}
                </Button>
              </Tooltip>
            )}

            {/* Pause button - show when processing (not waiting) */}
            {isActive && workflow.status !== WorkflowStatus.WaitingForInput && (
              <Tooltip title={t('workflow.queue.pause')}>
                <Button
                  size="small"
                  onClick={() => handlePauseWorkflow(workflow.id)}
                >
                  {t('workflow.queue.pause')}
                </Button>
              </Tooltip>
            )}

            {/* Delete button - show when completed/failed/cancelled */}
            {isFinished && (
              <Tooltip title={t('workflow.queue.delete')}>
                <Button
                  size="small"
                  icon={<DeleteOutlined />}
                  onClick={() => handleDeleteWorkflow(workflow.id)}
                  danger
                >
                  {t('workflow.queue.delete')}
                </Button>
              </Tooltip>
            )}

            {/* Error message - show when failed */}
            {workflow.status === WorkflowStatus.Failed && workflow.errorMessage && (
              <Tooltip title={workflow.errorMessage}>
                <Button size="small" icon={<ExclamationCircleOutlined />} danger>
                  {t('workflow.queue.viewError')}
                </Button>
              </Tooltip>
            )}
          </Space>
        );
      },
    },
  ];

  const selectedWaitingCount = tableData.filter(
    (row) =>
      selectedRowKeys.includes(row.workflow.id) &&
      row.workflow.status === WorkflowStatus.WaitingForInput
  ).length;

  const rowSelection = {
    selectedRowKeys,
    onChange: (keys: React.Key[]) => setSelectedRowKeys(keys as string[]),
    getCheckboxProps: (record: WorkflowTableRow) => ({
      disabled: record.workflow.status === WorkflowStatus.Completed ||
                record.workflow.status === WorkflowStatus.Failed ||
                record.workflow.status === WorkflowStatus.Cancelled,
    }),
  };

  return (
    <>
      {/* Batch Action Toolbar */}
      {selectedRowKeys.length > 0 && (
        <div style={{ marginBottom: 16, padding: '8px 12px', background: '#f0f2f5', borderRadius: 4 }}>
          <Space>
            <span>
              {t('common.selected')}: <strong>{selectedRowKeys.length}</strong>
            </span>
            {selectedWaitingCount > 0 && (
              <Button
                type="primary"
                size="small"
                onClick={handleBatchConfirm}
              >
                {t('workflow.queue.batchConfirm') || `Confirm ${selectedWaitingCount} Waiting`}
              </Button>
            )}
            <Button size="small" onClick={handleSelectAll}>
              {selectedRowKeys.length === tableData.length
                ? t('common.deselectAll')
                : t('common.selectAll')}
            </Button>
            <Button size="small" onClick={() => setSelectedRowKeys([])}>
              {t('common.clear') || 'Clear'}
            </Button>
          </Space>
        </div>
      )}

      <DataTable<WorkflowTableRow>
        className="workflow-queue-table"
        columns={columns}
        dataSource={tableData}
        rowKey={(row) => row.workflow.id}
        pagination={false}
        size="middle"
        rowSelection={rowSelection}
        expandable={{
          expandedRowRender: renderExpandedRow,
          rowExpandable: () => true,
        }}
        emptyText={t('workflow.queue.empty')}
      />

      {/* Metadata Input Modal */}
      <Modal
        title={t('workflow.modImport.provideMetadata')}
        open={metadataModalVisible}
        onCancel={() => {
          setMetadataModalVisible(false);
          setSelectedWorkflow(null);
          form.resetFields();
        }}
        onOk={handleMetadataSubmit}
        confirmLoading={submitting}
        width={600}
        transitionName=""
        maskTransitionName=""
      >
        <p style={{ marginBottom: 16, color: '#888' }}>
          {t('workflow.modImport.metadataDescription')}
        </p>
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label={t('mods.edit.name')}
            rules={[{ required: true, message: t('mods.edit.nameRequired') }]}
          >
            <Input placeholder={t('mods.edit.namePlaceholder')} />
          </Form.Item>

          <Form.Item name="author" label={t('mods.edit.author')}>
            <Input placeholder={t('mods.edit.authorPlaceholder')} />
          </Form.Item>

          <Form.Item name="description" label={t('mods.edit.description')}>
            <TextArea rows={3} placeholder={t('mods.edit.descriptionPlaceholder')} />
          </Form.Item>

          <Form.Item
            name="category"
            label={t('mods.edit.category')}
            tooltip={t('mods.edit.categoryTooltip') || 'Leave empty for Unclassified'}
          >
            <Input placeholder={t('mods.edit.categoryPlaceholder') || 'Leave empty for Unclassified'} />
          </Form.Item>

          <Form.Item name="grading" label={t('mods.edit.ageRating.label')}>
            <Select options={ageRatingOptions} />
          </Form.Item>

          <Form.Item name="tags" label={t('mods.edit.tags')}>
            <Select mode="tags" placeholder={t('mods.edit.tagsPlaceholder')} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
};
