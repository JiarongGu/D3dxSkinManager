/**
 * ModImport Workflow Table
 * Download manager style table showing all active mod import workflows
 * Now uses the shared DataTable component with expandable detail rows
 */

import React, { useState, useCallback, useMemo } from "react";
import { Progress, Space, Tooltip, Descriptions, Empty } from "antd";
import {
  DataTable,
  ColumnsType,
} from "../../../../shared/components/common/DataTable";
import { StatusTag, StatusTone } from "../../../../shared/components/common/StatusTag";
import {
  FolderOutlined,
  CheckCircleOutlined,
  DeleteOutlined,
  EditOutlined,
  PauseCircleOutlined,
  PlayCircleOutlined,
} from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { useProfile } from "../../../../shared/context/ProfileContext";
import {
  WorkflowInfo,
  WorkflowStatus,
  ModImportWorkflowContext,
  ModImportWorkflowSteps,
} from "../../types/workflow.types";
import { handleError, translateErrorMessage } from "../../../../shared/utils/errorHandler";
import {
  ModImportMetadataDialog,
  ModImportMetadataFormValues,
} from "./ModImportMetadataDialog";
import "./ModImportWorkflowTable.css";
import { CompactButton } from "../../../../shared/components/compact";
import { workflowService } from "../../../../shared/services/ipc";

interface ModImportWorkflowTableProps {
  workflows: WorkflowInfo[];
  onRefresh?: () => void;
  selectedRowKeys?: string[];
  onSelectionChange?: (keys: string[]) => void;
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

export const ModImportWorkflowTable: React.FC<ModImportWorkflowTableProps> = ({
  workflows,
  onRefresh,
  selectedRowKeys: externalSelectedRowKeys,
  onSelectionChange,
}) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [metadataModalVisible, setMetadataModalVisible] = useState(false);
  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowInfo | null>(
    null,
  );
  const [selectedContext, setSelectedContext] =
    useState<ModImportWorkflowContext | null>(null);

  // Track expanded rows
  const [expandedRowKeys, setExpandedRowKeys] = useState<string[]>([]);

  // Use external selection state if provided, otherwise use internal state
  const [internalSelectedRowKeys, setInternalSelectedRowKeys] = useState<
    string[]
  >([]);
  const selectedRowKeys = externalSelectedRowKeys ?? internalSelectedRowKeys;
  const setSelectedRowKeys = onSelectionChange ?? setInternalSelectedRowKeys;

  /**
   * Parse workflows and cache results - only re-parse when workflow.context changes
   * This is the performance bottleneck - JSON.parse is expensive
   */
  const tableData = useMemo(() => {
    return workflows.map((workflow): WorkflowTableRow => {
      let context: ModImportWorkflowContext | null = null;
      try {
        context = JSON.parse(workflow.context) as ModImportWorkflowContext;
      } catch (error: unknown) {
        // Invalid JSON, context stays null
      }

      // Calculate progress from context
      let progress = 0;
      if (workflow.status === WorkflowStatus.Completed) {
        progress = 100;
      } else if (
        workflow.status === WorkflowStatus.Failed ||
        workflow.status === WorkflowStatus.Cancelled ||
        workflow.status === WorkflowStatus.Deleting
      ) {
        progress = 0;
      } else if (context && context.progress !== undefined) {
        progress = context.progress;
      }

      // Get display name
      const name =
        context?.name ||
        context?.folderName ||
        context?.folderPath?.split("\\").pop() ||
        t("workflow.modImport.unknownName");

      // Get status text based on current status and step
      let statusText = "";
      switch (workflow.status) {
        case WorkflowStatus.Pending:
          statusText = t("workflow.status.pending");
          break;
        case WorkflowStatus.Processing:
          if (context?.step === ModImportWorkflowSteps.CompressFolder) {
            statusText = t("workflow.modImport.compressing");
          } else if (context?.step === ModImportWorkflowSteps.ImportMod) {
            statusText = t("workflow.modImport.importing");
          } else if (context?.step === ModImportWorkflowSteps.ExtractMetadata) {
            statusText = t("workflow.modImport.extracting");
          } else {
            statusText = t("workflow.status.processing");
          }
          break;
        case WorkflowStatus.WaitingForInput:
          statusText = t("workflow.status.awaitingConfirmation");
          break;
        case WorkflowStatus.Paused:
          statusText = t("workflow.status.paused");
          break;
        case WorkflowStatus.Completed:
          statusText = t("workflow.status.completed");
          break;
        case WorkflowStatus.Failed:
          statusText = t("workflow.status.failed");
          break;
        case WorkflowStatus.Cancelled:
          statusText = t("workflow.status.cancelled");
          break;
        case WorkflowStatus.Deleting:
          statusText = t("workflow.status.deleting");
          break;
        default:
          statusText = t("workflow.status.unknown", { status: workflow.status });
          break;
      }

      return { workflow, context, name, progress, statusText };
    });
  }, [workflows, t]);

  /**
   * Handle metadata edit button click (works for any status, not just WaitingForInput)
   */
  const handleEditMetadata = useCallback((row: WorkflowTableRow) => {
    setSelectedWorkflow(row.workflow);
    setSelectedContext(row.context);
    setMetadataModalVisible(true);
  }, []);

  /**
   * Handle confirm button click - directly continue workflow without opening modal
   */
  const handleConfirm = useCallback(async (workflowId: string) => {
    if (!selectedProfileId) return;

    try {
      await workflowService.continueWorkflow(selectedProfileId, workflowId);
      onRefresh?.();
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, onRefresh]);

  /**
   * Handle metadata update
   * Only updates the context, does NOT automatically continue the workflow
   * User must click the separate "Confirm" button to actually import the mod
   */
  const handleMetadataSubmit = useCallback(async (values: ModImportMetadataFormValues) => {
    if (!selectedWorkflow || !selectedProfileId) return;

    try {
      // Update context with metadata
      // JsonHelper on backend will handle camelCase (JS) to PascalCase (C#) conversion
      await workflowService.updateWorkflowContext(
        selectedProfileId,
        selectedWorkflow.id,
        {
          name: values.name,
          author: values.author || null,
          description: values.description || null,
          category: values.category || null,
          grading: values.grading || "G",
        },
      );

      // Just update metadata - don't continue workflow
      // User must click "Confirm" button separately to import

      setMetadataModalVisible(false);
      setSelectedWorkflow(null);
      setSelectedContext(null);
      onRefresh?.();
    } catch (error: unknown) {
            handleError(error);
      throw error; // Re-throw to prevent modal from closing
    }
  }, [selectedWorkflow, selectedProfileId, onRefresh]);

  const handleMetadataCancel = useCallback(() => {
    setMetadataModalVisible(false);
    setSelectedWorkflow(null);
    setSelectedContext(null);
  }, []);

  /**
   * Handle pause workflow
   */
  const handlePauseWorkflow = useCallback(async (workflowId: string) => {
    if (!selectedProfileId) return;

    try {
      await workflowService.pauseWorkflow(selectedProfileId, workflowId);
      onRefresh?.();
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, onRefresh]);

  /**
   * Handle resume workflow (for workflows that stopped after app reboot)
   */
  const handleResumeWorkflow = useCallback(async (workflowId: string) => {
    if (!selectedProfileId) return;

    try {
      await workflowService.resumeWorkflow(selectedProfileId, workflowId);
      onRefresh?.();
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, onRefresh]);

  /**
   * Handle delete workflow
   */
  const handleDeleteWorkflow = useCallback(async (workflowId: string) => {
    if (!selectedProfileId) return;

    try {
      await workflowService.deleteWorkflow(selectedProfileId, workflowId);
      onRefresh?.();
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, onRefresh]);

  /**
   * Get status tag color
   */
  const getStatusTone = (status: WorkflowStatus): StatusTone => {
    switch (status) {
      case WorkflowStatus.Pending:
        return "info";
      case WorkflowStatus.Processing:
        return "processing";
      case WorkflowStatus.WaitingForInput:
        return "warning";
      case WorkflowStatus.Completed:
        return "success";
      case WorkflowStatus.Failed:
        return "error";
      case WorkflowStatus.Deleting:
        return "warning";
      case WorkflowStatus.Paused:
      case WorkflowStatus.Cancelled:
      default:
        return "neutral";
    }
  };

  /**
   * Render expandable row content showing detailed workflow context
   */
  const renderExpandedRow = useCallback((row: WorkflowTableRow) => {
    const { workflow, context } = row;

    return (
      <Descriptions bordered size="small" column={2} style={{ marginLeft: 48 }}>
        <Descriptions.Item label={t("workflow.queue.workflowId")} span={2}>
          {workflow.id}
        </Descriptions.Item>
        <Descriptions.Item label={t("workflow.queue.step")}>
          {context?.step || "N/A"}
        </Descriptions.Item>
        <Descriptions.Item label={t("workflow.queue.progress")}>
          {context?.progress || 0}%
        </Descriptions.Item>
        <Descriptions.Item label={t("workflow.queue.folderPath")} span={2}>
          {context?.folderPath || "N/A"}
        </Descriptions.Item>
        {context?.tempArchivePath && (
          <Descriptions.Item
            label={t("workflow.queue.tempArchivePath")}
            span={2}
          >
            {context.tempArchivePath}
          </Descriptions.Item>
        )}
        <Descriptions.Item label={t("workflow.queue.fileCount")}>
          {context?.fileCount || 0}
        </Descriptions.Item>
        <Descriptions.Item label={t("common.author")}>
          {context?.author || t("common.notSet")}
        </Descriptions.Item>
        <Descriptions.Item label={t("common.category")}>
          {context?.categoryName || t("common.notSet")}
        </Descriptions.Item>
        <Descriptions.Item label={t("workflow.queue.grading")}>
          {context?.grading || "G"}
        </Descriptions.Item>
        <Descriptions.Item label={t("common.tags")} span={2}>
          {context?.tags && context.tags.length > 0
            ? context.tags.join(", ")
            : t("common.none")}
        </Descriptions.Item>
        {context?.description && (
          <Descriptions.Item label={t("common.description")} span={2}>
            {context.description}
          </Descriptions.Item>
        )}
        <Descriptions.Item label={t("workflow.queue.createdAt")}>
          {new Date(workflow.createdAt).toLocaleString()}
        </Descriptions.Item>
        {workflow.completedAt && (
          <Descriptions.Item label={t("workflow.queue.completedAt")}>
            {new Date(workflow.completedAt).toLocaleString()}
          </Descriptions.Item>
        )}
        {workflow.errorMessage && (
          <Descriptions.Item label={t("workflow.queue.error")} span={2}>
            <span style={{ color: "#ff4d4f" }}>{workflow.errorMessage}</span>
          </Descriptions.Item>
        )}
        {context?.importedModId && (
          <Descriptions.Item
            label={t("workflow.queue.importedModId")}
            span={2}
          >
            {context.importedModId}
          </Descriptions.Item>
        )}
      </Descriptions>
    );
  }, [t]);

  const columns: ColumnsType<WorkflowTableRow> = useMemo(() => [
    {
      title: t("common.name"),
      dataIndex: "name",
      key: "name",
      width: "25%",
      ellipsis: true,
      sorter: (a, b) => a.name.localeCompare(b.name),
      showSorterTooltip: false,
      render: (name: string, row: WorkflowTableRow) => (
        <div className="mod-import-workflow-table-name-cell">
          <FolderOutlined style={{ marginRight: 8, color: "#1890ff" }} />
          <Tooltip title={row.context?.folderPath}>
            <span className="mod-import-workflow-table-name">{name}</span>
          </Tooltip>
          {row.context?.fileCount && (
            <span className="mod-import-workflow-table-file-count">
              ({row.context.fileCount} {t("common.files")})
            </span>
          )}
        </div>
      ),
    },
    {
      title: t("common.category"),
      dataIndex: "category",
      key: "category",
      width: "15%",
      ellipsis: true,
      sorter: (a, b) => {
        const catA = a.context?.categoryName || "";
        const catB = b.context?.categoryName || "";
        return catA.localeCompare(catB);
      },
      showSorterTooltip: false,
      render: (_: unknown, row: WorkflowTableRow) => {
        const categoryName = row.context?.categoryName;
        if (!categoryName) {
          return <span style={{ color: "#8c8c8c" }}>{t("common.notSet")}</span>;
        }
        return <span>{categoryName}</span>;
      },
    },
    {
      title: t("common.status"),
      dataIndex: "statusText",
      key: "status",
      width: "15%",
      sorter: (a, b) => {
        // Define status priority for sorting
        const statusPriority: Record<WorkflowStatus, number> = {
          [WorkflowStatus.Processing]: 1,
          [WorkflowStatus.WaitingForInput]: 2,
          [WorkflowStatus.Pending]: 3,
          [WorkflowStatus.Paused]: 4,
          [WorkflowStatus.Deleting]: 5,
          [WorkflowStatus.Completed]: 6,
          [WorkflowStatus.Failed]: 7,
          [WorkflowStatus.Cancelled]: 8,
        };
        return statusPriority[a.workflow.status] - statusPriority[b.workflow.status];
      },
      showSorterTooltip: false,
      render: (statusText: string, row: WorkflowTableRow) => {
        const tag = <StatusTag tone={getStatusTone(row.workflow.status)} label={statusText} />;

        // Show error message in tooltip for failed workflows
        if (row.workflow.status === WorkflowStatus.Failed && row.workflow.errorMessage) {
          // Use shared error translation logic
          const errorMessage = translateErrorMessage(row.workflow.errorMessage, 'WORKFLOW_UNKNOWN_ERROR');

          return (
            <Tooltip
              title={errorMessage}
              placement="bottom"
              overlayStyle={{ maxWidth: 'calc(100vw - 200px)' }}
              overlayInnerStyle={{ whiteSpace: 'normal' }}
            >
              {tag}
            </Tooltip>
          );
        }

        return tag;
      },
    },
    {
      title: t("workflow.queue.progress"),
      dataIndex: "progress",
      key: "progress",
      width: "25%",
      sorter: (a, b) => a.progress - b.progress,
      showSorterTooltip: false,
      render: (progress: number, row: WorkflowTableRow) => {
        const status =
          row.workflow.status === WorkflowStatus.Failed
            ? "exception"
            : row.workflow.status === WorkflowStatus.Completed
              ? "success"
              : "active";

        return (
          <Progress
            percent={progress}
            status={status}
            size="small"
            strokeColor={
              row.workflow.status === WorkflowStatus.WaitingForInput
                ? "#faad14"
                : undefined
            }
          />
        );
      },
    },
    {
      title: t("common.actions"),
      key: "actions",
      width: "15%",
      render: (_: unknown, row: WorkflowTableRow) => {
        const { workflow } = row;
        const isActive =
          workflow.status !== WorkflowStatus.Completed &&
          workflow.status !== WorkflowStatus.Failed &&
          workflow.status !== WorkflowStatus.Cancelled &&
          workflow.status !== WorkflowStatus.Deleting;

        return (
          <Space size="small">
            {/* Confirm button - show for WaitingForInput status */}
            {workflow.status === WorkflowStatus.WaitingForInput && (
              <Tooltip title={t("workflow.queue.confirm")}>
                <CompactButton.Success
                  type="primary"
                  size="small"
                  shape="default"
                  icon={<CheckCircleOutlined />}
                  onClick={() => handleConfirm(workflow.id)}
                />
              </Tooltip>
            )}

            {/* Edit button - always show for active workflows */}
            {isActive && (
              <Tooltip title={t("common.edit")}>
                <CompactButton.Primary
                  size="small"
                  shape="default"
                  icon={<EditOutlined />}
                  onClick={() => handleEditMetadata(row)}
                />
              </Tooltip>
            )}

            {/* Pause button - show when Pending or Processing */}
            {(workflow.status === WorkflowStatus.Pending || workflow.status === WorkflowStatus.Processing) && (
              <Tooltip title={t("workflow.queue.pause")}>
                <CompactButton.Warning
                  size="small"
                  shape="default"
                  icon={<PauseCircleOutlined />}
                  onClick={() => handlePauseWorkflow(workflow.id)}
                />
              </Tooltip>
            )}

            {/* Resume button - show when Paused */}
            {workflow.status === WorkflowStatus.Paused && (
              <Tooltip title={t("workflow.queue.resume")}>
                <CompactButton.Success
                  size="small"
                  shape="default"
                  icon={<PlayCircleOutlined />}
                  onClick={() => handleResumeWorkflow(workflow.id)}
                />
              </Tooltip>
            )}

            {/* Delete button - hide during final import step (after user confirmation) */}
            {!(row.context?.step === ModImportWorkflowSteps.ImportMod && workflow.status === WorkflowStatus.Processing) && (
              <Tooltip title={t("common.delete")}>
                <CompactButton.Danger
                  size="small"
                  shape="default"
                  icon={<DeleteOutlined />}
                  onClick={() => handleDeleteWorkflow(workflow.id)}
                  danger
                />
              </Tooltip>
            )}
          </Space>
        );
      },
    },
  ], [t, handleEditMetadata, handleConfirm, handlePauseWorkflow, handleResumeWorkflow, handleDeleteWorkflow, getStatusTone]);

  const rowSelection = useMemo(() => ({
    selectedRowKeys,
    onChange: (keys: React.Key[]) => setSelectedRowKeys(keys as string[]),
    getCheckboxProps: () => ({
      disabled: false, // Allow selection of all workflows for batch operations
    }),
  }), [selectedRowKeys, setSelectedRowKeys]);

  /**
   * Handle row click to toggle expansion
   */
  const handleRowClick = useCallback((record: WorkflowTableRow) => {
    const key = record.workflow.id;
    setExpandedRowKeys((prev) =>
      prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key]
    );
  }, []);

  return (
    <>
      <DataTable<WorkflowTableRow>
        className="mod-import-workflow-table"
        columns={columns}
        dataSource={tableData}
        rowKey={(row) => row.workflow.id}
        pagination={false}
        size="middle"
        rowSelection={rowSelection}
        sticky={{ offsetHeader: 0 }}
        scroll={{ y: 'calc(100vh - 282px)' }}
        expandable={{
          expandedRowRender: renderExpandedRow,
          rowExpandable: () => true,
          expandedRowKeys: expandedRowKeys,
          onExpandedRowsChange: (keys) => setExpandedRowKeys(keys as string[]),
          showExpandColumn: false, // Hide the expand (+) button column
        }}
        onRow={(record) => ({
          onClick: (e) => {
            // Don't toggle if clicking on buttons, checkboxes, or interactive elements
            const target = e.target as HTMLElement;
            if (
              target.closest('button') ||
              target.closest('.ant-checkbox-wrapper') ||
              target.closest('a')
            ) {
              return;
            }
            handleRowClick(record);
          },
          style: { cursor: 'pointer' },
        })}
        emptyText={
          <Empty
            description={t("workflow.queue.empty")}
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          />
        }
      />

      {/* Metadata Edit Dialog */}
      <ModImportMetadataDialog
        visible={metadataModalVisible}
        workflow={selectedWorkflow}
        context={selectedContext}
        onCancel={handleMetadataCancel}
        onSubmit={handleMetadataSubmit}
      />
    </>
  );
};
