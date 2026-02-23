import React from 'react';
import { Progress, Typography } from 'antd';
import { LoadingOutlined } from '@ant-design/icons';
import { CompactSpace, CompactAlert, CompactCard } from '../../../../shared/components/compact';
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
    <CompactSpace vertical className="migration-step-container">
      <CompactAlert
        title={t('migration.progress.title')}
        description={t('migration.progress.description')}
        type="info"
        showIcon
        icon={<LoadingOutlined />}
        extraCompact
      />

      <CompactCard extraCompact>
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
      </CompactCard>
    </CompactSpace>
  );
};
