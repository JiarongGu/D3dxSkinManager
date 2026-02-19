import React, { useState, useCallback } from 'react';
import { ImportOutlined } from '@ant-design/icons';
import { MigrationWizard } from '../../migration/components/MigrationWizard';
import {
  CompactCard,
  CompactSpace,
  CompactAlert,
  CompactButton,
} from '../../../shared/components/compact';

interface PythonMigrationToolProps {
  onMigrationComplete?: () => void;
}

/**
 * PythonMigrationTool - Migrate from Python version to React version
 *
 * Features:
 * - Import mods, previews, and configuration from Python d3dxSkinManage
 * - Launch migration wizard with step-by-step process
 */
export const PythonMigrationTool: React.FC<PythonMigrationToolProps> = ({ onMigrationComplete }) => {
  const [showMigrationWizard, setShowMigrationWizard] = useState(false);

  const handleClose = useCallback(() => {
    setShowMigrationWizard(false);
  }, []);

  return (
    <>
      <CompactCard
        title={<><ImportOutlined /> Python Migration</>}
      >
        <CompactSpace vertical style={{ width: '100%' }}>
          <CompactAlert
            title="Migrate from Python Version"
            description="Import your mods, previews, and configuration from the Python d3dxSkinManage installation to this React version."
            type="info"
            showIcon
          />

          <CompactButton
            type="primary"
            icon={<ImportOutlined />}
            onClick={() => setShowMigrationWizard(true)}
          >
            Start Migration Wizard
          </CompactButton>
        </CompactSpace>
      </CompactCard>

      {/* Migration Wizard */}
      <MigrationWizard
        visible={showMigrationWizard}
        onClose={handleClose}
        onMigrationComplete={onMigrationComplete}
      />
    </>
  );
};
