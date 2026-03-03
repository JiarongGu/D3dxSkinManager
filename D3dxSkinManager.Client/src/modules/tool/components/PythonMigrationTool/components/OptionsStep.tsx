import React, { useEffect } from 'react';
import { Form, Input, Checkbox, Radio } from 'antd';
import { useTranslation } from 'react-i18next';

import { CompactSpace, CompactAlert, CompactDivider } from '../../../../../shared/components/compact';
import { usePythonMigrationTool } from '../context/PythonMigrationToolContext';
import { ArchiveHandling } from '../services/migrationService';
import './MigrationSteps.css';

/**
 * Step 2: Options
 * Configure migration options
 */
export const OptionsStep: React.FC = () => {
  const { t } = useTranslation();
  const { setForm, analysis } = usePythonMigrationTool();
  const [localForm] = Form.useForm();

  // Register form instance with context
  useEffect(() => {
    setForm(localForm);
  }, [localForm, setForm]);

  return (
    <CompactSpace vertical className="migration-step-container">
      <CompactAlert
        title={t('migration.options.title')}
        description={t('migration.options.description')}
        type="info"
        showIcon
        extraCompact
      />

      <Form
        form={localForm}
        layout="vertical"
        initialValues={{
          migrateArchives: true,
          migrateMetadata: true,
          migratePreviews: true,
          migrateConfiguration: true,
          migrateCategories: true,
          archiveMode: ArchiveHandling.Copy,
          environmentName: analysis?.activeEnvironment,
        }}
      >
        <Form.Item
          label={t('migration.options.environment')}
          name="environmentName"
          tooltip={t('migration.options.environmentTooltip')}
          style={{ marginBottom: 16 }}
        >
          <Input placeholder={analysis?.activeEnvironment} />
        </Form.Item>

        <Form.Item label={t('migration.options.whatToMigrate')} style={{ marginBottom: 16 }}>
          <div className="compact-checkbox-group">
            <Form.Item name="migrateMetadata" valuePropName="checked" noStyle>
              <Checkbox>{t('migration.options.modMetadata')}</Checkbox>
            </Form.Item>
            <Form.Item name="migrateArchives" valuePropName="checked" noStyle>
              <Checkbox>{t('migration.options.modArchives')}</Checkbox>
            </Form.Item>
            <Form.Item name="migratePreviews" valuePropName="checked" noStyle>
              <Checkbox>{t('migration.options.previewImages')}</Checkbox>
            </Form.Item>
            <Form.Item name="migrateConfiguration" valuePropName="checked" noStyle>
              <Checkbox>{t('migration.options.configSettings')}</Checkbox>
            </Form.Item>
            <Form.Item name="migrateCategories" valuePropName="checked" noStyle>
              <Checkbox>{t('migration.options.CategoryRules')}</Checkbox>
            </Form.Item>
          </div>
        </Form.Item>

        <Form.Item
          label={t('migration.options.archiveHandling')}
          name="archiveMode"
          tooltip={t('migration.options.archiveHandlingTooltip')}
          style={{ marginBottom: 16 }}
        >
          <Radio.Group>
            <Radio value={ArchiveHandling.Copy}>
              {t('migration.options.archiveCopy')}
            </Radio>
            <Radio value={ArchiveHandling.Move}>
              {t('migration.options.archiveMove')}
            </Radio>
          </Radio.Group>
        </Form.Item>

        <CompactDivider extraCompact style={{ margin: '12px 0' }} />

        <Form.Item
          name="createProfile"
          valuePropName="checked"
          tooltip={t('migration.options.createProfileTooltip')}
          style={{ marginBottom: 12 }}
        >
          <Checkbox>{t('migration.options.createProfile')}</Checkbox>
        </Form.Item>

        <Form.Item
          noStyle
          shouldUpdate={(prevValues, currentValues) =>
            prevValues.createProfile !== currentValues.createProfile
          }
        >
          {({ getFieldValue }) =>
            getFieldValue('createProfile') ? (
              <>
                <Form.Item
                  label={t('migration.options.profileName')}
                  name="profileName"
                  rules={[{ required: true, message: t('migration.options.profileNameRequired') }]}
                >
                  <Input
                    placeholder={analysis?.activeEnvironment || t('migration.options.profileNamePlaceholder')}
                  />
                </Form.Item>
                <Form.Item
                  label={t('migration.options.workDirectory')}
                  name="workDirectory"
                  tooltip={t('migration.options.workDirectoryTooltip')}
                >
                  <Input
                    placeholder={t('migration.options.workDirectoryPlaceholder')}
                  />
                </Form.Item>
              </>
            ) : null
          }
        </Form.Item>
      </Form>
    </CompactSpace>
  );
};
