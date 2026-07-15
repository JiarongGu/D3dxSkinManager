import React from 'react';
import { Progress, Typography, Collapse, Space } from 'antd';
import { LoadingOutlined, CheckCircleOutlined, ExclamationCircleOutlined, DownOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

import { CompactSpace, CompactAlert, CompactCard } from '../../../../../shared/components/compact';
import { usePythonMigrationTool } from '../context/PythonMigrationToolContext';
import type { MigrationError } from '../services/migrationService';
import {
  getTranslatedModName,
  getTranslatedCategory,
  getTranslatedStep,
  getTranslatedMessage
} from '../utils/migrationErrorMapper';
import { formatBytes } from '../../../../../shared/utils/formatBytes';
import './MigrationSteps.css';

const { Paragraph, Text } = Typography;
const { Panel } = Collapse;

/**
 * Helper function to get translated stage name
 * Converts PascalCase to camelCase and looks up translation
 */
const getTranslatedStageName = (stage: string, t: any): string => {
  // Convert PascalCase to camelCase: "MigratingMetadata" -> "migratingMetadata"
  const camelCase = stage.charAt(0).toLowerCase() + stage.slice(1);
  const key = `migration.stage.${camelCase}`;
  const translated = t(key);

  // If translation not found (key returned), fall back to formatted English
  if (translated === key) {
    return stage.replace(/([A-Z])/g, ' $1').trim();
  }

  return translated;
};

/**
 * Group errors by mod name
 */
const groupErrorsByMod = (errors: MigrationError[], t: any) => {
  const grouped = new Map<string, MigrationError[]>();

  errors.forEach(error => {
    const key = getTranslatedModName(error, t);
    if (!grouped.has(key)) {
      grouped.set(key, []);
    }
    grouped.get(key)!.push(error);
  });

  return Array.from(grouped.entries()).map(([modName, modErrors]) => ({
    modName,
    errors: modErrors,
    count: modErrors.length
  }));
};

/**
 * Step 3: Progress
 * Shows migration progress with detailed information
 */
export const ProgressStep: React.FC = () => {
  const { t } = useTranslation();
  const { migrating, migrationProgress, currentMigrationProgress, result } = usePythonMigrationTool();

  // Check if there are errors in the result
  const hasErrors = result && result.detailedErrors && result.detailedErrors.length > 0;
  const groupedErrors = hasErrors ? groupErrorsByMod(result.detailedErrors, t) : [];

  return (
    <CompactSpace vertical className="migration-step-container">
      <CompactAlert
        title={t('migration.progress.title')}
        description={t('migration.progress.description')}
        type="info"
        showIcon
        icon={<LoadingOutlined />}
        extraCompact
      />

      <CompactCard extraCompact>
        <CompactSpace vertical style={{ width: '100%' }}>
          {/* Overall progress bar */}
          <div style={{ width: '100%' }}>
            <div style={{ marginBottom: '8px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <Text strong style={{ fontSize: '14px' }}>Overall Progress</Text>
              {currentMigrationProgress && currentMigrationProgress.totalSteps > 0 && (
                <Text type="secondary" style={{ fontSize: '12px' }}>
                  Step {currentMigrationProgress.currentStep}/{currentMigrationProgress.totalSteps}
                </Text>
              )}
            </div>
            <Progress
              percent={migrationProgress}
              status={migrating ? 'active' : hasErrors ? 'exception' : 'success'}
              strokeColor={{
                '0%': '#108ee9',
                '100%': '#87d068',
              }}
              strokeWidth={12}
            />
          </div>

          {/* Current step progress */}
          {currentMigrationProgress && migrating && currentMigrationProgress.stepName && (
            <div style={{ width: '100%', marginTop: '16px' }}>
              <div style={{ marginBottom: '8px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <Text strong style={{ fontSize: '14px' }}>
                  {currentMigrationProgress.stepName}
                </Text>
                {currentMigrationProgress.totalItems > 0 && (
                  <Text type="secondary" style={{ fontSize: '12px' }}>
                    {currentMigrationProgress.processedItems} / {currentMigrationProgress.totalItems}
                  </Text>
                )}
              </div>
              <Progress
                percent={currentMigrationProgress.stepProgress || 0}
                status="active"
                strokeWidth={8}
                showInfo={false}
              />
            </div>
          )}

          {/* Detailed progress information - inline style without table */}
          {currentMigrationProgress && migrating && (
            <div style={{
              padding: '12px 16px',
              background: 'rgba(255, 255, 255, 0.04)',
              borderRadius: '4px',
              width: '100%',
              boxSizing: 'border-box' // Fix overflow issue
            }}>
              {/* Stage and Task in one row */}
              <Space direction="vertical" size={4} style={{ width: '100%' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
                  <Text type="secondary" style={{ fontSize: '12px' }}>Stage:</Text>
                  <Text strong style={{ fontSize: '14px' }}>
                    {getTranslatedStageName(currentMigrationProgress.stage, t)}
                  </Text>
                  {currentMigrationProgress.totalBytes > 0 && (
                    <>
                      <Text type="secondary" style={{ fontSize: '12px', marginLeft: '8px' }}>•</Text>
                      <Text style={{ fontSize: '12px' }}>
                        {formatBytes(currentMigrationProgress.bytesProcessed)} / {formatBytes(currentMigrationProgress.totalBytes)}
                      </Text>
                    </>
                  )}
                </div>
                <div style={{ marginTop: '4px' }}>
                  <Text type="secondary" style={{ fontSize: '12px' }}>
                    {currentMigrationProgress.currentTask}
                  </Text>
                </div>
              </Space>
            </div>
          )}

          {/* Error display at the end of migration - Collapsible by mod */}
          {!migrating && hasErrors && (
            <div style={{ marginTop: '12px' }}>
              <div style={{
                padding: '12px 16px',
                background: 'rgba(255, 77, 79, 0.1)',
                borderRadius: '4px 4px 0 0',
                borderLeft: '3px solid #ff4d4f',
                display: 'flex',
                alignItems: 'center',
                gap: '8px'
              }}>
                <ExclamationCircleOutlined style={{ color: '#ff4d4f', fontSize: '16px' }} />
                <Text strong style={{ fontSize: '14px', color: '#ff4d4f' }}>
                  {t('migration.progress.errorHeader', { count: result.detailedErrors.length })}
                </Text>
              </div>

              <Collapse
                ghost
                expandIcon={({ isActive }) => <DownOutlined rotate={isActive ? 180 : 0} />}
                style={{
                  background: 'rgba(255, 77, 79, 0.05)',
                  borderRadius: '0 0 4px 4px'
                }}
              >
                {groupedErrors.map((group, index) => (
                  <Panel
                    header={
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <Text strong style={{ fontSize: '14px' }}>
                          {group.modName}
                        </Text>
                        <Text type="secondary" style={{ fontSize: '12px' }}>
                          ({t('migration.error.errorCount', { count: group.count })})
                        </Text>
                      </div>
                    }
                    key={index}
                    style={{
                      borderBottom: index < groupedErrors.length - 1 ? '1px solid rgba(255, 77, 79, 0.1)' : 'none'
                    }}
                  >
                    <Space direction="vertical" size={8} style={{ width: '100%' }}>
                      {group.errors.map((error, errorIndex) => (
                        <div key={errorIndex} style={{
                          padding: '8px 12px',
                          background: 'rgba(255, 77, 79, 0.08)',
                          borderRadius: '4px',
                          borderLeft: '2px solid #ff4d4f'
                        }}>
                          <Text style={{ fontSize: '12px', display: 'block', marginBottom: '4px' }}>
                            {getTranslatedMessage(error, t)}
                          </Text>
                          {(error.stepCode || error.categoryCode) && (
                            <Text type="secondary" style={{ fontSize: '12px' }}>
                              {getTranslatedCategory(error.categoryCode, t)}
                              {error.stepCode && error.categoryCode && ' • '}
                              {getTranslatedStep(error.stepCode, t)}
                            </Text>
                          )}
                        </div>
                      ))}
                    </Space>
                  </Panel>
                ))}
              </Collapse>
            </div>
          )}

          {/* Success message - only show when migration is complete AND no errors */}
          {!migrating && !hasErrors && result && (
            <div style={{
              marginTop: '12px',
              padding: '12px 16px',
              background: 'rgba(82, 196, 26, 0.1)',
              borderRadius: '4px',
              borderLeft: '3px solid #52c41a',
              display: 'flex',
              alignItems: 'center',
              gap: '8px'
            }}>
              <CheckCircleOutlined style={{ color: '#52c41a', fontSize: '16px' }} />
              <Text strong style={{ fontSize: '14px', color: '#52c41a' }}>
                {t('migration.progress.successMessage')}
              </Text>
            </div>
          )}
        </CompactSpace>
      </CompactCard>
    </CompactSpace>
  );
};
