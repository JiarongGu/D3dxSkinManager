import React from 'react';
import { Space, Alert, Card, Row, Col, Statistic, Divider, List, Typography } from 'antd';
import { useMigrationWizard } from '../../context/MigrationWizardContext';

const { Text } = Typography;

/**
 * Step 4: Complete
 * Shows migration results
 */
export const CompleteStep: React.FC = () => {
  const { result } = useMigrationWizard();

  if (!result) {
    return (
      <Alert
        title="No Results"
        description="Migration result is not available."
        type="warning"
        showIcon
      />
    );
  }

  return (
    <Space orientation="vertical" style={{ width: '100%' }} size="large">
      <Alert
        title={result.success ? 'Migration Successful!' : 'Migration Completed with Errors'}
        description={
          result.success
            ? 'Your mods and configuration have been successfully migrated.'
            : 'Some errors occurred during migration. Check the log file for details.'
        }
        type={result.success ? 'success' : 'warning'}
        showIcon
      />

      <Card title="Migration Summary">
        <Row gutter={[16, 16]}>
          <Col span={12}>
            <Statistic title="Mods Migrated" value={result.modsMigrated} />
          </Col>
          <Col span={12}>
            <Statistic title="Archives Copied" value={result.archivesCopied} />
          </Col>
          <Col span={12}>
            <Statistic title="Previews Copied" value={result.previewsCopied} />
          </Col>
          <Col span={12}>
            <Statistic title="Duration" value={result.duration} />
          </Col>
        </Row>

        {result.warnings.length > 0 && (
          <>
            <Divider />
            <Alert
              title={`${result.warnings.length} Warning(s)`}
              description={
                <List
                  size="small"
                  dataSource={result.warnings.slice(0, 5)}
                  renderItem={(item) => <List.Item>{item}</List.Item>}
                />
              }
              type="warning"
              showIcon
            />
          </>
        )}

        {result.errors.length > 0 && (
          <>
            <Divider />
            <Alert
              title={`${result.errors.length} Error(s)`}
              description={
                <List
                  size="small"
                  dataSource={result.errors.slice(0, 5)}
                  renderItem={(item) => <List.Item>{item}</List.Item>}
                />
              }
              type="error"
              showIcon
            />
          </>
        )}

        <Divider />
        <Text type="secondary">Log file: {result.logFilePath}</Text>
      </Card>
    </Space>
  );
};
