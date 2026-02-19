import React, { createContext, useContext, useState, ReactNode } from 'react';
import type { FormInstance } from 'antd';
import {
  MigrationAnalysis,
  MigrationResult,
  MigrationOptions,
} from '../services/migrationService';

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
interface MigrationWizardContextState {
  // Current step
  currentStep: MigrationStep;
  setCurrentStep: (step: MigrationStep) => void;

  // Step 1: Detection
  pythonPath: string;
  setPythonPath: (path: string) => void;
  analysis: MigrationAnalysis | null;
  setAnalysis: (analysis: MigrationAnalysis | null) => void;
  loading: boolean;
  setLoading: (loading: boolean) => void;

  // Step 2: Options
  form: FormInstance | null;
  setForm: (form: FormInstance) => void;

  // Step 3: Progress
  migrating: boolean;
  setMigrating: (migrating: boolean) => void;
  migrationProgress: number;
  setMigrationProgress: (progress: number | ((prev: number) => number)) => void;

  // Step 4: Complete
  result: MigrationResult | null;
  setResult: (result: MigrationResult | null) => void;

  // Navigation
  goToNextStep: () => void;
  goToPreviousStep: () => void;
  resetWizard: () => void;
}

const MigrationWizardContext = createContext<MigrationWizardContextState | undefined>(
  undefined
);

/**
 * Hook to use migration wizard context
 * Must be used within MigrationWizardProvider
 */
export const useMigrationWizard = (): MigrationWizardContextState => {
  const context = useContext(MigrationWizardContext);
  if (!context) {
    throw new Error('useMigrationWizard must be used within MigrationWizardProvider');
  }
  return context;
};

interface MigrationWizardProviderProps {
  children: ReactNode;
}

/**
 * Migration wizard context provider
 * Manages state for the entire migration wizard flow
 */
export const MigrationWizardProvider: React.FC<MigrationWizardProviderProps> = ({
  children,
}) => {
  // Step management
  const [currentStep, setCurrentStep] = useState<MigrationStep>(MigrationStep.Detection);

  // Step 1: Detection
  const [pythonPath, setPythonPath] = useState<string>('');
  const [analysis, setAnalysis] = useState<MigrationAnalysis | null>(null);
  const [loading, setLoading] = useState<boolean>(false);

  // Step 2: Options
  // Form instance is created in OptionsStep to avoid unconnected form warning
  const [form, setForm] = useState<FormInstance | null>(null);

  // Step 3: Progress
  const [migrating, setMigrating] = useState<boolean>(false);
  const [migrationProgress, setMigrationProgress] = useState<number>(0);

  // Step 4: Complete
  const [result, setResult] = useState<MigrationResult | null>(null);

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
    setAnalysis(null);
    setLoading(false);
    setMigrating(false);
    setMigrationProgress(0);
    setResult(null);
    form?.resetFields();
  };

  const value: MigrationWizardContextState = {
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

    // Complete
    result,
    setResult,

    // Navigation
    goToNextStep,
    goToPreviousStep,
    resetWizard,
  };

  return (
    <MigrationWizardContext.Provider value={value}>
      {children}
    </MigrationWizardContext.Provider>
  );
};
