import React from 'react';
import { Space, Tag, Progress, Button } from 'antd';
import {
  CheckCircleOutlined,
  LoadingOutlined,
  QuestionCircleOutlined,
} from '@ant-design/icons';
import { AnnotatedTooltip, annotations } from '../../../../shared/components/common/TooltipSystem';
import { useTranslation } from 'react-i18next';
import './AppStatusBar.css';

export type StatusType = 'normal' | 'warning' | 'error';

interface AppStatusBarProps {
  serverStatus: 'connected' | 'disconnected' | 'connecting';
  modsLoaded: number;
  modsTotal: number;
  statusMessage?: string;
  statusType?: StatusType;
  progressPercent?: number;
  progressVisible?: boolean;
  operationName?: string; // Current operation name
  activeOperationCount?: number; // Number of active operations
  onHelpClick?: () => void;
  onProgressClick?: () => void; // Click handler for progress bar (opens operation monitor)
}

export const AppStatusBar: React.FC<AppStatusBarProps> = ({
  serverStatus,
  modsLoaded,
  modsTotal,
  statusMessage,
  statusType = 'normal',
  progressPercent = 0,
  progressVisible = false,
  operationName,
  activeOperationCount = 0,
  onHelpClick,
  onProgressClick,
}) => {
  const { t } = useTranslation();

  const getServerStatusIcon = () => {
    const iconClass = `app-status-bar-status-icon-${serverStatus}`;
    switch (serverStatus) {
      case 'connected':
        return <CheckCircleOutlined className={iconClass} />;
      case 'connecting':
        return <LoadingOutlined className={iconClass} />;
      case 'disconnected':
        return <CheckCircleOutlined className={iconClass} />;
    }
  };

  const getServerStatusText = () => {
    switch (serverStatus) {
      case 'connected':
        return t('statusBar.connected');
      case 'connecting':
        return t('common.loading');
      case 'disconnected':
        return t('statusBar.disconnected');
    }
  };

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
          className={onProgressClick ? 'app-status-bar-progress' : 'app-status-bar-progress-default'}
          title={operationName || t('dialogs.operationMonitor.noOperations')}
        >
          <Progress
            percent={progressPercent}
            size="small"
            showInfo={false}
            status={progressPercent === 100 ? 'success' : 'active'}
            className="app-status-bar-progress-bar"
          />
        </div>
      )}

      {/* Main status bar */}
      <div className="app-status-bar-main">
        {/* Left side - Status, Message */}
        <Space size="large">
          <AnnotatedTooltip title="Backend connection status" level={2}>
            <Space size="small">
              {getServerStatusIcon()}
              <span>{getServerStatusText()}</span>
            </Space>
          </AnnotatedTooltip>

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
            <span className={getStatusClass()}>
              {statusMessage}
            </span>
          ) : null}
        </Space>

        {/* Right side - Help, Mods, Version */}
        <Space size="large">
          {/* Help link */}
          {onHelpClick && (
            <AnnotatedTooltip
              title={annotations.statusBar.helpButton.title}
              level={annotations.statusBar.helpButton.level}
            >
              <Button
                type="link"
                size="small"
                icon={<QuestionCircleOutlined />}
                onClick={onHelpClick}
                className="app-status-bar-help-button"
              >
                {t('statusBar.help')}
              </Button>
            </AnnotatedTooltip>
          )}

          <AnnotatedTooltip
            title={annotations.statusBar.modsCount.title}
            level={annotations.statusBar.modsCount.level}
          >
            <Space size="small">
              <Tag color={modsLoaded > 0 ? 'green' : 'default'}>
                {t('statusBar.modsLoaded', { count: modsLoaded, total: modsTotal })}
              </Tag>
            </Space>
          </AnnotatedTooltip>

          <span className="app-status-bar-version">
            D3dxSkinManager v1.0.0
          </span>
        </Space>
      </div>
    </div>
  );
};
