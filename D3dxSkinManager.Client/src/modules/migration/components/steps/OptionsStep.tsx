import React, { useEffect } from 'react';
import { Space, Alert, Form, Input, Checkbox, Radio, Divider } from 'antd';
import { useMigrationWizard } from '../../context/MigrationWizardContext';
import { ArchiveHandling, PostMigrationAction } from '../../services/migrationService';

/**
 * Step 2: Options
 * Configure migration options
 */
export const OptionsStep: React.FC = () => {
  const { setForm, analysis } = useMigrationWizard();
  const [localForm] = Form.useForm();

  // Register form instance with context
  useEffect(() => {
    setForm(localForm);
  }, [localForm, setForm]);

  return (
    <Space orientation="vertical" style={{ width: '100%' }} size="large">
      <Alert
        title="Configure Migration"
        description="Select what to migrate and how to handle the original files."
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
          label="Environment"
          name="environmentName"
          tooltip="Which environment to migrate from (if Python installation has multiple)"
        >
          <Input placeholder={analysis?.activeEnvironment} size="large" />
        </Form.Item>

        <Form.Item label="What to Migrate">
          <Space orientation="vertical">
            <Form.Item name="migrateMetadata" valuePropName="checked" noStyle>
              <Checkbox>Mod Metadata (names, authors, descriptions)</Checkbox>
            </Form.Item>
            <Form.Item name="migrateArchives" valuePropName="checked" noStyle>
              <Checkbox>Mod Archives (.7z, .zip files)</Checkbox>
            </Form.Item>
            <Form.Item name="migratePreviews" valuePropName="checked" noStyle>
              <Checkbox>Preview Images</Checkbox>
            </Form.Item>
            <Form.Item name="migrateConfiguration" valuePropName="checked" noStyle>
              <Checkbox>Configuration Settings</Checkbox>
            </Form.Item>
            <Form.Item name="migrateClassifications" valuePropName="checked" noStyle>
              <Checkbox>Classification Rules</Checkbox>
            </Form.Item>
          </Space>
        </Form.Item>

        <Form.Item
          label="Archive Handling"
          name="archiveMode"
          tooltip="How to transfer archive files"
        >
          <Radio.Group>
            <Space orientation="vertical">
              <Radio value={ArchiveHandling.Copy}>
                Copy (safe, keeps original intact)
              </Radio>
              <Radio value={ArchiveHandling.Move}>
                Move (frees space, removes original)
              </Radio>
              <Radio value={ArchiveHandling.Link} disabled>
                Symbolic Link (not implemented)
              </Radio>
            </Space>
          </Radio.Group>
        </Form.Item>

        <Form.Item
          label="After Migration"
          name="postAction"
          tooltip="What to do with the Python installation after migration"
        >
          <Radio.Group>
            <Space orientation="vertical">
              <Radio value={PostMigrationAction.Keep}>Keep (recommended)</Radio>
              <Radio value={PostMigrationAction.BackupAndRemove}>
                Backup & Remove
              </Radio>
              <Radio value={PostMigrationAction.Remove}>
                Remove (not recommended)
              </Radio>
            </Space>
          </Radio.Group>
        </Form.Item>

        <Divider />

        <Form.Item
          name="createProfile"
          valuePropName="checked"
          tooltip="Create a new profile for this migrated configuration"
        >
          <Checkbox>Create new profile from this migration</Checkbox>
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
                  label="Profile Name"
                  name="profileName"
                  rules={[{ required: true, message: 'Please enter profile name' }]}
                >
                  <Input
                    placeholder={analysis?.activeEnvironment || 'Profile Name'}
                    size="large"
                  />
                </Form.Item>
                <Form.Item
                  label="Work Directory"
                  name="workDirectory"
                  tooltip="Directory where 3DMigoto d3dx.dll is loaded"
                >
                  <Input
                    placeholder="Leave empty to use Python installation directory"
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
