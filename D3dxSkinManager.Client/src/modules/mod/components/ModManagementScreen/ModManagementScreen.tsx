import React from 'react';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import { TaskQueueView } from './TaskQueueView';
import { useTranslation } from 'react-i18next';
import './ModManagementScreen.css';

export type ModManagementMode = 'import';

/**
 * Mod Management Screen - Task Queue Manager
 *
 * Redesigned as a download manager / task queue view:
 * - List view showing all pending import tasks
 * - Inline editing of mod metadata before import
 * - Real-time progress tracking for each task
 * - Status bar showing overall queue progress
 * - Works with current backend (synchronous imports with MOD_IMPORTED event)
 * - Ready for future backend progress reporting
 *
 * Features:
 * - Add tasks via drag/drop or file picker
 * - Edit metadata for each task before import
 * - Batch select and edit multiple tasks
 * - Start/pause/remove tasks
 * - Real-time status updates via MOD_IMPORTED event
 * - Progress tracking (estimated for now, real when backend supports it)
 */
const ModManagementFormContent: React.FC = () => {
  return (
    <div className="mod-management-screen-container">
      <TaskQueueView />
    </div>
  );
};

/**
 * Slide-in screen wrapper for ModManagementScreen
 */
export const ModManagementScreen: React.FC = () => {
  const visible = useModsStore(s => s.modManagementScreenVisible);
  const { closeModManagementScreen } = useMods();
  const { t } = useTranslation();

  useSlideInScreen({
    visible,
    title: t('modManagement.title.importQueue'),
    content: <ModManagementFormContent />,
    width: '85%',
    onClose: closeModManagementScreen,
  });

  return null;
};
