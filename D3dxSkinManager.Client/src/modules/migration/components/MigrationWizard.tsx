import React from 'react';
import { Steps, Space, message } from 'antd';
import {
  FolderOpenOutlined,
  CheckCircleOutlined,
  LoadingOutlined,
  InfoCircleOutlined,
} from '@ant-design/icons';
import { CompactButton } from '../../../shared/components/compact';
import { useSlideInDialog } from '../../../shared/hooks/useSlideInDialog';
import {
  MigrationWizardProvider,
  useMigrationWizard,
  MigrationStep,
} from '../context/MigrationWizardContext';
import { DetectionStep, OptionsStep, ProgressStep, CompleteStep } from './steps';
import { migrationService, MigrationOptions, ArchiveHandling, PostMigrationAction } from '../services/migrationService';
import { profileService } from '../../profiles/services/profileService';
import { useProfile } from '../../../shared/context/ProfileContext';

interface MigrationWizardProps {
  visible: boolean;
  onClose: () => void;
  onMigrationComplete?: () => void;
}


/**
 * Inner content component with access to context
 */
const MigrationWizardInner: React.FC<{
  visible: boolean;
  onClose: () => void;
  onMigrationComplete?: () => void;
}> = ({ onClose, onMigrationComplete }) => {
  const { state: profileState } = useProfile();
  const {
    currentStep,
    analysis,
    pythonPath,
    form,
    setMigrating,
    setMigrationProgress,
    setResult,
    setCurrentStep,
    goToPreviousStep,
    goToNextStep,
    resetWizard,
  } = useMigrationWizard();

  const handleClose = () => {
    setTimeout(() => {
      resetWizard();
      onClose();
    }, 0);
  };

  const handleNext = () => {
    if (currentStep === MigrationStep.Detection && (!analysis || !analysis.isValid)) {
      message.error('Please select a valid Python installation first');
      return;
    }
    goToNextStep();
  };

  const handleStartMigration = async () => {
    if (!form) {
      message.error('Form not initialized');
      return;
    }

    if (!profileState.selectedProfile?.id) {
      message.error('No profile selected');
      return;
    }

    try {
      const values = await form.validateFields() as any;
      setMigrating(true);
      setCurrentStep(MigrationStep.Progress);

      const options: MigrationOptions = {
        sourcePath: pythonPath,
        environmentName: values.environmentName || analysis!.activeEnvironment,
        migrateArchives: values.migrateArchives !== false,
        migrateMetadata: values.migrateMetadata !== false,
        migratePreviews: values.migratePreviews !== false,
        migrateConfiguration: values.migrateConfiguration !== false,
        migrateClassifications: values.migrateClassifications !== false,
        archiveMode: values.archiveMode || ArchiveHandling.Copy,
        postAction: values.postAction || PostMigrationAction.Keep,
      };

      const intervalId = setInterval(() => {
        setMigrationProgress((prev: number) => Math.min(prev + 5, 95));
      }, 1000);

      const profileId = profileState.selectedProfile?.id || '';
      const migrationResult = await migrationService.startMigration(profileId, options);

      clearInterval(intervalId);
      setMigrationProgress(100);
      setResult(migrationResult);

      if (migrationResult.success && values.createProfile) {
        try {
          const profileName =
            values.profileName || analysis?.activeEnvironment || 'Migrated Profile';
          await profileService.createProfile({
            name: profileName,
            description: `Migrated from Python d3dxSkinManage on ${new Date().toLocaleDateString()}`,
            workDirectory: values.workDirectory || pythonPath,
            gameName: analysis?.activeEnvironment,
            copyFromCurrent: false,
          });
          message.success(`Profile "${profileName}" created successfully!`);
        } catch (error) {
          console.error('Failed to create profile:', error);
          message.warning('Migration succeeded but profile creation failed');
        }
      }

      setCurrentStep(MigrationStep.Complete);

      if (migrationResult.success) {
        message.success('Migration completed successfully!');
        if (onMigrationComplete) {
          onMigrationComplete();
        }
      } else {
        message.error('Migration completed with errors');
      }
    } catch (error) {
      message.error('Migration failed');
      console.error(error);
    } finally {
      setMigrating(false);
    }
  };

  const renderStepContent = () => {
    switch (currentStep) {
      case MigrationStep.Detection:
        return <DetectionStep />;
      case MigrationStep.Options:
        return <OptionsStep />;
      case MigrationStep.Progress:
        return <ProgressStep />;
      case MigrationStep.Complete:
        return <CompleteStep />;
      default:
        return null;
    }
  };

  const renderFooter = () => {
    return (
      <div className="slide-in-screen-footer">
        <Space>
          {currentStep === MigrationStep.Detection && (
            <CompactButton onClick={handleClose}>Cancel</CompactButton>
          )}
          {currentStep === MigrationStep.Options && (
            <CompactButton onClick={goToPreviousStep}>Back</CompactButton>
          )}
          {currentStep === MigrationStep.Options && (
            <CompactButton type="primary" onClick={handleStartMigration}>
              Start Migration
            </CompactButton>
          )}
          {currentStep === MigrationStep.Detection && (
            <CompactButton
              type="primary"
              onClick={handleNext}
              disabled={!analysis || !analysis.isValid}
            >
              Next
            </CompactButton>
          )}
          {currentStep === MigrationStep.Complete && (
            <CompactButton type="primary" onClick={handleClose}>
              Close
            </CompactButton>
          )}
        </Space>
      </div>
    );
  };

  return (
    <div>
      <Steps
        current={currentStep}
        style={{ marginBottom: 24 }}
        items={[
          { title: 'Detection', icon: <FolderOpenOutlined /> },
          { title: 'Options', icon: <InfoCircleOutlined /> },
          { title: 'Migration', icon: <LoadingOutlined /> },
          { title: 'Complete', icon: <CheckCircleOutlined /> },
        ]}
      />
      {renderStepContent()}
      {renderFooter()}
    </div>
  );
};

/**
 * Migration Wizard Component
 * Guides user through migrating from Python version to React version
 */
export const MigrationWizard: React.FC<MigrationWizardProps> = ({
  visible,
  onClose,
  onMigrationComplete,
}) => {
  // Wrap content in provider so it's available in slide-in context
  const content = (
    <MigrationWizardProvider>
      <MigrationWizardInner
        visible={visible}
        onClose={onClose}
        onMigrationComplete={onMigrationComplete}
      />
    </MigrationWizardProvider>
  );

  useSlideInDialog({
    visible,
    title: 'Python to React Migration Wizard',
    content,
    width: '70%',
    onClose,
  });

  return null;
};
