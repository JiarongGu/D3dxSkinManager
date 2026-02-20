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

interface OperationMonitorScreenProps {
  onClose: () => void;
}

const OperationMonitorScreen: React.FC<OperationMonitorScreenProps> = ({ onClose }) => {
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
        return <LoadingOutlined style={{ color: 'var(--color-primary)' }} spin />;
      case 'Completed':
        return <CheckCircleOutlined style={{ color: 'var(--color-success)' }} />;
      case 'Failed':
        return <CloseCircleOutlined style={{ color: 'var(--color-error)' }} />;
      case 'Cancelled':
        return <StopOutlined style={{ color: 'var(--color-warning)' }} />;
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
    return <Tag color={colors[status]}>{status}</Tag>;
  };

  // Render operation item
  const renderOperation = (operation: OperationProgress): React.ReactElement => {
    const isActive = operation.status === 'Running';

    const content = (
      <>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8 }}>
          <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
            {getStatusIcon(operation.status)}
            <span style={{ fontWeight: 500 }}>{operation.operationName}</span>
            {getStatusTag(operation.status)}
          </div>
          <span style={{ fontSize: '12px', color: 'var(--color-text-tertiary)' }}>
            {formatDuration(operation.startedAt, operation.completedAt)}
          </span>
        </div>
        {isActive && (
          <div style={{ marginBottom: 8 }}>
            <Progress
              percent={operation.percentComplete}
              size="small"
              status={operation.percentComplete === 100 ? 'success' : 'active'}
              format={(percent) => `${percent}%`}
            />
          </div>
        )}
        {operation.currentStep && (
          <div style={{ fontSize: '12px', color: 'var(--color-text-secondary)', marginBottom: 4 }}>
            {operation.currentStep}
          </div>
        )}
        {operation.errorMessage && (
          <div
            style={{
              fontSize: '12px',
              color: 'var(--color-error)',
              marginTop: 8,
              padding: '6px 8px',
              background: 'var(--color-error-bg)',
              borderRadius: '4px',
            }}
          >
            <strong>Error:</strong> {operation.errorMessage}
          </div>
        )}
        {operation.metadata && (
          <div style={{ fontSize: '11px', color: 'var(--color-text-tertiary)', marginTop: 4 }}>
            {typeof operation.metadata === 'string'
              ? operation.metadata
              : JSON.stringify(operation.metadata)}
          </div>
        )}
      </>
    );

    return <div style={{ padding: '12px 0' }}>{content}</div>;
  };

  // Tab items
  const tabItems = [
    {
      key: 'active',
      label: (
        <Badge count={activeOperations.length} offset={[10, 0]}>
          <span>Active</span>
        </Badge>
      ),
      children: (
        <>
          {activeOperations.length === 0 ? (
            <Empty
              description="No active operations"
              style={{ marginTop: 40 }}
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          ) : (
            <List
              dataSource={activeOperations}
              renderItem={renderOperation}
              rowKey={(item) => item.operationId}
              style={{ background: 'transparent' }}
            />
          )}
        </>
      ),
    },
    {
      key: 'completed',
      label: (
        <Badge count={completedOperations.length} offset={[10, 0]} color="green">
          <span>Completed</span>
        </Badge>
      ),
      children: (
        <>
          {completedOperations.length === 0 ? (
            <Empty
              description="No completed operations"
              style={{ marginTop: 40 }}
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          ) : (
            <>
              <div style={{ padding: '8px 16px', textAlign: 'right' }}>
                <CompactButton
                  size="small"
                  icon={<DeleteOutlined />}
                  onClick={actions.clearCompleted}
                >
                  Clear All
                </CompactButton>
              </div>
              <List
                dataSource={completedOperations}
                renderItem={renderOperation}
                rowKey={(item) => item.operationId}
                style={{ background: 'transparent' }}
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
          <span>Failed</span>
        </Badge>
      ),
      children: (
        <>
          {failedOperations.length === 0 ? (
            <Empty
              description="No failed operations"
              style={{ marginTop: 40 }}
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          ) : (
            <>
              <div style={{ padding: '8px 16px', textAlign: 'right' }}>
                <CompactButton
                  size="small"
                  icon={<DeleteOutlined />}
                  onClick={actions.clearFailed}
                  danger
                >
                  Clear All
                </CompactButton>
              </div>
              <List
                dataSource={failedOperations}
                renderItem={renderOperation}
                rowKey={(item) => item.operationId}
                style={{ background: 'transparent' }}
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
    <div
      style={{
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        background: 'var(--color-bg-container)',
      }}
    >
      {/* Header */}
      <div
        style={{
          padding: '16px 24px',
          borderBottom: '1px solid var(--color-border-secondary)',
          background: 'var(--color-bg-spotlight)',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <h2 style={{ margin: 0, fontSize: '18px' }}>Operation Monitor</h2>
            <div style={{ fontSize: '12px', color: 'var(--color-text-tertiary)', marginTop: 4 }}>
              {totalCount} total operation{totalCount !== 1 ? 's' : ''}
            </div>
          </div>
          <CompactButton onClick={onClose}>Close</CompactButton>
        </div>
      </div>

      {/* Content */}
      <div style={{ flex: 1, overflow: 'auto' }}>
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={tabItems}
          style={{ padding: '0' }}
          tabBarStyle={{ padding: '0 16px', marginBottom: 0 }}
        />
      </div>
    </div>
  );
};

export default OperationMonitorScreen;
