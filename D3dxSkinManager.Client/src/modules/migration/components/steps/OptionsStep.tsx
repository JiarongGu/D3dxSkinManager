import React, { useEffect } from 'react';
import { Space, Alert, Form, Input, Checkbox, Radio, Divider } from 'antd';
import { useMigrationWizard } from '../../context/MigrationWizardContext';
import { ArchiveHandling, PostMigrationAction } from '../../services/migrationService';
import { useTranslation } from 'react-i18next';
import './MigrationSteps.css';

/**
 * Step 2: Options
 * Configure migration options
 */
export const OptionsStep: React.FC = () => {
  const { t } = useTranslation();
  const { setForm, analysis } = useMigrationWizard();
  const [localForm] = Form.useForm();

  // Register form instance with context
  useEffect(() => {
    setForm(localForm);
  }, [localForm, setForm]);

  return (
    <Space orientation="vertical" className="migration-step-container" size="large">
      <Alert
        title={t('migration.options.title')}
        description={t('migration.options.description')}
        type="info"
        showIcon
      />

      <Form
        form={localForm}
        layout="vertical"
        initialValues={{
          migrateArchives: true,
          migrateMetadata: true,
          migratePreviews: true,
          migrateConfiguration: true,
          migrateClassifications: true,
          archiveMode: ArchiveHandling.Copy,
          postAction: PostMigrationAction.Keep,
          environmentName: analysis?.activeEnvironment,
        }}
      >
        <Form.Item
          label={t('migration.options.environment')}
          name="environmentName"
          tooltip={t('migration.options.environmentTooltip')}
        >
          <Input placeholder={analysis?.activeEnvironment} size="large" />
        </Form.Item>

        <Form.Item label={t('migration.options.whatToMigrate')}>
          <Space orientation="vertical">
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
            <Form.Item name="migrateClassifications" valuePropName="checked" noStyle>
              <Checkbox>{t('migration.options.classificationRules')}</Checkbox>
            </Form.Item>
          </Space>
        </Form.Item>

        <Form.Item
          label={t('migration.options.archiveHandling')}
          name="archiveMode"
          tooltip={t('migration.options.archiveHandlingTooltip')}
        >
          <Radio.Group>
            <Space orientation="vertical">
              <Radio value={ArchiveHandling.Copy}>
                {t('migration.options.archiveCopy')}
              </Radio>
              <Radio value={ArchiveHandling.Move}>
                {t('migration.options.archiveMove')}
              </Radio>
              <Radio value={ArchiveHandling.Link} disabled>
                {t('migration.options.archiveLink')}
              </Radio>
            </Space>
          </Radio.Group>
        </Form.Item>

        <Form.Item
          label={t('migration.options.afterMigration')}
          name="postAction"
          tooltip={t('migration.options.afterMigrationTooltip')}
        >
          <Radio.Group>
            <Space orientation="vertical">
              <Radio value={PostMigrationAction.Keep}>{t('migration.options.postActionKeep')}</Radio>
              <Radio value={PostMigrationAction.BackupAndRemove}>
                {t('migration.options.postActionBackupRemove')}
              </Radio>
              <Radio value={PostMigrationAction.Remove}>
                {t('migration.options.postActionRemove')}
              </Radio>
            </Space>
          </Radio.Group>
        </Form.Item>

        <Divider />

        <Form.Item
          name="createProfile"
          valuePropName="checked"
          tooltip={t('migration.options.createProfileTooltip')}
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
                    size="large"
                  />
                </Form.Item>
                <Form.Item
                  label={t('migration.options.workDirectory')}
                  name="workDirectory"
                  tooltip={t('migration.options.workDirectoryTooltip')}
                >
                  <Input
                    placeholder={t('migration.options.workDirectoryPlaceholder')}
                    size="large"
                  />
                </Form.Item>
              </>
            ) : null
          }
        </Form.Item>
      </Form>
    </Space>
  );
};
