import { notification } from '../../../../shared/utils/notification';
import React from 'react';
import { Space, Alert, Card, Row, Col, Statistic, Divider, List, Typography } from 'antd';
import { FolderOpenOutlined } from '@ant-design/icons';
import { CompactButton } from '../../../../shared/components/compact';
import { useMigrationWizard } from '../../context/MigrationWizardContext';
import { migrationService } from '../../services/migrationService';
import { fileDialogService } from '../../../../shared/services/systemService';
import { useProfile } from '../../../../shared/context/ProfileContext';

const { Text } = Typography;

/**
 * Step 1: Detection
 * Allows user to select Python installation directory and analyzes it
 */
export const DetectionStep: React.FC = () => {
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
        notification.success('Python installation detected');
        await handleAnalyze(detectedPath);
      } else {
        notification.warning('Could not auto-detect Python installation. Please browse manually.');
      }
    } catch (error) {
      notification.error('Failed to auto-detect Python installation');
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
        title: 'Select Python d3dxSkinManage Installation Directory',
        defaultPath: 'E:\\Mods',
        rememberPathKey: 'migration_python_install',
      });

      if (result.success && result.filePath) {
        setPythonPath(result.filePath);
        await handleAnalyze(result.filePath);
      }
    } catch (error) {
      notification.error('Failed to browse for directory');
      console.error(error);
    }
  };

  /**
   * Analyze selected Python installation
   */
  const handleAnalyze = async (path: string) => {
    if (!profileState.selectedProfile?.id) {
      notification.error('No profile selected');
      return;
    }
    const profileId = profileState.selectedProfile.id;
    try {
      setLoading(true);
      const analysisResult = await migrationService.analyzePythonInstallation(profileId, path);
      console.log('Migration analysis result:', analysisResult);
      setAnalysis(analysisResult);

      if (!analysisResult.isValid) {
        notification.error(`Invalid Python installation directory: ${analysisResult.errors.join(', ')}`);
      } else {
        notification.success(`Found ${analysisResult.totalMods} mods ready to migrate`);
      }
    } catch (error) {
      notification.error('Failed to analyze Python installation');
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
    <Space orientation="vertical" style={{ width: '100%' }} size="large">
      <Alert
        title="Migrate from Python Version"
        description="This wizard will help you migrate your mods, previews, and configuration from the Python d3dxSkinManage to this React version."
        type="info"
        showIcon
      />

      <Card title="Step 1: Locate Python Installation">
        <Space orientation="vertical" style={{ width: '100%' }} size="middle">
          <CompactButton
            type="primary"
            icon={<FolderOpenOutlined />}
            onClick={handleAutoDetect}
            loading={loading}
            block
          >
            Auto-Detect Python Installation
          </CompactButton>

          <CompactButton
            icon={<FolderOpenOutlined />}
            onClick={handleBrowse}
            block
          >
            Browse for Python Installation Directory
          </CompactButton>

          {pythonPath && (
            <Alert
              title="Selected Path"
              description={pythonPath}
              type="info"
            />
          )}

          {analysis && (
            <Card
              size="small"
              title="Analysis Result"
              style={{
                background: 'var(--color-bg-elevated)',
                borderColor: 'var(--color-border-secondary)',
              }}
            >
              {analysis.isValid ? (
                <>
                  <Row gutter={16}>
                    <Col span={12}>
                      <Statistic title="Total Mods" value={analysis.totalMods} />
                    </Col>
                    <Col span={12}>
                      <Statistic
                        title="Archive Size"
                        value={analysis.totalArchiveSizeFormatted || 'N/A'}
                      />
                    </Col>
                  </Row>
                  <Divider style={{ margin: '12px 0' }} />
                  <Row gutter={16}>
                    <Col span={12}>
                      <Statistic
                        title="Preview Size"
                        value={analysis.totalPreviewSizeFormatted || 'N/A'}
                      />
                    </Col>
                    <Col span={12}>
                      <Text>Environments: {analysis.environments.join(', ')}</Text>
                    </Col>
                  </Row>
                  {analysis.warnings.length > 0 && (
                    <>
                      <Divider style={{ margin: '12px 0' }} />
                      <Alert
                        title="Warnings"
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
                  title="Invalid Installation"
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
