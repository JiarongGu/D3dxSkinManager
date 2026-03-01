/**
 * Mod Import Workflow Screen
 * Displays a step-by-step workflow for importing mods from folders
 */

import React, { useState, useEffect } from 'react';
import { Modal, Steps, Form, Input, Select, Button, Spin, Result } from 'antd';
import {
  FolderOutlined,
  EditOutlined,
  ImportOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  LoadingOutlined,
} from '@ant-design/icons';
import { useModImportWorkflow } from '../hooks/useModImportWorkflow';
import { useTranslation } from 'react-i18next';
import type {
  ModImportWorkflowContext,
  WorkflowInfo,
} from '../types/workflow.types';
import { WorkflowStatus, ModImportWorkflowSteps } from '../types/workflow.types';

// Legacy metadata type (kept for backward compatibility)
interface ModImportMetadata {
  name: string;
  author?: string;
  description?: string;
  category: string;
  tags: string[];
  grading: string;
}
import './ModImportWorkflowScreen.css';

const { TextArea } = Input;

interface ModImportWorkflowScreenProps {
  visible: boolean;
  folderPath?: string;
  onClose: () => void;
  onSuccess?: (modSha: string) => void;
}

/**
 * Main workflow screen component
 */
export const ModImportWorkflowScreen: React.FC<ModImportWorkflowScreenProps> = ({
  visible,
  folderPath,
  onClose,
  onSuccess,
}) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const { workflow, loading, startImport, updateContext, continueWorkflow, cancelImport, clearWorkflow } =
    useModImportWorkflow();

  /**
   * Parse workflow context
   */
  const getContext = (): ModImportWorkflowContext | null => {
    if (!workflow || !workflow.context) return null;
    try {
      return JSON.parse(workflow.context) as ModImportWorkflowContext;
    } catch (error) {
      console.error('[ModImportWorkflowScreen] Failed to parse workflow context:', error);
      return null;
    }
  };

  const context = getContext();

  /**
   * Start the workflow when modal opens with a folder path
   */
  useEffect(() => {
    if (visible && folderPath && !workflow) {
      void startImport(folderPath);
    }
  }, [visible, folderPath]);

  /**
   * Determine current step based on workflow status and context
   */
  const getCurrentStep = (): number => {
    if (!workflow || !context) return 0;

    switch (context.step) {
      case ModImportWorkflowSteps.CompressFolder:
        return 0;
      case ModImportWorkflowSteps.WaitingForUserConfirmation:
        return 1;
      case ModImportWorkflowSteps.ImportMod:
        return 2;
      case ModImportWorkflowSteps.Completed:
        return 3;
      default:
        return 0;
    }
  };

  /**
   * Get step status based on workflow state
   */
  const getStepStatus = (stepIndex: number): 'wait' | 'process' | 'finish' | 'error' => {
    if (!workflow) return 'wait';

    const currentStep = getCurrentStep();

    if (workflow.status === WorkflowStatus.Failed) {
      if (stepIndex === currentStep) return 'error';
      if (stepIndex < currentStep) return 'finish';
      return 'wait';
    }

    if (stepIndex < currentStep) return 'finish';
    if (stepIndex === currentStep) return 'process';
    return 'wait';
  };

  /**
   * Handle metadata form submission
   */
  const handleMetadataSubmit = async () => {
    try {
      const values = await form.validateFields();

      // Update context with metadata
      await updateContext({
        name: values.name,
        author: values.author || null,
        description: values.description || null,
        category: values.category,
        tags: values.tags || [],
        grading: values.grading || 'G',
      });

      // Continue workflow to next step
      await continueWorkflow();
    } catch (error) {
      console.error('[ModImportWorkflowScreen] Failed to submit metadata:', error);
    }
  };

  /**
   * Handle close/cancel
   */
  const handleClose = async () => {
    if (workflow && workflow.status === WorkflowStatus.Processing) {
      // Cancel the workflow if it's still running
      await cancelImport();
    }
    clearWorkflow();
    form.resetFields();
    onClose();
  };

  /**
   * Handle completion
   */
  useEffect(() => {
    if (workflow && workflow.status === WorkflowStatus.Completed && context?.importedModSha) {
      // Notify parent of success
      if (onSuccess) {
        onSuccess(context.importedModSha);
      }
    }
  }, [workflow?.status, context?.importedModSha]);

  /**
   * Set initial form values when waiting for metadata
   */
  useEffect(() => {
    if (context?.step === ModImportWorkflowSteps.WaitingForUserConfirmation) {
      form.setFieldsValue({
        name: context.folderName || '',
        author: '',
        description: '',
        category: '',
        tags: [],
        grading: 'G',
      });
    }
  }, [context?.step, context?.folderName]);

  const ageRatingOptions = [
    { value: 'G', label: t('mods.edit.ageRating.general') },
    { value: 'P', label: t('mods.edit.ageRating.parentalGuidance') },
    { value: 'R', label: t('mods.edit.ageRating.restricted') },
    { value: 'X', label: t('mods.edit.ageRating.adultsOnly') },
  ];

  return (
    <Modal
      title={t('workflow.modImport.title')}
      open={visible}
      onCancel={handleClose}
      width={700}
      footer={null}
      className="mod-import-workflow-screen"
    >
      <div className="workflow-content">
        {/* Steps indicator */}
        <Steps
          current={getCurrentStep()}
          className="workflow-steps"
          items={[
            {
              title: t('workflow.modImport.steps.compress'),
              icon:
                getStepStatus(0) === 'process' ? (
                  <LoadingOutlined />
                ) : getStepStatus(0) === 'error' ? (
                  <CloseCircleOutlined />
                ) : (
                  <FolderOutlined />
                ),
              status: getStepStatus(0),
            },
            {
              title: t('workflow.modImport.steps.metadata'),
              icon: <EditOutlined />,
              status: getStepStatus(1),
            },
            {
              title: t('workflow.modImport.steps.import'),
              icon:
                getStepStatus(2) === 'process' ? (
                  <LoadingOutlined />
                ) : getStepStatus(2) === 'error' ? (
                  <CloseCircleOutlined />
                ) : (
                  <ImportOutlined />
                ),
              status: getStepStatus(2),
            },
          ]}
        />

        {/* Step content */}
        <div className="workflow-step-content">
          {/* Step 1: Compressing folder */}
          {context?.step === ModImportWorkflowSteps.CompressFolder && (
            <div className="workflow-step workflow-step-processing">
              <Spin size="large" />
              <h3>{t('workflow.modImport.compressing')}</h3>
              <p className="step-description">
                {t('workflow.modImport.compressingDescription', {
                  folder: context.folderName || folderPath,
                  count: context.fileCount || 0,
                })}
              </p>
            </div>
          )}

          {/* Step 2: Metadata input */}
          {context?.step === ModImportWorkflowSteps.WaitingForUserConfirmation && (
            <div className="workflow-step workflow-step-metadata">
              <h3>{t('workflow.modImport.provideMetadata')}</h3>
              <p className="step-description">
                {t('workflow.modImport.metadataDescription')}
              </p>

              <Form form={form} layout="vertical" className="metadata-form">
                <Form.Item
                  name="name"
                  label={t('mods.edit.name')}
                  rules={[{ required: true, message: t('mods.edit.nameRequired') }]}
                >
                  <Input placeholder={t('mods.edit.namePlaceholder')} />
                </Form.Item>

                <Form.Item name="author" label={t('mods.edit.author')}>
                  <Input placeholder={t('mods.edit.authorPlaceholder')} />
                </Form.Item>

                <Form.Item name="description" label={t('mods.edit.description')}>
                  <TextArea rows={3} placeholder={t('mods.edit.descriptionPlaceholder')} />
                </Form.Item>

                <Form.Item
                  name="category"
                  label={t('mods.edit.category')}
                  rules={[{ required: true, message: 'Please select a category' }]}
                >
                  <Input placeholder={t('mods.edit.categoryPlaceholder')} />
                </Form.Item>

                <Form.Item name="grading" label={t('mods.edit.ageRating.label')}>
                  <Select options={ageRatingOptions} />
                </Form.Item>

                <Form.Item name="tags" label={t('mods.edit.tags')}>
                  <Select mode="tags" placeholder={t('mods.edit.tagsPlaceholder')} />
                </Form.Item>

                <Form.Item>
                  <Button
                    type="primary"
                    onClick={handleMetadataSubmit}
                    loading={loading}
                    block
                    size="large"
                  >
                    {t('common.continue')}
                  </Button>
                </Form.Item>
              </Form>
            </div>
          )}

          {/* Step 3: Importing mod */}
          {context?.step === ModImportWorkflowSteps.ImportMod && (
            <div className="workflow-step workflow-step-processing">
              <Spin size="large" />
              <h3>{t('workflow.modImport.importing')}</h3>
              <p className="step-description">{t('workflow.modImport.importingDescription')}</p>
            </div>
          )}

          {/* Completed */}
          {workflow?.status === WorkflowStatus.Completed && (
            <div className="workflow-step workflow-step-completed">
              <Result
                status="success"
                title={t('workflow.modImport.completed')}
                subTitle={t('workflow.modImport.completedDescription')}
                extra={
                  <Button type="primary" onClick={handleClose}>
                    {t('common.close')}
                  </Button>
                }
              />
            </div>
          )}

          {/* Failed */}
          {workflow?.status === WorkflowStatus.Failed && (
            <div className="workflow-step workflow-step-failed">
              <Result
                status="error"
                title={t('workflow.modImport.failed')}
                subTitle={workflow.errorMessage || t('workflow.modImport.failedDescription')}
                extra={
                  <Button onClick={handleClose}>{t('common.close')}</Button>
                }
              />
            </div>
          )}

          {/* Cancelled */}
          {workflow?.status === WorkflowStatus.Cancelled && (
            <div className="workflow-step workflow-step-cancelled">
              <Result
                status="warning"
                title={t('workflow.modImport.cancelled')}
                subTitle={t('workflow.modImport.cancelledDescription')}
                extra={
                  <Button onClick={handleClose}>{t('common.close')}</Button>
                }
              />
            </div>
          )}
        </div>
      </div>
    </Modal>
  );
};
