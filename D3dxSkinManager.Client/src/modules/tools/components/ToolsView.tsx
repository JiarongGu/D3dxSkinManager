import React from 'react';
import { StartupValidationTool } from './StartupValidationTool';
import { PythonMigrationTool } from './PythonMigrationTool';
import { CacheManagementTool } from './CacheManagementTool';
import { TagManagementTool } from './TagManagementTool';
import { UtilitiesTool } from './UtilitiesTool';

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
    <div style={{ height: '100%', overflow: 'auto', padding: '24px' }}>
      <div style={{ maxWidth: '1200px', margin: '0 auto' }}>
        <StartupValidationTool />
        <PythonMigrationTool onMigrationComplete={onModsChanged} />
        <CacheManagementTool />
        <TagManagementTool />
        <UtilitiesTool />
      </div>
    </div>
  );
};
