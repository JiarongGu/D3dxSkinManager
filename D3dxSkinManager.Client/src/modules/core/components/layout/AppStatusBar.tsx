import React, { useState, useEffect } from "react";
import { Space, Tag, Progress, Button } from "antd";
import { LoadingOutlined, QuestionCircleOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { useModsStore } from "../../../mod/store/modsStore";
import { ModPresetMenu } from "./ModPresetMenu";
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

export type StatusType = "normal" | "warning" | "error";

interface AppStatusBarProps {
  operationName?: string; // Current operation name
  activeOperationCount?: number; // Number of active operations
  onHelpClick?: () => void;
  onProgressClick?: () => void; // Click handler for progress bar (opens operation monitor)
}

export const AppStatusBar: React.FC<AppStatusBarProps> = ({
  operationName,
  activeOperationCount = 0,
  onHelpClick,
  onProgressClick,
}) => {
  const { t } = useTranslation();

  const [statusMessage, setStatusMessage] = useState<string>("");
  const [statusType, setStatusType] = useState<StatusType>("normal");
  const [progressPercent, setProgressPercent] = useState<number>(0);
  const [progressVisible, setProgressVisible] = useState<boolean>(false);
  const [appVersion, setAppVersion] = useState<string>("1.0.0");

  // Get mod statistics from store
  // Note: statistics contains GLOBAL counts (all mods across all categories)
  // state.mods/categoryFilteredMods only contain currently filtered mods
  const statistics = useModsStore((state) => state.statistics);

  const modsLoaded = statistics?.loadedMods ?? 0;
  const modsTotal = statistics?.totalMods ?? 0;

  // Get app version from injected global variable
  useEffect(() => {
    const metadata = window.__APP_METADATA__;
    if (metadata?.version) {
      setAppVersion(metadata.version);
    }
  }, []);

  // Get CSS class for status message based on type
  const getStatusClass = (): string => {
    return `app-status-bar-status-message app-status-bar-status-message-${statusType}`;
  };

  return (
    <div className="app-status-bar">
      {/* Progress bar - shown when progressVisible is true */}
      {progressVisible && (
        <div
          onClick={onProgressClick}
          className={
            onProgressClick
              ? "app-status-bar-progress"
              : "app-status-bar-progress-default"
          }
          title={operationName || t("dialogs.operationMonitor.noOperations")}
        >
          <Progress
            percent={progressPercent}
            size="small"
            showInfo={false}
            status={progressPercent === 100 ? "success" : "active"}
            className="app-status-bar-progress-bar"
          />
        </div>
      )}

      {/* Main status bar */}
      <div className="app-status-bar-main">
        {/* Left side - Status Message */}
        <Space size="large">
          {/* Help link */}
          {onHelpClick && (
            <Button
              type="link"
              size="small"
              icon={<QuestionCircleOutlined />}
              onClick={onHelpClick}
              className="app-status-bar-help-button"
            >
              {t("statusBar.help")}
            </Button>
          )}

          {/* Operation name or status message */}
          {operationName && activeOperationCount > 0 ? (
            <Space size="small">
              <LoadingOutlined className="app-status-bar-status-icon-connecting" />
              <span className="app-status-bar-operation-text">
                {operationName}
              </span>
              {activeOperationCount > 1 && (
                <Tag color="blue" className="app-status-bar-operation-tag">
                  +{activeOperationCount - 1} more
                </Tag>
              )}
            </Space>
          ) : statusMessage ? (
            <span className={getStatusClass()}>{statusMessage}</span>
          ) : null}
        </Space>

        {/* Right side - Presets, Mods, Version (all aligned to right) */}
        <Space size="large" style={{ marginLeft: "auto" }}>
          <ModPresetMenu />

          <Tag color={modsLoaded > 0 ? "green" : "default"}>
            {t("statusBar.modsLoaded", { count: modsLoaded, total: modsTotal })}
          </Tag>

          <span className="app-status-bar-version">D3dxSkinManager v{appVersion}</span>
        </Space>
      </div>
    </div>
  );
};
