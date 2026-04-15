import React, { useCallback } from 'react';
import { Row, Col, Space } from 'antd';
import {
  CheckCircleOutlined,
  ClearOutlined,
  ImportOutlined,
  TagsOutlined,
  ToolOutlined,
  CameraOutlined,
  SwapOutlined,
  SyncOutlined,
  ExperimentOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { StartupValidationTool } from './StartupValidationTool';
import { TagManagementTool } from './TagManagementTool/TagManagementTool';
import { PythonMigrationTool } from './PythonMigrationTool/PythonMigrationTool';
import { ModPackageTool } from './ModPackageTool/ModPackageTool';
import { FileCleanupTool } from './FileCleanupTool/FileCleanupTool';
import { ModAnalyzerTool } from './ModAnalyzerTool/ModAnalyzerTool';
import { ModIdMigrationTool } from './ModIdMigrationTool/ModIdMigrationTool';
import { useSlideInScreenContext } from '../../../shared/context/SlideInScreenContext';
import { CompactCard } from '../../../shared/components/compact';
import { api } from '../../../shared/services/ipc';
import { useProfile } from '../../../shared/context/ProfileContext';
import './ToolsView.css';
import logger from '../../../shared/utils/logger';

interface ToolCardData {
  key: string;
  title: string;
  description: string;
  icon: React.ReactNode;
  content: React.ReactNode;
}

/**
 * ToolsView - Main tools page with various utility features
 *
 * Features:
 * - Startup Validation
 * - Screen Capture
 * - Python Migration
 * - Tag Management
 */
export const ToolsView: React.FC = () => {
  const { t } = useTranslation();
  const { openScreen } = useSlideInScreenContext();
  const { selectedProfileId } = useProfile();
  const [showMigrationTool, setShowMigrationTool] = React.useState(false);
  const [showModPackageTool, setShowModPackageTool] = React.useState(false);
  const [showFileCleanupTool, setShowFileCleanupTool] = React.useState(false);
  const [showModAnalyzerTool, setShowModAnalyzerTool] = React.useState(false);
  const [showModIdMigrationTool, setShowModIdMigrationTool] = React.useState(false);

  // ModsProvider already handles migration completion events
  // No need to manually refresh here
  const handleModsChanged = useCallback(() => {
    // No-op: ModsProvider handles this automatically
  }, []);

  const tools: ToolCardData[] = [
    {
      key: 'screen-capture',
      title: t('tools.screenCapture.title'),
      description: t('tools.screenCapture.description'),
      icon: <CameraOutlined />,
      content: null, // Special case - opens WinForm control panel
    },
    {
      key: 'mod-package',
      title: t('tools.modPackage.title'),
      description: t('tools.modPackage.description'),
      icon: <SwapOutlined />,
      content: null, // Special case - handled separately
    },
    {
      key: 'file-cleanup',
      title: t('tools.fileCleanup.title'),
      description: t('tools.fileCleanup.description'),
      icon: <ClearOutlined />,
      content: null, // Special case - handled separately
    },
    {
      key: 'python-migration',
      title: t('tools.pythonMigration.title'),
      description: t('tools.pythonMigration.description'),
      icon: <ImportOutlined />,
      content: null, // Special case - handled separately
    },
    {
      key: 'tag-management',
      title: t('tools.tagManagement.title'),
      description: t('tools.tagManagement.description'),
      icon: <TagsOutlined />,
      content: <TagManagementTool />,
    },
    {
      key: 'startup-validation',
      title: t('tools.startupValidation.title'),
      description: t('tools.startupValidation.description'),
      icon: <CheckCircleOutlined />,
      content: <StartupValidationTool />,
    },
    {
      key: 'mod-analyzer',
      title: t('tools.modAnalyzer.title'),
      description: t('tools.modAnalyzer.description'),
      icon: <ExperimentOutlined />,
      content: null, // Special case - handled separately
    },
    {
      key: 'mod-id-migration',
      title: t('tools.modIdMigration.title'),
      description: t('tools.modIdMigration.cardDescription'),
      icon: <SyncOutlined />,
      content: null, // Special case - handled separately
    },
  ];

  const handleToolClick = (tool: ToolCardData) => {
    // Special handling for Mod ID Migration - open tool directly
    if (tool.key === 'mod-id-migration') {
      setShowModIdMigrationTool(true);
      return;
    }

    // Special handling for Mod Analyzer - open tool directly
    if (tool.key === 'mod-analyzer') {
      setShowModAnalyzerTool(true);
      return;
    }

    // Special handling for Mod Package - open wizard directly
    if (tool.key === 'mod-package') {
      setShowModPackageTool(true);
      return;
    }

    // Special handling for File Cleanup - open tool directly
    if (tool.key === 'file-cleanup') {
      setShowFileCleanupTool(true);
      return;
    }

    // Special handling for Python Migration - open wizard directly
    if (tool.key === 'python-migration') {
      setShowMigrationTool(true);
      return;
    }

    // Special handling for Screen Capture - toggle WinForm control panel
    if (tool.key === 'screen-capture') {
      if (!selectedProfileId) {
        logger.error('[ToolsView] No profile selected');
        return;
      }
      logger.info('[ToolsView] Calling api.tool.toggleControlPanel()...');
      api.tool.toggleControlPanel(selectedProfileId)
        .then(() => {
          logger.info('[ToolsView] Control panel toggled successfully');
        })
        .catch((error) => {
          logger.error('[ToolsView] Failed to toggle capture control panel:', error);
        });
      return;
    }

    // For other tools, open in SlideInScreen
    openScreen({
      title: tool.title,
      content: tool.content,
      width: '80%',
    });
  };

  return (
    <>
      <div className="tools-view-container">
        <div className="tools-view-content">
          <div className="tools-view-box">
            <div className="tools-view-header">
              <Space>
                <ToolOutlined style={{ fontSize: '20px' }} />
                <span className="tools-view-title">{t('tools.title')}</span>
              </Space>
            </div>
            <Row gutter={[16, 16]}>
              {tools.map((tool) => (
                <Col xs={24} sm={12} md={12} lg={8} xl={6} key={tool.key}>
                  <CompactCard
                    hoverable
                    className="tool-card-clickable"
                    onClick={() => handleToolClick(tool)}
                  >
                    <div className="tool-card-content">
                      <div className="tool-card-header">
                        <span className="tool-card-icon">{tool.icon}</span>
                        <span className="tool-card-title">{tool.title}</span>
                      </div>
                      <div className="tool-card-description">{tool.description}</div>
                    </div>
                  </CompactCard>
                </Col>
              ))}
            </Row>
          </div>
        </div>
      </div>

      {/* Mod Analyzer Tool - Opens in SlideInScreen */}
      <ModAnalyzerTool
        visible={showModAnalyzerTool}
        onClose={() => setShowModAnalyzerTool(false)}
      />

      {/* Mod Package Tool - Opens in SlideInScreen */}
      <ModPackageTool
        visible={showModPackageTool}
        onClose={() => setShowModPackageTool(false)}
      />

      {/* File Cleanup Tool - Opens in SlideInScreen */}
      <FileCleanupTool
        visible={showFileCleanupTool}
        onClose={() => setShowFileCleanupTool(false)}
      />

      {/* Mod ID Migration Tool - Opens in SlideInScreen */}
      <ModIdMigrationTool
        visible={showModIdMigrationTool}
        onClose={() => setShowModIdMigrationTool(false)}
        onMigrationComplete={handleModsChanged}
      />

      {/* Python Migration Tool - Opens in SlideInScreen */}
      <PythonMigrationTool
        visible={showMigrationTool}
        onClose={() => setShowMigrationTool(false)}
        onMigrationComplete={handleModsChanged}
      />
    </>
  );
};
