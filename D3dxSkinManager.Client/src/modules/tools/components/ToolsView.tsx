import React from 'react';
import { StartupValidationTool } from './StartupValidationTool';
import { PythonMigrationTool } from './PythonMigrationTool';
import { CacheManagementTool } from './CacheManagementTool';
import { TagManagementTool } from './TagManagementTool';
import { UtilitiesTool } from './UtilitiesTool';
import './ToolsView.css';

interface ToolsViewProps {
  onModsChanged?: () => void;
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
export const ToolsView: React.FC<ToolsViewProps> = ({ onModsChanged }) => {
  return (
    <div className="tools-view-container">
      <div className="tools-view-content">
        <StartupValidationTool />
        <PythonMigrationTool onMigrationComplete={onModsChanged} />
        <CacheManagementTool />
        <TagManagementTool />
        <UtilitiesTool />
      </div>
    </div>
  );
};
