import { notification } from "../../../../shared/utils/notification";
import React from "react";
import { useTranslation } from "react-i18next";
import { Steps } from "antd";
import {
  FolderOpenOutlined,
  CheckCircleOutlined,
  SyncOutlined,
  InfoCircleOutlined,
} from "@ant-design/icons";
import {
  CompactButton,
  CompactSpace,
} from "../../../../shared/components/compact";
import { useSlideInScreen } from "../../../../shared/hooks/useSlideInScreen";
import {
  PythonMigrationToolProvider,
  usePythonMigrationTool,
  MigrationStep,
} from "./context/PythonMigrationToolContext";
import {
  DetectionStep,
  OptionsStep,
  ProgressStep,
  CompleteStep,
} from "./components";
import {
  migrationService,
  MigrationOptions,
  ArchiveHandling,
} from "./services/migrationService";
import { profileService } from "../../../../shared/services/ipc";
import { useProfile } from "../../../../shared/context/ProfileContext";
import logger from "../../../../shared/utils/logger";
import "./PythonMigrationTool.css";

interface PythonMigrationToolProps {
  visible: boolean;
  onClose: () => void;
  onMigrationComplete?: () => void;
}

/**
 * Form values from migration options step
 */
interface MigrationFormValues {
  environmentName?: string;
  migrateArchives?: boolean;
  migrateMetadata?: boolean;
  migratePreviews?: boolean;
  migrateConfiguration?: boolean;
  migrateCategories?: boolean;
  archiveMode?: ArchiveHandling;
  createProfile?: boolean;
  profileName?: string;
}

/**
 * Inner content component with access to context
 * Exported for direct use in SlideInScreens
 */
export const PythonMigrationToolInner: React.FC<{
  visible: boolean;
  onClose: () => void;
  onMigrationComplete?: () => void;
}> = ({ onClose, onMigrationComplete }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const {
    currentStep,
    analysis,
    pythonPath,
    form,
    setMigrating,
    setMigrationProgress,
    setCurrentMigrationProgress,
    setResult,
    setCurrentStep,
    goToPreviousStep,
    goToNextStep,
    resetWizard,
  } = usePythonMigrationTool();

  const handleClose = () => {
    setTimeout(() => {
      resetWizard();
      onClose();
    }, 0);
  };

  const handleNext = () => {
    if (
      currentStep === MigrationStep.Detection &&
      (!analysis || !analysis.isValid)
    ) {
      notification.error(t('migration.error.selectPython'));
      return;
    }
    goToNextStep();
  };

  const handleStartMigration = async () => {
    if (!form) {
      notification.error(t('migration.error.formNotInitialized'));
      return;
    }

    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }

    try {
      const values = await form.validateFields() as MigrationFormValues;
      setMigrating(true);
      setCurrentStep(MigrationStep.Progress);

      const options: MigrationOptions = {
        sourcePath: pythonPath,
        environmentName: values.environmentName || analysis!.activeEnvironment,
        migrateArchives: values.migrateArchives !== false,
        migrateMetadata: values.migrateMetadata !== false,
        migratePreviews: values.migratePreviews !== false,
        migrateConfiguration: values.migrateConfiguration !== false,
        migrateCategories: values.migrateCategories !== false,
        archiveMode: values.archiveMode || ArchiveHandling.Copy,
      };

      // Reset progress before starting migration
      setMigrationProgress(0);
      setCurrentMigrationProgress(undefined);

      const profileId = selectedProfileId || "";

      // Start migration - don't wait for response as it will timeout for long migrations
      // Rely on PROGRESS and COMPLETED events instead
      migrationService
        .startMigration(profileId, options)
        .then(async (migrationResult) => {
          // API response received successfully (migration was quick or didn't timeout)
          setResult(migrationResult);
          setCurrentStep(MigrationStep.Complete);

          // Handle profile creation if requested
          if (migrationResult.success && values.createProfile) {
            try {
              const profileName =
                values.profileName ||
                analysis?.activeEnvironment ||
                t('migration.defaultProfileName');
              await profileService.createProfile({
                name: profileName,
                description: t('migration.profileDescription', { date: new Date().toLocaleDateString() }),
                gameName: analysis?.activeEnvironment,
              });
              notification.success(
                t('migration.profileCreated', { profileName }),
              );
            } catch (error) {
              notification.warning(
                t('migration.profileCreationFailed'),
              );
            }
          }

          if (migrationResult.success) {
            notification.success(t('migration.complete.success'));
            if (onMigrationComplete) {
              onMigrationComplete();
            }
          } else {
            notification.error(t('migration.complete.withErrors'));
          }
        })
        .catch((error: unknown) => {
          // API timeout or error - this is expected for long migrations
          // The COMPLETED event will handle setting migrating=false
          logger.debug(
            "Migration API call timed out or errored (this is normal for long migrations):",
            error,
          );
          // Don't show error notification - rely on events
        });

      // Don't wait for API response - continue and let events drive the UI
    } catch (error) {
      notification.error(t('migration.error.failedToStart'));
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
        <CompactSpace>
          {currentStep === MigrationStep.Detection && (
            <CompactButton.Danger onClick={handleClose}>{t('common.cancel')}</CompactButton.Danger>
          )}
          {currentStep === MigrationStep.Options && (
            <CompactButton onClick={goToPreviousStep}>{t('common.back')}</CompactButton>
          )}
          {currentStep === MigrationStep.Options && (
            <CompactButton type="primary" onClick={handleStartMigration}>
              {t('migration.buttons.startMigration')}
            </CompactButton>
          )}
          {currentStep === MigrationStep.Detection && (
            <CompactButton
              type="primary"
              onClick={handleNext}
              disabled={!analysis || !analysis.isValid}
            >
              {t('common.next')}
            </CompactButton>
          )}
          {currentStep === MigrationStep.Complete && (
            <CompactButton type="primary" onClick={handleClose}>
              {t('common.close')}
            </CompactButton>
          )}
        </CompactSpace>
      </div>
    );
  };

  return (
    <div>
      <Steps
        current={currentStep}
        className="migration-wizard-steps"
        items={[
          { title: t('migration.steps.detection'), icon: <FolderOpenOutlined /> },
          { title: t('migration.steps.options'), icon: <InfoCircleOutlined /> },
          { title: t('migration.steps.migration'), icon: <SyncOutlined /> },
          { title: t('migration.steps.complete'), icon: <CheckCircleOutlined /> },
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
export const PythonMigrationTool: React.FC<PythonMigrationToolProps> = ({
  visible,
  onClose,
  onMigrationComplete,
}) => {
  const { t } = useTranslation();

  // Wrap content in provider so it's available in slide-in context
  const content = (
    <PythonMigrationToolProvider>
      <PythonMigrationToolInner
        visible={visible}
        onClose={onClose}
        onMigrationComplete={onMigrationComplete}
      />
    </PythonMigrationToolProvider>
  );

  useSlideInScreen({
    visible,
    title: t('migration.title'),
    content,
    width: "85%",
    onClose,
  });

  return null;
};
