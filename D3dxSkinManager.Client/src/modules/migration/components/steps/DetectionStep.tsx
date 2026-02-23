import { notification } from '../../../../shared/utils/notification';
import React from 'react';
import { Space, Alert, Card, Row, Col, Statistic, Divider, List, Typography } from 'antd';
import { FolderOpenOutlined } from '@ant-design/icons';
import { CompactButton } from '../../../../shared/components/compact';
import { useMigrationWizard } from '../../context/MigrationWizardContext';
import { migrationService } from '../../services/migrationService';
import { fileDialogService } from '../../../../shared/services/systemService';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { useTranslation } from 'react-i18next';
import './DetectionStep.css';

const { Text } = Typography;

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
  } = useMigrationWizard();
  const { state: profileState } = useProfile();

  /**
   * Auto-detect Python installation
   */
  const handleAutoDetect = async () => {
    try {
      setLoading(true);
      const detectedPath = await migrationService.autoDetect();
      if (detectedPath) {
        setPythonPath(detectedPath);
        notification.success(t('migration.detection.pythonDetected'));
        await handleAnalyze(detectedPath);
      } else {
        notification.warning(t('migration.detection.autoDetectFailed'));
      }
    } catch (error) {
      notification.error(t('migration.detection.autoDetectError'));
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  /**
   * Browse for Python installation directory
   */
  const handleBrowse = async () => {
    try {
      const result = await fileDialogService.openFolderDialog({
        title: t('migration.detection.selectPythonDir'),
        defaultPath: 'E:\\Games',
        rememberPathKey: 'migration_python_install',
      });

      if (result.success && result.filePath) {
        setPythonPath(result.filePath);
        await handleAnalyze(result.filePath);
      }
    } catch (error) {
      notification.error(t('migration.detection.browseFailed'));
      console.error(error);
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
      console.log('Migration analysis result:', analysisResult);
      setAnalysis(analysisResult);

      if (!analysisResult.isValid) {
        notification.error(t('migration.detection.invalidInstallation', { errors: analysisResult.errors.join(', ') }));
      } else {
        notification.success(t('migration.detection.modsFound', { count: analysisResult.totalMods }));
      }
    } catch (error) {
      notification.error(t('migration.detection.analyzeFailed'));
      console.error('Analysis error:', error);
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
    <Space orientation="vertical" className="detection-step-container" size="large">
      <Alert
        title={t('migration.detection.title')}
        description={t('migration.detection.description')}
        type="info"
        showIcon
      />

      <Card title={t('migration.detection.step1Title')}>
        <Space orientation="vertical" className="detection-step-inner-container" size="middle">
          <CompactButton
            type="primary"
            icon={<FolderOpenOutlined />}
            onClick={handleAutoDetect}
            loading={loading}
            block
          >
            {t('migration.detection.autoDetect')}
          </CompactButton>

          <CompactButton
            icon={<FolderOpenOutlined />}
            onClick={handleBrowse}
            block
          >
            {t('migration.detection.browse')}
          </CompactButton>

          {pythonPath && (
            <Alert
              title={t('migration.detection.selectedPath')}
              description={pythonPath}
              type="info"
            />
          )}

          {analysis && (
            <Card
              size="small"
              title={t('migration.detection.analysisResult')}
              className="detection-step-analysis-card"
            >
              {analysis.isValid ? (
                <>
                  <Row gutter={16}>
                    <Col span={12}>
                      <Statistic title={t('migration.detection.totalMods')} value={analysis.totalMods} />
                    </Col>
                    <Col span={12}>
                      <Statistic
                        title={t('migration.detection.archiveSize')}
                        value={analysis.totalArchiveSizeFormatted || t('migration.detection.notAvailable')}
                      />
                    </Col>
                  </Row>
                  <Divider className="detection-step-divider" />
                  <Row gutter={16}>
                    <Col span={12}>
                      <Statistic
                        title={t('migration.detection.previewSize')}
                        value={analysis.totalPreviewSizeFormatted || t('migration.detection.notAvailable')}
                      />
                    </Col>
                    <Col span={12}>
                      <Text>{t('migration.detection.environments', { envs: analysis.environments.join(', ') })}</Text>
                    </Col>
                  </Row>
                  {analysis.warnings.length > 0 && (
                    <>
                      <Divider className="detection-step-divider" />
                      <Alert
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
                      />
                    </>
                  )}
                </>
              ) : (
                <Alert
                  title={t('migration.detection.invalidInstallationTitle')}
                  description={analysis.errors.join('\n')}
                  type="error"
                  showIcon
                />
              )}
            </Card>
          )}
        </Space>
      </Card>
    </Space>
  );
};
