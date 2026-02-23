import React, { useState } from 'react';
import { List, Progress, Tag, Empty, Tabs, Badge } from 'antd';
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  LoadingOutlined,
  StopOutlined,
  DeleteOutlined,
} from '@ant-design/icons';
import { useOperation } from '../../context/OperationContext';
import { OperationProgress, OperationStatus } from '../../types/operation.types';
import { CompactButton } from '../compact';
import { useTranslation } from 'react-i18next';
import './OperationMonitorScreen.css';

interface OperationMonitorScreenProps {
  onClose: () => void;
}

const OperationMonitorScreen: React.FC<OperationMonitorScreenProps> = ({ onClose }) => {
  const { t } = useTranslation();
  const { state, actions } = useOperation();
  const { activeOperations, completedOperations, failedOperations } = state;
  const [activeTab, setActiveTab] = useState<string>('active');

  // Format duration
  const formatDuration = (startedAt: Date, completedAt?: Date) => {
    const end = completedAt || new Date();
    const durationMs = end.getTime() - startedAt.getTime();
    const seconds = Math.floor(durationMs / 1000);
    const minutes = Math.floor(seconds / 60);

    if (minutes > 0) {
      return `${minutes}m ${seconds % 60}s`;
    }
    return `${seconds}s`;
  };

  // Get status icon
  const getStatusIcon = (status: OperationStatus) => {
    switch (status) {
      case 'Running':
        return <LoadingOutlined className="operation-status-icon-running" spin />;
      case 'Completed':
        return <CheckCircleOutlined className="operation-status-icon-completed" />;
      case 'Failed':
        return <CloseCircleOutlined className="operation-status-icon-failed" />;
      case 'Cancelled':
        return <StopOutlined className="operation-status-icon-cancelled" />;
    }
  };

  // Get status tag
  const getStatusTag = (status: OperationStatus) => {
    const colors = {
      Running: 'blue',
      Completed: 'green',
      Failed: 'red',
      Cancelled: 'orange',
    };
    const labels = {
      Running: t('operationMonitor.status.running'),
      Completed: t('operationMonitor.status.completed'),
      Failed: t('operationMonitor.status.failed'),
      Cancelled: t('operationMonitor.status.cancelled'),
    };
    return <Tag color={colors[status]}>{labels[status]}</Tag>;
  };

  // Render operation item
  const renderOperation = (operation: OperationProgress): React.ReactElement => {
    const isActive = operation.status === 'Running';

    const content = (
      <>
        <div className="operation-header">
          <div className="operation-info">
            {getStatusIcon(operation.status)}
            <span className="operation-name">{operation.operationName}</span>
            {getStatusTag(operation.status)}
          </div>
          <span className="operation-duration">
            {formatDuration(operation.startedAt, operation.completedAt)}
          </span>
        </div>
        {isActive && (
          <div className="operation-progress">
            <Progress
              percent={operation.percentComplete}
              size="small"
              status={operation.percentComplete === 100 ? 'success' : 'active'}
              format={(percent) => `${percent}%`}
            />
          </div>
        )}
        {operation.currentStep && (
          <div className="operation-current-step">
            {operation.currentStep}
          </div>
        )}
        {operation.errorMessage && (
          <div className="operation-error">
            <strong>{t('operationMonitor.error')}:</strong> {operation.errorMessage}
          </div>
        )}
        {operation.metadata && (
          <div className="operation-metadata">
            {typeof operation.metadata === 'string'
              ? operation.metadata
              : JSON.stringify(operation.metadata)}
          </div>
        )}
      </>
    );

    return <div className="operation-item">{content}</div>;
  };

  // Tab items
  const tabItems = [
    {
      key: 'active',
      label: (
        <Badge count={activeOperations.length} offset={[10, 0]}>
          <span>{t('operationMonitor.tabs.active')}</span>
        </Badge>
      ),
      children: (
        <>
          {activeOperations.length === 0 ? (
            <Empty
              description={t('operationMonitor.empty.active')}
              className="operation-empty"
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          ) : (
            <List
              dataSource={activeOperations}
              renderItem={renderOperation}
              rowKey={(item) => item.operationId}
              className="operation-list"
            />
          )}
        </>
      ),
    },
    {
      key: 'completed',
      label: (
        <Badge count={completedOperations.length} offset={[10, 0]} color="green">
          <span>{t('operationMonitor.tabs.completed')}</span>
        </Badge>
      ),
      children: (
        <>
          {completedOperations.length === 0 ? (
            <Empty
              description={t('operationMonitor.empty.completed')}
              className="operation-empty"
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          ) : (
            <>
              <div className="operation-clear-container">
                <CompactButton
                  size="small"
                  icon={<DeleteOutlined />}
                  onClick={actions.clearCompleted}
                >
                  {t('operationMonitor.clearAll')}
                </CompactButton>
              </div>
              <List
                dataSource={completedOperations}
                renderItem={renderOperation}
                rowKey={(item) => item.operationId}
                className="operation-list"
              />
            </>
          )}
        </>
      ),
    },
    {
      key: 'failed',
      label: (
        <Badge count={failedOperations.length} offset={[10, 0]} color="red">
          <span>{t('operationMonitor.tabs.failed')}</span>
        </Badge>
      ),
      children: (
        <>
          {failedOperations.length === 0 ? (
            <Empty
              description={t('operationMonitor.empty.failed')}
              className="operation-empty"
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          ) : (
            <>
              <div className="operation-clear-container">
                <CompactButton
                  size="small"
                  icon={<DeleteOutlined />}
                  onClick={actions.clearFailed}
                  danger
                >
                  {t('operationMonitor.clearAll')}
                </CompactButton>
              </div>
              <List
                dataSource={failedOperations}
                renderItem={renderOperation}
                rowKey={(item) => item.operationId}
                className="operation-list"
              />
            </>
          )}
        </>
      ),
    },
  ];

  // Total count
  const totalCount = activeOperations.length + completedOperations.length + failedOperations.length;

  return (
    <div className="operation-monitor-screen">
      {/* Header */}
      <div className="operation-monitor-header">
        <div className="operation-monitor-header-content">
          <div>
            <h2 className="operation-monitor-title">{t('operationMonitor.title')}</h2>
            <div className="operation-monitor-subtitle">
              {t('operationMonitor.totalOperations', { count: totalCount })}
            </div>
          </div>
          <CompactButton onClick={onClose}>{t('operationMonitor.close')}</CompactButton>
        </div>
      </div>

      {/* Content */}
      <div className="operation-monitor-content">
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={tabItems}
          className="operation-monitor-tabs"
          tabBarStyle={{ padding: '0 16px', marginBottom: 0 }}
        />
      </div>
    </div>
  );
};

export default OperationMonitorScreen;
