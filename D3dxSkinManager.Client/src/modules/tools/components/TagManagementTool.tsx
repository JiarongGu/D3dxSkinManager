import { notification } from '../../../shared/utils/notification';
import React, { useState } from 'react';
import { Tag, Modal } from 'antd';
import { TagsOutlined, ReloadOutlined } from '@ant-design/icons';
import {
  CompactCard,
  CompactSpace,
  CompactParagraph,
  CompactDivider,
  CompactSection,
  CompactButton,
} from '../../../shared/components/compact';

/**
 * TagManagementTool - Manage mod tags and categories
 *
 * Features:
 * - View common tags
 * - Edit custom tags
 * - Refresh tag list
 */
export const TagManagementTool: React.FC = () => {
  const [showTagEditor, setShowTagEditor] = useState(false);

  return (
    <>
      <CompactCard
        title={<><TagsOutlined /> Tag Management</>}
      >
        <CompactSpace vertical style={{ width: '100%' }}>
          <CompactParagraph>Manage mod tags and categories</CompactParagraph>
          <CompactSpace>
            <CompactButton
              type="primary"
              icon={<TagsOutlined />}
              onClick={() => setShowTagEditor(true)}
            >
              Edit Tags
            </CompactButton>
            <CompactButton icon={<ReloadOutlined />}>
              Refresh Tags
            </CompactButton>
          </CompactSpace>

          <CompactDivider />

          <CompactSection title="Common Tags">
            <CompactSpace wrap>
              <Tag color="blue">Character</Tag>
              <Tag color="green">Weapon</Tag>
              <Tag color="orange">UI</Tag>
              <Tag color="purple">Effect</Tag>
              <Tag color="red">NSFW</Tag>
              <Tag color="cyan">Texture</Tag>
            </CompactSpace>
          </CompactSection>
        </CompactSpace>
      </CompactCard>

      {/* Tag Editor Modal - Placeholder */}
      <Modal
        title="Tag Editor"
        open={showTagEditor}
        onCancel={() => setShowTagEditor(false)}
        footer={[
          <CompactButton key="cancel" onClick={() => setShowTagEditor(false)}>
            Cancel
          </CompactButton>,
          <CompactButton key="save" type="primary" onClick={() => {
            notification.success('Tags saved');
            setShowTagEditor(false);
          }}>
            Save
          </CompactButton>,
        ]}
      >
        <CompactSpace vertical style={{ width: '100%' }}>
          <CompactSection title="Existing Tags">
            <CompactSpace wrap>
              <Tag closable>Custom Tag 1</Tag>
              <Tag closable>Custom Tag 2</Tag>
            </CompactSpace>
          </CompactSection>
        </CompactSpace>
      </Modal>
    </>
  );
};
