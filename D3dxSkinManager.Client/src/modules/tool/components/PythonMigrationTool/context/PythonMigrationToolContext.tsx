import React, { createContext, useContext, useState, ReactNode } from 'react';
import type { FormInstance } from 'antd';
import {
  MigrationAnalysis,
  MigrationResult,
  MigrationOptions,
  MigrationProgress,
} from '../services/migrationService';
import { Module, MigrationEventType } from '../../../../../shared/services/eventBus';
import { useEventSubscription } from '../../../../../shared/hooks/useEventSubscription';

/**
 * Migration wizard steps
 */
export enum MigrationStep {
  Detection = 0,
  Options = 1,
  Progress = 2,
  Complete = 3,
}

/**
 * Migration wizard context state
 */
interface PythonMigrationToolContextState {
  // Current step
  currentStep: MigrationStep;
  setCurrentStep: (step: MigrationStep) => void;

  // Step 1: Detection
  pythonPath: string;
  setPythonPath: (path: string) => void;
  analysis: MigrationAnalysis | undefined;
  setAnalysis: (analysis: MigrationAnalysis | undefined) => void;
  loading: boolean;
  setLoading: (loading: boolean) => void;

  // Step 2: Options
  form: FormInstance | undefined;
  setForm: (form: FormInstance) => void;

  // Step 3: Progress
  migrating: boolean;
  setMigrating: (migrating: boolean) => void;
  migrationProgress: number;
  setMigrationProgress: (progress: number | ((prev: number) => number)) => void;
  currentMigrationProgress: MigrationProgress | undefined;
  setCurrentMigrationProgress: (progress: MigrationProgress | undefined) => void;

  // Step 4: Complete
  result: MigrationResult | undefined;
  setResult: (result: MigrationResult | undefined) => void;

  // Navigation
  goToNextStep: () => void;
  goToPreviousStep: () => void;
  resetWizard: () => void;
}

const PythonMigrationToolContext = createContext<PythonMigrationToolContextState | undefined>(
  undefined
);

/**
 * Hook to use migration wizard context
 * Must be used within PythonMigrationToolProvider
 */
export const usePythonMigrationTool = (): PythonMigrationToolContextState => {
  const context = useContext(PythonMigrationToolContext);
  if (!context) {
    throw new Error('usePythonMigrationTool must be used within PythonMigrationToolProvider');
  }
  return context;
};

interface PythonMigrationToolProviderProps {
  children: ReactNode;
}

/**
 * Migration wizard context provider
 * Manages state for the entire migration wizard flow
 */
export const PythonMigrationToolProvider: React.FC<PythonMigrationToolProviderProps> = ({
  children,
}) => {
  // Step management
  const [currentStep, setCurrentStep] = useState<MigrationStep>(MigrationStep.Detection);

  // Step 1: Detection
  const [pythonPath, setPythonPath] = useState<string>('');
  const [analysis, setAnalysis] = useState<MigrationAnalysis>();
  const [loading, setLoading] = useState<boolean>(false);

  // Step 2: Options
  // Form instance is created in OptionsStep to avoid unconnected form warning
  const [form, setForm] = useState<FormInstance>();

  // Step 3: Progress
  const [migrating, setMigrating] = useState<boolean>(false);
  const [migrationProgress, setMigrationProgress] = useState<number>(0);
  const [currentMigrationProgress, setCurrentMigrationProgress] = useState<MigrationProgress>();

  // Step 4: Complete
  const [result, setResult] = useState<MigrationResult>();

  /**
   * Subscribe to migration progress and completed events
   */
  useEventSubscription(
    Module.MIGRATION,
    MigrationEventType.PROGRESS,
    (payload) => {
      if (payload) {
        setCurrentMigrationProgress(payload);
        setMigrationProgress(payload.percentComplete);
      }
    }
  );

  useEventSubscription(
    Module.MIGRATION,
    MigrationEventType.COMPLETED,
    (payload) => {
      // Migration completed - stop migrating state
      setMigrating(false);
      setMigrationProgress(100);

      // Set result from event payload
      if (payload) {
        setResult(payload);
        setCurrentStep(MigrationStep.Complete);
      }
    }
  );

  /**
   * Navigate to next step
   */
  const goToNextStep = () => {
    if (currentStep < MigrationStep.Complete) {
      setCurrentStep((prev) => prev + 1);
    }
  };

  /**
   * Navigate to previous step
   */
  const goToPreviousStep = () => {
    if (currentStep > MigrationStep.Detection) {
      setCurrentStep((prev) => prev - 1);
    }
  };

  /**
   * Reset wizard to initial state
   */
  const resetWizard = () => {
    setCurrentStep(MigrationStep.Detection);
    setPythonPath('');
    setAnalysis(undefined);
    setLoading(false);
    setMigrating(false);
    setMigrationProgress(0);
    setCurrentMigrationProgress(undefined);
    setResult(undefined);
    form?.resetFields();
  };

  const value: PythonMigrationToolContextState = {
    // Step management
    currentStep,
    setCurrentStep,

    // Detection
    pythonPath,
    setPythonPath,
    analysis,
    setAnalysis,
    loading,
    setLoading,

    // Options
    form,
    setForm,

    // Progress
    migrating,
    setMigrating,
    migrationProgress,
    setMigrationProgress,
    currentMigrationProgress,
    setCurrentMigrationProgress,

    // Complete
    result,
    setResult,

    // Navigation
    goToNextStep,
    goToPreviousStep,
    resetWizard,
  };

  return (
    <PythonMigrationToolContext.Provider value={value}>
      {children}
    </PythonMigrationToolContext.Provider>
  );
};
