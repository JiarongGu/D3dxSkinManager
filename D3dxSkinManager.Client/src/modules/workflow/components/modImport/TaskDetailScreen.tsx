import React, { useMemo } from "react";
import { Descriptions } from "antd";
import { useTranslation } from "react-i18next";
import { StatusTag, StatusTone } from "../../../../shared/components/common/StatusTag";
import { WorkflowInfo, WorkflowStatus } from "../../types/workflow.types";
import { formatDateTime } from "../../../../shared/utils/formatDate";
import { translateErrorMessage } from "../../../../shared/utils/errorHandler";
import "./TaskDetailScreen.css";

interface TaskDetailScreenProps {
  workflow: WorkflowInfo;
}

const STATUS_TONE: Record<WorkflowStatus, StatusTone> = {
  [WorkflowStatus.Pending]: "info",
  [WorkflowStatus.Processing]: "processing",
  [WorkflowStatus.WaitingForInput]: "warning",
  [WorkflowStatus.Completed]: "success",
  [WorkflowStatus.Failed]: "error",
  [WorkflowStatus.Deleting]: "warning",
  [WorkflowStatus.Paused]: "neutral",
  [WorkflowStatus.Cancelled]: "neutral",
};

/**
 * Detail screen for ONE import queue task, opened from a row. The queue is shared (local mod imports +
 * remote downloads run on the same actor), so the body is TYPE-SWITCHED on `workflow.type`:
 * MOD_IMPORT shows the folder / steps / metadata; REMOTE_IMPORT shows the source / download host / url /
 * tags / target category (from the RemoteImportJob context). Read-only — actions stay on the table row.
 */
export const TaskDetailScreen: React.FC<TaskDetailScreenProps> = ({ workflow }) => {
  const { t } = useTranslation();
  const isRemote = workflow.type === "REMOTE_IMPORT";

  // The context is a ModImportWorkflowContext (local) or a two-stage RemoteImportWorkflowContext (remote:
  // { job, stage, download }) — both JSON. The remote job fields live under `job` (legacy bare-job rows
  // put them at the top level → fall back to ctx).
  const ctx = useMemo<Record<string, unknown> & { [k: string]: any }>(() => {
    try {
      return JSON.parse(workflow.context) ?? {};
    } catch {
      return {};
    }
  }, [workflow.context]);

  const remote = isRemote ? ((ctx.job as Record<string, any>) ?? ctx) : ctx;
  const title = isRemote ? remote.detail?.title : (ctx.name || ctx.folderName);
  const dash = "—";
  const val = (v: unknown) => (v === undefined || v === null || v === "" ? dash : String(v));
  const tags: string[] = Array.isArray(remote.tags) ? remote.tags : [];

  return (
    <div className="task-detail">
      <div className="task-detail__head">
        <div className="task-detail__title">{title || t("workflow.modImport.unknownName")}</div>
        <div className="task-detail__badges">
          <StatusTag tone="info" icon={null} label={isRemote ? t("workflow.detail.typeRemote") : t("workflow.detail.typeLocal")} />
          <StatusTag tone={STATUS_TONE[workflow.status] ?? "neutral"} icon={null} label={t(`workflow.status.${workflow.status}`, String(workflow.status))} />
        </div>
      </div>

      <Descriptions className="task-detail__desc" bordered size="small" column={1}>
        <Descriptions.Item label={t("workflow.queue.workflowId")}>{workflow.id}</Descriptions.Item>
        <Descriptions.Item label={t("workflow.queue.createdAt")}>{workflow.createdAt ? formatDateTime(workflow.createdAt) : dash}</Descriptions.Item>
        {workflow.completedAt && (
          <Descriptions.Item label={t("workflow.queue.completedAt")}>{formatDateTime(workflow.completedAt)}</Descriptions.Item>
        )}
        {workflow.status === WorkflowStatus.Failed && workflow.errorMessage && (
          <Descriptions.Item label={t("workflow.queue.error")}>
            <span className="task-detail__error">{translateErrorMessage(workflow.errorMessage, "WORKFLOW_UNKNOWN_ERROR")}</span>
          </Descriptions.Item>
        )}
      </Descriptions>

      {isRemote ? (
        <Descriptions className="task-detail__desc" title={t("workflow.detail.remoteSection")} bordered size="small" column={1}>
          <Descriptions.Item label={t("workflow.detail.source")}>{val(remote.sourceId)}</Descriptions.Item>
          <Descriptions.Item label={t("workflow.detail.host")}>{val(remote.option?.name)}</Descriptions.Item>
          <Descriptions.Item label={t("workflow.detail.downloadType")}>{val(remote.option?.type)}</Descriptions.Item>
          <Descriptions.Item label={t("workflow.detail.url")}><span className="task-detail__mono">{val(remote.option?.url)}</span></Descriptions.Item>
          <Descriptions.Item label={t("workflow.detail.detailUrl")}><span className="task-detail__mono">{val(remote.detail?.detailUrl)}</span></Descriptions.Item>
          <Descriptions.Item label={t("workflow.detail.targetCategory")}>{val(remote.categoryId)}</Descriptions.Item>
          <Descriptions.Item label={t("workflow.detail.tags")}>{tags.length ? tags.join(", ") : dash}</Descriptions.Item>
        </Descriptions>
      ) : (
        <Descriptions className="task-detail__desc" title={t("workflow.detail.localSection")} bordered size="small" column={1}>
          <Descriptions.Item label={t("workflow.queue.folderPath")}><span className="task-detail__mono">{val(ctx.folderPath)}</span></Descriptions.Item>
          <Descriptions.Item label={t("workflow.queue.step")}>{val(ctx.step)}</Descriptions.Item>
          <Descriptions.Item label={t("workflow.queue.fileCount")}>{val(ctx.fileCount)}</Descriptions.Item>
          {ctx.tempArchivePath && (
            <Descriptions.Item label={t("workflow.queue.tempArchivePath")}><span className="task-detail__mono">{String(ctx.tempArchivePath)}</span></Descriptions.Item>
          )}
          <Descriptions.Item label={t("common.name")}>{val(ctx.name)}</Descriptions.Item>
          <Descriptions.Item label={t("workflow.detail.author")}>{val(ctx.author)}</Descriptions.Item>
          <Descriptions.Item label={t("common.category")}>{val(ctx.categoryName || ctx.category)}</Descriptions.Item>
          <Descriptions.Item label={t("workflow.detail.tags")}>{tags.length ? tags.join(", ") : dash}</Descriptions.Item>
          {ctx.description && <Descriptions.Item label={t("workflow.detail.description")}>{String(ctx.description)}</Descriptions.Item>}
        </Descriptions>
      )}
    </div>
  );
};
