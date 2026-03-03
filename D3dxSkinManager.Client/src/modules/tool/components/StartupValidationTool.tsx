import { notification } from '../../../shared/utils/notification';
import React, { useState } from 'react';
import { Card } from 'antd';
import {
  CheckCircleOutlined,
  ReloadOutlined,
  WarningOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons';
import { validationService, StartupValidationReport, ValidationSeverity } from '../services/validationService';
import {
  CompactCard,
  CompactSpace,
  CompactAlert,
  CompactButton,
} from '../../../shared/components/compact';
import { useTranslation } from 'react-i18next';
import './StartupValidationTool.css';

/**
 * StartupValidationTool - Validates system startup requirements
 *
 * Features:
 * - Run validation checks for directories, 3DMigoto, configuration, database, etc.
 * - Display validation results with color-coded severity
 * - Show detailed messages for each check
 */
export const StartupValidationTool: React.FC = () => {
  const { t } = useTranslation();
  const [validationReport, setValidationReport] = useState<StartupValidationReport>();
  const [validationLoading, setValidationLoading] = useState(false);

  /**
   * Run startup validation
   */
  const handleRunValidation = async () => {
    try {
      setValidationLoading(true);
      const report = await validationService.validateStartup();
      setValidationReport(report);

      if (report.isValid) {
        notification.success(t('validation.allPassed'));
      } else if (report.errorCount > 0) {
        notification.error(t('validation.failed', { errorCount: report.errorCount, warningCount: report.warningCount }));
      } else {
        notification.warning(t('validation.passedWithWarnings', { warningCount: report.warningCount }));
      }
    } catch (error: unknown) {
      notification.error(t('validation.runFailed'));
          } finally {
      setValidationLoading(false);
    }
  };

  const getCardClass = (result: any) => {
    if (result.isValid) return 'validation-result-card validation-result-card-success';
    if (result.severity === ValidationSeverity.Error) return 'validation-result-card validation-result-card-error';
    if (result.severity === ValidationSeverity.Warning) return 'validation-result-card validation-result-card-warning';
    return 'validation-result-card validation-result-card-info';
  };

  const getIconComponent = (result: any) => {
    if (result.isValid) return <CheckCircleOutlined className="validation-icon-success" />;
    if (result.severity === ValidationSeverity.Error) return <ExclamationCircleOutlined className="validation-icon-error" />;
    if (result.severity === ValidationSeverity.Warning) return <WarningOutlined className="validation-icon-warning" />;
    return <ExclamationCircleOutlined className="validation-icon-info" />;
  };

  return (
    <CompactCard
      title={<><CheckCircleOutlined /> {t('validation.title')}</>}
      extra={
        <CompactButton
          type="primary"
          icon={<ReloadOutlined />}
          onClick={handleRunValidation}
          loading={validationLoading}
        >
          {t('validation.runValidation')}
        </CompactButton>
      }
    >
      <CompactSpace vertical className="validation-container">
        {!validationReport && (
          <CompactAlert
            title={t('validation.systemValidation')}
            description={t('validation.description')}
            type="info"
            showIcon
          />
        )}

        {validationReport && (
          <>
            {/* Validation Summary */}
            <CompactAlert
              title={validationReport.isValid ? t('validation.summaryPassed') : t('validation.summaryFailed')}
              description={t('validation.summaryDetails', {
                passed: validationReport.results.filter(r => r.isValid).length,
                total: validationReport.results.length,
                errors: validationReport.errorCount > 0 ? t('validation.errors', { count: validationReport.errorCount }) : '',
                warnings: validationReport.warningCount > 0 ? t('validation.warnings', { count: validationReport.warningCount }) : ''
              })}
              type={validationReport.isValid ? (validationReport.warningCount > 0 ? 'warning' : 'success') : 'error'}
              showIcon
            />

            {/* Validation Results */}
            <CompactSpace orientation="vertical" className="validation-container">
              {validationReport.results.map((result, index) => (
                <Card key={index} size="small" className={getCardClass(result)}>
                  <CompactSpace>
                    {getIconComponent(result)}
                    <div>
                      <div className="validation-check-name">{result.checkName}</div>
                      <div className="validation-message">
                        {result.message}
                      </div>
                    </div>
                  </CompactSpace>
                </Card>
              ))}
            </CompactSpace>
          </>
        )}
      </CompactSpace>
    </CompactCard>
  );
};
