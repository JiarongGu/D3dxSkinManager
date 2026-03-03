import React from 'react';
import { Row, Col, List } from 'antd';
import { FolderOpenOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

import { CompactButton, CompactAlert, CompactCard, CompactSpace, CompactDivider } from '../../../../../shared/components/compact';
import { usePythonMigrationTool } from '../context/PythonMigrationToolContext';
import { migrationService } from '../services/migrationService';
import { fileDialogService } from '../../../../../shared/services/systemService';
import { useProfile } from '../../../../../shared/context/ProfileContext';
import { notification } from '../../../../../shared/utils/notification';
import './DetectionStep.css';

/**
 * Step 1: Detection
 * Allows user to select Python installation directory and analyzes it
 */
export const DetectionStep: React.FC = () => {
  const { t } = useTranslation();
  const {
    pythonPath,
    setPythonPath,
    analysis,
    setAnalysis,
    loading,
    setLoading,
  } = usePythonMigrationTool();
  const { state: profileState } = useProfile();

  /**
   * Browse for Python installation directory
   */
  const handleBrowse = async () => {
    try {
      const result = await fileDialogService.openFolderDialog({
        title: t('migration.detection.selectPythonDir'),
        defaultPath: 'E:\\Games',
        rememberPathKey: 'migration-python-install',
      });

      if (result.success && result.filePath) {
        setPythonPath(result.filePath);
        await handleAnalyze(result.filePath);
      }
    } catch (error) {
      notification.error(t('migration.detection.browseFailed'));
          }
  };

  /**
   * Analyze selected Python installation
   */
  const handleAnalyze = async (path: string) => {
    if (!profileState.selectedProfile?.id) {
      notification.error(t('migration.detection.noProfileSelected'));
      return;
    }
    const profileId = profileState.selectedProfile.id;
    try {
      setLoading(true);
      const analysisResult = await migrationService.analyzePythonInstallation(profileId, path);
            setAnalysis(analysisResult);

      if (!analysisResult.isValid) {
        notification.error(t('migration.detection.invalidInstallation', { errors: analysisResult.errors.join(', ') }));
      } else {
        notification.success(t('migration.detection.modsFound', { count: analysisResult.totalMods }));
      }
    } catch (error) {
      notification.error(t('migration.detection.analyzeFailed'));
            // Set a failed analysis state so user can see the error
      setAnalysis({
        isValid: false,
        sourcePath: path,
        totalMods: 0,
        totalArchiveSize: 0,
        totalArchiveSizeFormatted: '0 B',
        totalPreviewSize: 0,
        totalPreviewSizeFormatted: '0 B',
        environments: [],
        activeEnvironment: '',
        errors: [String(error)],
        warnings: [],
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <CompactSpace vertical className="detection-step-container">
      <CompactAlert
        title={t('migration.detection.title')}
        description={t('migration.detection.description')}
        type="info"
        showIcon
        extraCompact
      />

      <CompactCard title={t('migration.detection.step1Title')} extraCompact>
        <CompactSpace vertical className="detection-step-inner-container">
          {pythonPath && (
            <CompactAlert
              description={pythonPath}
              type="info"
              extraCompact
            />
          )}
          <CompactButton
            type="primary"
            icon={<FolderOpenOutlined />}
            onClick={handleBrowse}
            block
          >
            {t('migration.detection.browse')}
          </CompactButton>


          {analysis && (
            <CompactCard
              size="small"
              title={t('migration.detection.analysisResult')}
              className="detection-step-analysis-card"
              extraCompact
            >
              {analysis.isValid ? (
                <>
                  <Row gutter={8}>
                    <Col span={12}>
                      <div className="detection-step-stat">
                        <div className="detection-step-stat-label">{t('migration.detection.totalMods')}</div>
                        <div className="detection-step-stat-value">{analysis.totalMods}</div>
                      </div>
                    </Col>
                    <Col span={12}>
                      <div className="detection-step-stat">
                        <div className="detection-step-stat-label">{t('migration.detection.archiveSize')}</div>
                        <div className="detection-step-stat-value">{analysis.totalArchiveSizeFormatted || t('migration.detection.notAvailable')}</div>
                      </div>
                    </Col>
                  </Row>
                  <CompactDivider extraCompact />
                  <Row gutter={8}>
                    <Col span={12}>
                      <div className="detection-step-stat">
                        <div className="detection-step-stat-label">{t('migration.detection.previewSize')}</div>
                        <div className="detection-step-stat-value">{analysis.totalPreviewSizeFormatted || t('migration.detection.notAvailable')}</div>
                      </div>
                    </Col>
                    <Col span={12}>
                      <div className="detection-step-stat">
                        <div className="detection-step-stat-label">{t('migration.detection.environmentsLabel')}</div>
                        <div className="detection-step-stat-value">{analysis.environments.join(', ')}</div>
                      </div>
                    </Col>
                  </Row>
                  {analysis.warnings.length > 0 && (
                    <>
                      <CompactDivider extraCompact />
                      <CompactAlert
                        title={t('migration.detection.warnings')}
                        description={
                          <List
                            size="small"
                            dataSource={analysis.warnings}
                            renderItem={(item) => <List.Item>{item}</List.Item>}
                          />
                        }
                        type="warning"
                        showIcon
                        extraCompact
                      />
                    </>
                  )}
                </>
              ) : (
                <CompactAlert
                  title={t('migration.detection.invalidInstallationTitle')}
                  description={analysis.errors.join('\n')}
                  type="error"
                  showIcon
                  extraCompact
                />
              )}
            </CompactCard>
          )}
        </CompactSpace>
      </CompactCard>
    </CompactSpace>
  );
};
