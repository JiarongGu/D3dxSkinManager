/**
 * Keyboard Shortcuts Dialog (Phase 13)
 * Shows all available keyboard shortcuts
 */

import React from 'react';
import { Modal, Table, Tag, Typography, Divider } from 'antd';
import { CodeOutlined } from '@ant-design/icons';
import { KeyboardShortcutManager, ShortcutConfig } from '../../utils/KeyboardShortcutManager';

const { Title, Text } = Typography;

interface KeyboardShortcutsDialogProps {
  visible: boolean;
  onClose: () => void;
  shortcuts: Array<{ id: string; config: ShortcutConfig }>;
}

interface ShortcutGroup {
  title: string;
  shortcuts: Array<{ id: string; config: ShortcutConfig }>;
}

export const KeyboardShortcutsDialog: React.FC<KeyboardShortcutsDialogProps> = ({
  visible,
  onClose,
  shortcuts,
}) => {
  // Group shortcuts by context
  const groupedShortcuts: ShortcutGroup[] = React.useMemo(() => {
    const groups: Record<string, ShortcutGroup> = {
      global: { title: 'Global Shortcuts', shortcuts: [] },
      'mod-list': { title: 'Mod Management', shortcuts: [] },
      'import-window': { title: 'Import Window', shortcuts: [] },
      dialog: { title: 'Dialogs', shortcuts: [] },
    };

    shortcuts.forEach((shortcut) => {
      const context = shortcut.config.context || 'global';
      if (!groups[context]) {
        groups[context] = { title: context, shortcuts: [] };
      }
      groups[context].shortcuts.push(shortcut);
    });

    return Object.values(groups).filter((group) => group.shortcuts.length > 0);
  }, [shortcuts]);

  const columns = [
    {
      title: 'Shortcut',
      dataIndex: 'config',
      key: 'shortcut',
      width: 200,
      render: (config: ShortcutConfig) => (
        <Tag
          icon={<CodeOutlined />}
          color="blue"
          style={{ padding: '4px 12px', fontSize: '13px', fontWeight: 600 }}
        >
          {KeyboardShortcutManager.formatShortcut(config)}
        </Tag>
      ),
    },
    {
      title: 'Description',
      dataIndex: 'config',
      key: 'description',
      render: (config: ShortcutConfig) => (
        <Text style={{ fontSize: '14px' }}>{config.description}</Text>
      ),
    },
  ];

  return (
    <Modal
      title={
        <>
          <CodeOutlined style={{ marginRight: 8 }} />
          Keyboard Shortcuts
        </>
      }
      open={visible}
      onCancel={onClose}
      footer={null}
      width={700}
      styles={{ body: { maxHeight: '70vh', overflowY: 'auto' } }}
    >
      <div style={{ padding: '8px 0' }}>
        {groupedShortcuts.map((group, index) => (
          <div key={group.title}>
            {index > 0 && <Divider />}
            <Title level={5} style={{ marginTop: index > 0 ? 16 : 0, marginBottom: 12 }}>
              {group.title}
            </Title>
            <Table
              columns={columns}
              dataSource={group.shortcuts}
              pagination={false}
              rowKey="id"
              size="small"
              showHeader={false}
              style={{ marginBottom: 0 }}
            />
          </div>
        ))}

        <Divider />

        <div style={{ marginTop: 16, padding: '12px', background: '#f0f5ff', borderRadius: '4px' }}>
          <Text type="secondary" style={{ fontSize: '13px' }}>
            <strong>Tip:</strong> Press <Tag>Ctrl + /</Tag> or <Tag>?</Tag> to open this help dialog anytime.
          </Text>
        </div>
      </div>
    </Modal>
  );
};
