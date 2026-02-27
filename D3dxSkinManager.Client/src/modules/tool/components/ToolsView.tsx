import React, { useCallback } from 'react';
import { StartupValidationTool } from './StartupValidationTool';
import { PythonMigrationTool } from './PythonMigrationTool';
import { CacheManagementTool } from './CacheManagementTool';
import { TagManagementTool } from './TagManagementTool';
import { UtilitiesTool } from './UtilitiesTool';
import { useProfile } from '../../../shared/context/ProfileContext';
import { loadMods } from '../../mod/operations/modOperations';
import './ToolsView.css';

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

  const handleModsChanged = useCallback(() => {
    if (selectedProfileId) {
      loadMods(selectedProfileId);
    }
  }, [selectedProfileId]);

  return (
    <div className="tools-view-container">
      <div className="tools-view-content">
        <StartupValidationTool />
        <PythonMigrationTool onMigrationComplete={handleModsChanged} />
        <CacheManagementTool />
        <TagManagementTool />
        <UtilitiesTool />
      </div>
    </div>
  );
};
