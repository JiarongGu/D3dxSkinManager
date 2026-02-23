import React from 'react';
import { Row, Col, List, Typography } from 'antd';
import { CompactSpace, CompactAlert, CompactCard, CompactDivider } from '../../../../shared/components/compact';
import { useMigrationWizard } from '../../context/MigrationWizardContext';
import { useTranslation } from 'react-i18next';
import './MigrationSteps.css';
import './CompleteStep.css';

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
      <CompactAlert
        title={t('migration.complete.noResults')}
        description={t('migration.complete.noResultsDescription')}
        type="warning"
        showIcon
        extraCompact
      />
    );
  }

  return (
    <CompactSpace vertical className="migration-step-container">
      <CompactAlert
        title={result.success ? t('migration.complete.successTitle') : t('migration.complete.errorTitle')}
        description={
          result.success
            ? t('migration.complete.successDescription')
            : result.failedAtStep && result.failedStepName
            ? t('migration.complete.errorDescriptionWithStep', {
                step: result.failedAtStep,
                stepName: result.failedStepName
              })
            : t('migration.complete.errorDescription')
        }
        type={result.success ? 'success' : 'warning'}
        showIcon
        extraCompact
      />

      <CompactCard title={t('migration.complete.summary')} extraCompact className="complete-step-card">
        <Row gutter={8}>
          <Col span={12}>
            <div className="complete-step-stat">
              <div className="complete-step-stat-label">{t('migration.complete.modsMigrated')}</div>
              <div className="complete-step-stat-value">{result.modsMigrated}</div>
            </div>
          </Col>
          <Col span={12}>
            <div className="complete-step-stat">
              <div className="complete-step-stat-label">{t('migration.complete.archivesCopied')}</div>
              <div className="complete-step-stat-value">{result.archivesCopied}</div>
            </div>
          </Col>
          <Col span={12}>
            <div className="complete-step-stat">
              <div className="complete-step-stat-label">{t('migration.complete.previewsCopied')}</div>
              <div className="complete-step-stat-value">{result.previewsCopied}</div>
            </div>
          </Col>
          <Col span={12}>
            <div className="complete-step-stat">
              <div className="complete-step-stat-label">{t('migration.complete.duration')}</div>
              <div className="complete-step-stat-value">{result.duration}</div>
            </div>
          </Col>
        </Row>

        {result.warnings.length > 0 && (
          <>
            <CompactDivider extraCompact />
            <CompactAlert
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
              extraCompact
            />
          </>
        )}

        {result.errors.length > 0 && (
          <>
            <CompactDivider extraCompact />
            <CompactAlert
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
              extraCompact
            />
          </>
        )}

        <CompactDivider extraCompact />
        <Text type="secondary">{t('migration.complete.logFile', { path: result.logFilePath })}</Text>
      </CompactCard>
    </CompactSpace>
  );
};
