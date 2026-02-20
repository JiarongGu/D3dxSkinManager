/**
 * Keyboard Shortcuts Dialog (Phase 13)
 * Shows all available keyboard shortcuts
 */

import React from 'react';
import { Modal, Table, Tag, Typography, Divider } from 'antd';
import { CodeOutlined } from '@ant-design/icons';
import { KeyboardShortcutManager, ShortcutConfig } from '../../utils/KeyboardShortcutManager';
import { useTranslation } from 'react-i18next';
import './KeyboardShortcutsDialog.css';

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
  const { t } = useTranslation();

  // Group shortcuts by context
  const groupedShortcuts: ShortcutGroup[] = React.useMemo(() => {
    const groups: Record<string, ShortcutGroup> = {
      global: { title: t('shortcuts.groups.global'), shortcuts: [] },
      'mod-list': { title: t('shortcuts.groups.modManagement'), shortcuts: [] },
      'import-window': { title: t('shortcuts.groups.importWindow'), shortcuts: [] },
      dialog: { title: t('shortcuts.groups.dialogs'), shortcuts: [] },
    };

    shortcuts.forEach((shortcut) => {
      const context = shortcut.config.context || 'global';
      if (!groups[context]) {
        groups[context] = { title: context, shortcuts: [] };
      }
      groups[context].shortcuts.push(shortcut);
    });

    return Object.values(groups).filter((group) => group.shortcuts.length > 0);
  }, [shortcuts, t]);

  const columns = [
    {
      title: t('shortcuts.table.shortcut'),
      dataIndex: 'config',
      key: 'shortcut',
      width: 200,
      render: (config: ShortcutConfig) => (
        <Tag
          icon={<CodeOutlined />}
          color="blue"
          className="keyboard-shortcuts-tag"
        >
          {KeyboardShortcutManager.formatShortcut(config)}
        </Tag>
      ),
    },
    {
      title: t('shortcuts.table.description'),
      dataIndex: 'config',
      key: 'description',
      render: (config: ShortcutConfig) => (
        <Text className="keyboard-shortcuts-description">{config.description}</Text>
      ),
    },
  ];

  return (
    <Modal
      title={
        <>
          <CodeOutlined style={{ marginRight: 8 }} />
          {t('shortcuts.title')}
        </>
      }
      open={visible}
      onCancel={onClose}
      footer={null}
      width={700}
      styles={{ body: { maxHeight: '70vh', overflowY: 'auto' } }}
    >
      <div className="keyboard-shortcuts-content">
        {groupedShortcuts.map((group, index) => (
          <div key={group.title}>
            {index > 0 && <Divider />}
            <Title level={5} className="keyboard-shortcuts-group-title" style={{ marginTop: index > 0 ? 16 : 0 }}>
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

        <div className="keyboard-shortcuts-tip">
          <Text type="secondary" className="keyboard-shortcuts-tip-text">
            <strong>{t('shortcuts.tip.label')}:</strong> {t('shortcuts.tip.message')} <Tag>Ctrl + /</Tag> {t('shortcuts.tip.or')} <Tag>?</Tag>
          </Text>
        </div>
      </div>
    </Modal>
  );
};
