import React from 'react';
import { Space, Alert, Card, Row, Col, Statistic, Divider, List, Typography } from 'antd';
import { useMigrationWizard } from '../../context/MigrationWizardContext';
import { useTranslation } from 'react-i18next';
import './MigrationSteps.css';

const { Text } = Typography;

/**
 * Step 4: Complete
 * Shows migration results
 */
export const CompleteStep: React.FC = () => {
  const { t } = useTranslation();
  const { result } = useMigrationWizard();

  if (!result) {
    return (
      <Alert
        title={t('migration.complete.noResults')}
        description={t('migration.complete.noResultsDescription')}
        type="warning"
        showIcon
      />
    );
  }

  return (
    <Space orientation="vertical" className="migration-step-container" size="large">
      <Alert
        title={result.success ? t('migration.complete.successTitle') : t('migration.complete.errorTitle')}
        description={
          result.success
            ? t('migration.complete.successDescription')
            : t('migration.complete.errorDescription')
        }
        type={result.success ? 'success' : 'warning'}
        showIcon
      />

      <Card title={t('migration.complete.summary')}>
        <Row gutter={[16, 16]}>
          <Col span={12}>
            <Statistic title={t('migration.complete.modsMigrated')} value={result.modsMigrated} />
          </Col>
          <Col span={12}>
            <Statistic title={t('migration.complete.archivesCopied')} value={result.archivesCopied} />
          </Col>
          <Col span={12}>
            <Statistic title={t('migration.complete.previewsCopied')} value={result.previewsCopied} />
          </Col>
          <Col span={12}>
            <Statistic title={t('migration.complete.duration')} value={result.duration} />
          </Col>
        </Row>

        {result.warnings.length > 0 && (
          <>
            <Divider />
            <Alert
              title={t('migration.complete.warningsCount', { count: result.warnings.length })}
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
              title={t('migration.complete.errorsCount', { count: result.errors.length })}
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
        <Text type="secondary">{t('migration.complete.logFile', { path: result.logFilePath })}</Text>
      </Card>
    </Space>
  );
};
