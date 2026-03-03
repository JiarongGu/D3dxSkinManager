import { notification } from "../../../../shared/utils/notification";
import React from "react";
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
import { profileService } from "../../../profile/services/profileService";
import { useProfile } from "../../../../shared/context/ProfileContext";
import "./PythonMigrationTool.css";

interface PythonMigrationToolProps {
  visible: boolean;
  onClose: () => void;
  onMigrationComplete?: () => void;
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
  const { state: profileState } = useProfile();
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
      notification.error("Please select a valid Python installation first");
      return;
    }
    goToNextStep();
  };

  const handleStartMigration = async () => {
    if (!form) {
      notification.error("Form not initialized");
      return;
    }

    if (!profileState.selectedProfile?.id) {
      notification.error("No profile selected");
      return;
    }

    try {
      const values = (await form.validateFields()) as any;
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

      const profileId = profileState.selectedProfile?.id || "";

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
                "Migrated Profile";
              await profileService.createProfile({
                name: profileName,
                description: `Migrated from Python d3dxSkinManage on ${new Date().toLocaleDateString()}`,
                gameName: analysis?.activeEnvironment,
              });
              notification.success(
                `Profile "${profileName}" created successfully!`,
              );
            } catch (error) {
              notification.warning(
                "Migration succeeded but profile creation failed",
              );
            }
          }

          if (migrationResult.success) {
            notification.success("Migration completed successfully!");
            if (onMigrationComplete) {
              onMigrationComplete();
            }
          } else {
            notification.error("Migration completed with errors");
          }
        })
        .catch((error) => {
          // API timeout or error - this is expected for long migrations
          // The COMPLETED event will handle setting migrating=false
          console.log(
            "Migration API call timed out or errored (this is normal for long migrations):",
            error,
          );
          // Don't show error notification - rely on events
        });

      // Don't wait for API response - continue and let events drive the UI
    } catch (error) {
      notification.error("Migration failed to start");
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
          { title: "Detection", icon: <FolderOpenOutlined /> },
          { title: "Options", icon: <InfoCircleOutlined /> },
          { title: "Migration", icon: <SyncOutlined /> },
          { title: "Complete", icon: <CheckCircleOutlined /> },
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
    title: "Python D3dxSkinManage Migration",
    content,
    width: "85%",
    onClose,
  });

  return null;
};
