import React, { useState } from 'react';
import { Button } from 'antd';
import { FolderOpenOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../shared/context/ProfileContext';
import { systemService } from '../../../shared/services/systemService';
import { workflowService } from '../services/workflowService';
import { handleError } from '../../../shared/utils/errorHandler';

/**
 * Button that triggers the folder import workflow
 * Adds import to queue, workflow is managed in the table view
 */
export const FolderImportButton: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [loading, setLoading] = useState(false);

  const handleClick = async () => {
    if (!selectedProfileId) {
      console.error('[FolderImportButton] No profile selected');
      return;
    }

    try {
      setLoading(true);

      // Open folder dialog
      const result = await systemService.openFolderDialog({
        title: t('mods.import.selectFolder'),
        rememberPathKey: 'mod-import-folder',
      });

      if (result.success && result.filePath) {
        console.log('[FolderImportButton] Starting workflow for:', result.filePath);

        // Start the workflow - it will automatically be added to the queue via events
        await workflowService.startModImport(selectedProfileId, result.filePath);
      }
    } catch (error) {
      console.error('[FolderImportButton] Failed to start import:', error);
      handleError(error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Button
      type="primary"
      icon={<FolderOpenOutlined />}
      onClick={handleClick}
      loading={loading}
    >
      {t('mods.import.fromFolder')}
    </Button>
  );
};
