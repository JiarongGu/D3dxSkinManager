import React, { useEffect, useRef, useState } from "react";
import { Space, Tag, Popover } from "antd";
import { LoadingOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { useModsStore } from "../../../mod/store/modsStore";
import { useProcessStore } from "../../../../shared/store/processStore";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { eventBus, Module, SystemEventType } from "../../../../shared/services/eventBus";
import { api } from "../../../../shared/services/ipc";
import { ModPresetMenu } from "./ModPresetMenu";
import { ActivityPanel } from "./ActivityPanel";
import { LaunchButton } from "./LaunchButton";
import "./AppStatusBar.css";

// Global app metadata injected by backend
interface AppMetadata {
  name: string;
  version: string;
}

declare global {
  interface Window {
    __APP_METADATA__?: AppMetadata;
  }
}

interface AppStatusBarProps {
  onHelpClick?: () => void;
}

export const AppStatusBar: React.FC<AppStatusBarProps> = ({ onHelpClick }) => {
  const { t } = useTranslation();
  const [appVersion, setAppVersion] = useState("1.0.0");
  const [activityOpen, setActivityOpen] = useState(false);
  const barRef = useRef<HTMLDivElement>(null);

  // Mod statistics
  const statistics = useModsStore((state) => state.statistics);
  const modsLoaded = statistics?.loadedMods ?? 0;
  const modsTotal = statistics?.totalMods ?? 0;

  // Long-running processes (authoritative backend ProcessRegistry, mirrored in processStore).
  // The bar reflects RUNNING processes; the full list (incl. history) lives in the Activity panel.
  // The legacy frontend taskStore is abandoned — producers emit to the backend registry instead.
  const processes = useProcessStore((s) => s.processes);
  const running = processes.filter((p) => p.status === "running");
  const taskCount = running.length;
  const latestTask = taskCount > 0 ? running[taskCount - 1] : undefined;
  const latestProgress = latestTask?.progress;

  useEffect(() => {
    const metadata = window.__APP_METADATA__;
    if (metadata?.version) {
      setAppVersion(metadata.version);
    }
  }, []);

  // Resume dispatcher: when the user resumes an interrupted process (ProcessRegistry emits
  // PROCESS_RESUME_REQUESTED), the always-alive frontend re-invokes the op for the active profile.
  // Each resumable op type maps to its re-trigger here (the op itself must be idempotent/checkpointed).
  const selectedProfile = useProfile().selectedProfile;
  useEffect(() => {
    return eventBus.subscribe(Module.SYSTEM, SystemEventType.PROCESS_RESUME_REQUESTED, (e) => {
      const type = (e.payload as { type?: string } | undefined)?.type;
      const profileId = selectedProfile?.id;
      if (!profileId) return;
      if (type === "migration") void api.tool.executeModIdMigration(profileId);
      // add other resumable op types here as they opt in (set Resumable on the backend)
    });
  }, [selectedProfile?.id]);

  const taskPanel = (
    <div className="app-status-bar-task-panel">
      <div className="app-status-bar-task-panel-header">
        {t("statusBar.taskPanel.title")}
      </div>
      {taskCount === 0 ? (
        <div className="app-status-bar-task-panel-empty">
          {t("statusBar.taskPanel.noTasks")}
        </div>
      ) : (
        <div className="app-status-bar-task-panel-list">
          {running.map((task, idx) => (
            <React.Fragment key={task.id}>
              {idx > 0 && <div className="app-status-bar-task-panel-divider" />}
              <div className="app-status-bar-task-panel-item">
                <LoadingOutlined spin className="app-status-bar-task-panel-icon" />
                <span className="app-status-bar-task-panel-label">{task.title}</span>
                {task.progress !== undefined && (
                  <span className="app-status-bar-task-panel-progress">{task.progress}%</span>
                )}
              </div>
            </React.Fragment>
          ))}
        </div>
      )}
    </div>
  );

  return (
    <div className="app-status-bar" ref={barRef}>
      <div className="app-status-bar-main">
        {/* Left: Task area — hover triggers panel above entire footer */}
        <Popover
          content={taskPanel}
          trigger="hover"
          placement="top"
          arrow={false}
          overlayClassName="app-status-bar-task-popover"
          getPopupContainer={() => barRef.current || document.body}
        >
          <div
            className="app-status-bar-task-area"
            onClick={() => setActivityOpen(true)}
            title={t("activity.title")}
          >
            {taskCount > 0 && (
              <span className="app-status-bar-task-count">
                <LoadingOutlined spin /> {taskCount}
              </span>
            )}
            <div className="app-status-bar-progress-track">
              {taskCount > 0 ? (
                latestProgress !== undefined ? (
                  <div
                    className="app-status-bar-progress-fill"
                    style={{ width: `${latestProgress}%` }}
                  />
                ) : (
                  <div className="app-status-bar-progress-indeterminate" />
                )
              ) : null}
            </div>
          </div>
        </Popover>

        {/* Right side */}
        <Space size="large" style={{ marginLeft: "auto" }}>
          <LaunchButton />
          <ModPresetMenu />

          <Tag color={modsLoaded > 0 ? "green" : "default"}>
            {t("statusBar.modsLoaded", { count: modsLoaded, total: modsTotal })}
          </Tag>

          <span
            className="app-status-bar-version"
            onClick={onHelpClick}
          >
            D3dxSkinManager v{appVersion}
          </span>
        </Space>
      </div>

      <ActivityPanel open={activityOpen} onClose={() => setActivityOpen(false)} />
    </div>
  );
};
