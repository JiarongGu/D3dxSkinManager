import React from 'react';
import { Space, Alert, Card, Progress, Typography } from 'antd';
import { LoadingOutlined } from '@ant-design/icons';
import { useMigrationWizard } from '../../context/MigrationWizardContext';

const { Paragraph } = Typography;

/**
 * Step 3: Progress
 * Shows migration progress
 */
export const ProgressStep: React.FC = () => {
  const { migrating, migrationProgress } = useMigrationWizard();

  return (
    <Space orientation="vertical" style={{ width: '100%' }} size="large">
      <Alert
        title="Migration in Progress"
        description="Please wait while your data is being migrated. This may take several minutes depending on the number of mods."
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
        <Paragraph style={{ marginTop: 16, textAlign: 'center' }}>
          {migrating ? 'Migrating data...' : 'Migration complete!'}
        </Paragraph>
      </Card>
    </Space>
  );
};
