import React from 'react';
import { ReloadOutlined } from '@ant-design/icons';
import {
  CompactCard,
  CompactSpace,
  CompactParagraph,
  CompactDivider,
  CompactSection,
  CompactButton,
} from '../../../shared/components/compact';

/**
 * UtilitiesTool - Various utility functions for the application
 *
 * Features:
 * - Mod Order Management
 * - Environment Variables Management
 */
export const UtilitiesTool: React.FC = () => {
  return (
    <CompactCard title="Utilities">
      <CompactSpace vertical style={{ width: '100%' }}>
        <CompactSection title="Mod Order Management">
          <CompactParagraph>Modify the loading order of mods</CompactParagraph>
          <CompactButton icon={<ReloadOutlined />}>
            Reset to Default Order
          </CompactButton>
        </CompactSection>

        <CompactDivider />

        <CompactSection title="Environment Variables">
          <CompactParagraph>Manage environment variables for mod loading</CompactParagraph>
          <CompactSpace>
            <CompactButton>View Variables</CompactButton>
            <CompactButton type="primary">Edit Variables</CompactButton>
          </CompactSpace>
        </CompactSection>
      </CompactSpace>
    </CompactCard>
  );
};
