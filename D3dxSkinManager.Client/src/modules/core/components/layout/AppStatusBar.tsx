import React from 'react';
import { Space, Tag, Progress, Button } from 'antd';
import {
  CheckCircleOutlined,
  LoadingOutlined,
  QuestionCircleOutlined,
} from '@ant-design/icons';
import { AnnotatedTooltip, annotations } from '../../../../shared/components/common/TooltipSystem';

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
  const getServerStatusIcon = () => {
    switch (serverStatus) {
      case 'connected':
        return <CheckCircleOutlined style={{ color: 'var(--color-success)' }} />;
      case 'connecting':
        return <LoadingOutlined style={{ color: 'var(--color-primary)' }} />;
      case 'disconnected':
        return <CheckCircleOutlined style={{ color: 'var(--color-error)' }} />;
    }
  };

  const getServerStatusText = () => {
    switch (serverStatus) {
      case 'connected':
        return 'Connected';
      case 'connecting':
        return 'Connecting...';
      case 'disconnected':
        return 'Disconnected';
    }
  };

  // Get color for status message based on type
  const getStatusColor = (): string => {
    switch (statusType) {
      case 'error':
        return 'var(--color-error)';
      case 'warning':
        return 'var(--color-warning)';
      case 'normal':
      default:
        return 'var(--color-text-secondary)';
    }
  };

  return (
    <div
      style={{
        background: 'var(--color-bg-spotlight)',
        borderTop: '1px solid var(--color-border-secondary)',
      }}
    >
      {/* Progress bar - shown when progressVisible is true */}
      {progressVisible && (
        <div
          onClick={onProgressClick}
          style={{ cursor: onProgressClick ? 'pointer' : 'default' }}
          title={operationName || 'Operation in progress'}
        >
          <Progress
            percent={progressPercent}
            size="small"
            showInfo={false}
            status={progressPercent === 100 ? 'success' : 'active'}
            style={{ marginBottom: 0, lineHeight: '2px' }}
          />
        </div>
      )}

      {/* Main status bar */}
      <div
        style={{
          height: '32px',
          lineHeight: '32px',
          padding: '0 16px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          fontSize: '12px',
        }}
      >
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
              <LoadingOutlined style={{ color: 'var(--color-primary)' }} />
              <span style={{ color: 'var(--color-text-secondary)', fontWeight: 500 }}>
                {operationName}
              </span>
              {activeOperationCount > 1 && (
                <Tag color="blue" style={{ marginLeft: 4 }}>
                  +{activeOperationCount - 1} more
                </Tag>
              )}
            </Space>
          ) : statusMessage ? (
            <span style={{ color: getStatusColor(), fontWeight: 500 }}>
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
                style={{ padding: 0, height: 'auto', fontSize: '12px' }}
              >
                Help
              </Button>
            </AnnotatedTooltip>
          )}

          <AnnotatedTooltip
            title={annotations.statusBar.modsCount.title}
            level={annotations.statusBar.modsCount.level}
          >
            <Space size="small">
              <Tag color={modsLoaded > 0 ? 'green' : 'default'}>
                {modsLoaded} / {modsTotal} Mods
              </Tag>
            </Space>
          </AnnotatedTooltip>

          <span style={{ color: 'var(--color-text-tertiary)' }}>
            D3dxSkinManager v1.0.0
          </span>
        </Space>
      </div>
    </div>
  );
};
