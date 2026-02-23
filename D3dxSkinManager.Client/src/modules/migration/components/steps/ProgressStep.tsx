import React from 'react';
import { Space, Alert, Card, Progress, Typography } from 'antd';
import { LoadingOutlined } from '@ant-design/icons';
import { useMigrationWizard } from '../../context/MigrationWizardContext';
import { useTranslation } from 'react-i18next';
import './MigrationSteps.css';

const { Paragraph } = Typography;

/**
 * Step 3: Progress
 * Shows migration progress
 */
export const ProgressStep: React.FC = () => {
  const { t } = useTranslation();
  const { migrating, migrationProgress } = useMigrationWizard();

  return (
    <Space orientation="vertical" className="migration-step-container" size="large">
      <Alert
        title={t('migration.progress.title')}
        description={t('migration.progress.description')}
        type="info"
        showIcon
        icon={<LoadingOutlined />}
      />

      <Card>
        <Progress
          percent={migrationProgress}
          status={migrating ? 'active' : 'success'}
          strokeColor={{
            '0%': '#108ee9',
            '100%': '#87d068',
          }}
        />
        <Paragraph className="progress-step-paragraph">
          {migrating ? t('migration.progress.migrating') : t('migration.progress.complete')}
        </Paragraph>
      </Card>
    </Space>
  );
};
