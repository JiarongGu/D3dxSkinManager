/**
 * Workflow Queue Table
 * Download manager style table showing all active workflows
 */

import React, { useState } from 'react';
import { Table, Progress, Tag, Button, Space, Tooltip, Modal, Form, Input, Select } from 'antd';
import type { ColumnsType } from 'antd/es/table';
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

  /**
   * Parse workflow context and prepare table data
   */
  const parseWorkflow = (workflow: WorkflowInfo): WorkflowTableRow => {
    let context: ModImportWorkflowContext | null = null;
    try {
      context = JSON.parse(workflow.context) as ModImportWorkflowContext;
    } catch (error) {
      console.error('[WorkflowQueueTable] Failed to parse context:', error);
    }

    // Calculate progress based on step
    let progress = 0;
    if (workflow.status === WorkflowStatus.Completed) {
      progress = 100;
    } else if (workflow.status === WorkflowStatus.Failed || workflow.status === WorkflowStatus.Cancelled) {
      progress = 0;
    } else if (context) {
      switch (context.step) {
        case ModImportWorkflowSteps.ExtractMetadata:
          progress = 25;
          break;
        case ModImportWorkflowSteps.WaitingForUserConfirmation:
          progress = 40;
          break;
        case ModImportWorkflowSteps.CompressFolder:
          progress = 70;
          break;
        case ModImportWorkflowSteps.ImportMod:
          progress = 90;
          break;
        case ModImportWorkflowSteps.Completed:
          progress = 100;
          break;
      }
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
    }

    return { workflow, context, name, progress, statusText };
  };

  const tableData = workflows.map(parseWorkflow);

  /**
   * Handle metadata edit button click
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
   * Handle metadata update and continue workflow
   */
  const handleMetadataSubmit = async () => {
    if (!selectedWorkflow || !selectedProfileId) return;

    try {
      setSubmitting(true);
      const values = await form.validateFields();

      // Update context with metadata
      await workflowService.updateWorkflowContext(selectedProfileId, selectedWorkflow.id, {
        name: values.name,
        author: values.author || null,
        description: values.description || null,
        category: values.category,
        tags: values.tags || [],
        grading: values.grading || 'G',
      });

      // Continue workflow to next step
      await workflowService.continueWorkflow(selectedProfileId, selectedWorkflow.id);

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
   * Handle cancel workflow
   */
  const handleCancelWorkflow = async (workflowId: string) => {
    if (!selectedProfileId) return;

    try {
      await workflowService.cancelModImport(selectedProfileId, workflowId);
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
        const { workflow, context } = row;

        return (
          <Space size="small">
            {/* Provide Metadata button - only show when waiting for input */}
            {workflow.status === WorkflowStatus.WaitingForInput && (
              <Button
                type="primary"
                size="small"
                icon={<EditOutlined />}
                onClick={() => handleEditMetadata(row)}
              >
                {t('workflow.queue.provideMetadata')}
              </Button>
            )}

            {/* Cancel button - show when not completed/failed/cancelled */}
            {workflow.status !== WorkflowStatus.Completed &&
              workflow.status !== WorkflowStatus.Failed &&
              workflow.status !== WorkflowStatus.Cancelled && (
                <Tooltip title={t('workflow.queue.cancel')}>
                  <Button
                    size="small"
                    icon={<DeleteOutlined />}
                    onClick={() => handleCancelWorkflow(workflow.id)}
                    danger
                  />
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

  return (
    <>
      <Table<WorkflowTableRow>
        className="workflow-queue-table"
        columns={columns}
        dataSource={tableData}
        rowKey={(row) => row.workflow.id}
        pagination={false}
        size="middle"
        locale={{
          emptyText: t('workflow.queue.empty'),
        }}
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
            rules={[{ required: true, message: 'Please select a category' }]}
          >
            <Input placeholder={t('mods.edit.categoryPlaceholder')} />
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
