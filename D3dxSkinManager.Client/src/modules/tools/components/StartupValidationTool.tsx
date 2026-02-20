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

/**
 * StartupValidationTool - Validates system startup requirements
 *
 * Features:
 * - Run validation checks for directories, 3DMigoto, configuration, database, etc.
 * - Display validation results with color-coded severity
 * - Show detailed messages for each check
 */
export const StartupValidationTool: React.FC = () => {
  const [validationReport, setValidationReport] = useState<StartupValidationReport | null>(null);
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
        notification.success('All validation checks passed!');
      } else if (report.errorCount > 0) {
        notification.error(`Validation failed: ${report.errorCount} errors, ${report.warningCount} warnings`);
      } else {
        notification.warning(`Validation passed with ${report.warningCount} warnings`);
      }
    } catch (error) {
      notification.error('Failed to run validation');
      console.error(error);
    } finally {
      setValidationLoading(false);
    }
  };

  return (
    <CompactCard
      title={<><CheckCircleOutlined /> Startup Validation</>}
      extra={
        <CompactButton
          type="primary"
          icon={<ReloadOutlined />}
          onClick={handleRunValidation}
          loading={validationLoading}
        >
          Run Validation
        </CompactButton>
      }
    >
      <CompactSpace vertical style={{ width: '100%' }}>
        {!validationReport && (
          <CompactAlert
            title="System Validation"
            description="Click 'Run Validation' to check directories, 3DMigoto installation, configuration, database, and required components."
            type="info"
            showIcon
          />
        )}

        {validationReport && (
          <>
            {/* Validation Summary */}
            <CompactAlert
              title={validationReport.isValid ? 'All Checks Passed' : 'Validation Issues Found'}
              description={`${validationReport.results.filter(r => r.isValid).length}/${validationReport.results.length} checks passed. ${
                validationReport.errorCount > 0 ? `${validationReport.errorCount} errors, ` : ''
              }${validationReport.warningCount > 0 ? `${validationReport.warningCount} warnings` : ''}`}
              type={validationReport.isValid ? (validationReport.warningCount > 0 ? 'warning' : 'success') : 'error'}
              showIcon
            />

            {/* Validation Results */}
            <CompactSpace orientation="vertical" style={{ width: '100%' }}>
              {validationReport.results.map((result, index) => (
                <Card key={index} size="small" style={{
                  borderLeft: `4px solid ${
                    result.isValid ? 'var(--color-success)' :
                    result.severity === ValidationSeverity.Error ? 'var(--color-error)' :
                    result.severity === ValidationSeverity.Warning ? 'var(--color-warning)' : 'var(--color-primary)'
                  }`,
                  marginBottom: '8px'
                }}>
                  <CompactSpace>
                    {result.isValid ? (
                      <CheckCircleOutlined style={{ color: 'var(--color-success)', fontSize: '16px' }} />
                    ) : result.severity === ValidationSeverity.Error ? (
                      <ExclamationCircleOutlined style={{ color: 'var(--color-error)', fontSize: '16px' }} />
                    ) : result.severity === ValidationSeverity.Warning ? (
                      <WarningOutlined style={{ color: 'var(--color-warning)', fontSize: '16px' }} />
                    ) : (
                      <ExclamationCircleOutlined style={{ color: 'var(--color-primary)', fontSize: '16px' }} />
                    )}
                    <div>
                      <div style={{ fontWeight: 500 }}>{result.checkName}</div>
                      <div style={{ fontSize: '12px', color: 'var(--color-text-tertiary)', whiteSpace: 'pre-wrap' }}>
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
