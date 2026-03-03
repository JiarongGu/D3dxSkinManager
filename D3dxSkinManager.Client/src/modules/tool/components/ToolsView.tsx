import React, { useCallback } from 'react';
import { Row, Col, Space } from 'antd';
import {
  CheckCircleOutlined,
  ImportOutlined,
  DeleteOutlined,
  TagsOutlined,
  ToolOutlined,
} from '@ant-design/icons';
import { StartupValidationTool } from './StartupValidationTool';
import { CacheManagementTool } from './CacheManagementTool';
import { TagManagementTool } from './TagManagementTool/TagManagementTool';
import { PythonMigrationTool } from './PythonMigrationTool/PythonMigrationTool';
import { useProfile } from '../../../shared/context/ProfileContext';
import { loadMods } from '../../mod/operations/modOperations';
import { useSlideInScreenContext } from '../../../shared/context/SlideInScreenContext';
import { CompactCard } from '../../../shared/components/compact';
import './ToolsView.css';

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
 * - Python Migration
 * - Cache Management
 * - Tag Management
 * - Utilities
 */
export const ToolsView: React.FC = () => {
  const { selectedProfileId } = useProfile();
  const { openScreen } = useSlideInScreenContext();
  const [showMigrationTool, setShowMigrationTool] = React.useState(false);

  const handleModsChanged = useCallback(() => {
    if (selectedProfileId) {
      loadMods(selectedProfileId);
    }
  }, [selectedProfileId]);

  const tools: ToolCardData[] = [
    {
      key: 'startup-validation',
      title: 'Startup Validation',
      description: 'Validate system startup requirements and configuration',
      icon: <CheckCircleOutlined />,
      content: <StartupValidationTool />,
    },
    {
      key: 'python-migration',
      title: 'Python Migration',
      description: 'Migrate from Python version to React version',
      icon: <ImportOutlined />,
      content: null, // Special case - handled separately
    },
    {
      key: 'cache-management',
      title: 'Cache Management',
      description: 'Manage mod cache files and cleanup unused data',
      icon: <DeleteOutlined />,
      content: <CacheManagementTool />,
    },
    {
      key: 'tag-management',
      title: 'Tag Management',
      description: 'Manage mod tags and categories',
      icon: <TagsOutlined />,
      content: <TagManagementTool />,
    }
  ];

  const handleToolClick = (tool: ToolCardData) => {
    // Special handling for Python Migration - open wizard directly
    if (tool.key === 'python-migration') {
      setShowMigrationTool(true);
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
                <span className="tools-view-title">Tools</span>
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

      {/* Python Migration Tool - Opens in SlideInScreen */}
      <PythonMigrationTool
        visible={showMigrationTool}
        onClose={() => setShowMigrationTool(false)}
        onMigrationComplete={handleModsChanged}
      />
    </>
  );
};
