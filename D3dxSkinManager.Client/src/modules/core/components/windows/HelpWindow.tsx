/**
 * Help Window (Phase 14)
 * Comprehensive help documentation and quick start guide
 */

import React, { useState } from 'react';
import { Modal, Tabs, Typography, Collapse, Space, Tag, Alert } from 'antd';
import {
  QuestionCircleOutlined,
  RocketOutlined,
  BulbOutlined,
  ToolOutlined,
  WarningOutlined,
} from '@ant-design/icons';

const { Title, Text, Paragraph } = Typography;
const { Panel } = Collapse;

interface HelpWindowProps {
  visible: boolean;
  onClose: () => void;
}

export const HelpWindow: React.FC<HelpWindowProps> = ({ visible, onClose }) => {
  const [activeTab, setActiveTab] = useState('quickstart');

  const quickStartContent = (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Alert
        title="Welcome to d3dx Skin Manager!"
        description="Get started with mod management in just a few steps."
        type="info"
        showIcon
        icon={<RocketOutlined />}
      />

      <div>
        <Title level={4}>1. Configure Game Path</Title>
        <Paragraph>
          Navigate to <Tag>Settings</Tag> tab and set your game executable path.
          This tells the manager where your game is located and where to install mods.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>2. Import Mods</Title>
        <Paragraph>
          You can import mods in several ways:
        </Paragraph>
        <ul style={{ lineHeight: '2' }}>
          <li><strong>Drag & Drop:</strong> Simply drag .zip files or folders into the mod table</li>
          <li><strong>Context Menu:</strong> Right-click in the mod table → "Add Archive as Mod"</li>
          <li><strong>Mod Warehouse:</strong> Browse and download from online repositories</li>
        </ul>
      </div>

      <div>
        <Title level={4}>3. Load/Unload Mods</Title>
        <Paragraph>
          Click the <Tag color="green">Load</Tag> button next to any mod to activate it.
          Loaded mods are marked with a green status indicator. Use <Tag color="red">Unload</Tag> to deactivate.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>4. Launch Game</Title>
        <Paragraph>
          Once your mods are configured, go to <Tag>Settings</Tag> tab and click
          <Tag color="blue">Launch Game</Tag> to start playing with your mods active!
        </Paragraph>
      </div>
    </Space>
  );

  const featuresContent = (
    <Collapse accordion>
      <Panel header="Hierarchical Organization" key="1">
        <Paragraph>
          Mods are organized into a three-level hierarchy:
        </Paragraph>
        <ul style={{ lineHeight: '2' }}>
          <li><strong>Classification:</strong> Top-level category (Character, Weapon, etc.)</li>
          <li><strong>Object:</strong> Specific object or character name</li>
          <li><strong>Mod:</strong> Individual mod file</li>
        </ul>
        <Paragraph>
          Use the search bars to filter at each level. The format <Tag>[loaded/total]</Tag> shows how many mods are active.
        </Paragraph>
      </Panel>

      <Panel header="Search & Filtering" key="2">
        <Paragraph>
          Powerful search capabilities:
        </Paragraph>
        <ul style={{ lineHeight: '2' }}>
          <li>Search by name, author, or tags</li>
          <li>Use <Tag>!</Tag> prefix for negation (e.g., <Tag>!NSFW</Tag>)</li>
          <li>Filter by grading (G, P, R, X)</li>
          <li>Real-time updates as you type</li>
        </ul>
      </Panel>

      <Panel header="Batch Operations" key="3">
        <Paragraph>
          Efficiently manage multiple mods:
        </Paragraph>
        <ul style={{ lineHeight: '2' }}>
          <li><strong>Batch Edit:</strong> Select multiple mods and edit properties together</li>
          <li><strong>Import Queue:</strong> Add multiple files and configure before importing</li>
          <li><strong>Tag Management:</strong> Apply tags to multiple mods at once</li>
        </ul>
      </Panel>

      <Panel header="Context Menus" key="4">
        <Paragraph>
          Right-click on any mod for quick actions:
        </Paragraph>
        <ul style={{ lineHeight: '2' }}>
          <li>Load/Unload mod</li>
          <li>Edit mod information</li>
          <li>Export mod file</li>
          <li>Copy SHA or name</li>
          <li>View original/work/cache files</li>
          <li>View preview image</li>
          <li>Delete mod</li>
        </ul>
      </Panel>

      <Panel header="Keyboard Shortcuts" key="5">
        <Paragraph>
          Press <Tag>?</Tag> or <Tag>Ctrl + /</Tag> to view all keyboard shortcuts.
          Common shortcuts include:
        </Paragraph>
        <ul style={{ lineHeight: '2' }}>
          <li><Tag>Ctrl + F</Tag> - Focus search</li>
          <li><Tag>F5</Tag> - Refresh list</li>
          <li><Tag>Delete</Tag> - Delete selected item</li>
          <li><Tag>Ctrl + A</Tag> - Select all</li>
          <li><Tag>Escape</Tag> - Cancel/Close dialog</li>
        </ul>
      </Panel>

      <Panel header="Plugins System" key="6">
        <Paragraph>
          The manager supports 26+ plugins for different games and mod types.
          View and manage plugins in the <Tag>Plugins</Tag> tab.
        </Paragraph>
        <Paragraph>
          Each plugin handles specific file formats and provides game-specific functionality.
        </Paragraph>
      </Panel>
    </Collapse>
  );

  const troubleshootingContent = (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Alert
        title="Common Issues & Solutions"
        type="warning"
        showIcon
        icon={<WarningOutlined />}
      />

      <Collapse accordion>
        <Panel header="Mods not appearing in game" key="1">
          <ul style={{ lineHeight: '2' }}>
            <li>Ensure mods are <Tag color="green">Loaded</Tag> (green status indicator)</li>
            <li>Check game path is correctly configured in Settings</li>
            <li>Verify 3DMigoto is properly installed</li>
            <li>Some mods may conflict - try loading mods individually</li>
          </ul>
        </Panel>

        <Panel header="Import fails or gets stuck" key="2">
          <ul style={{ lineHeight: '2' }}>
            <li>Check file format (should be .zip, .rar, .7z, or folder)</li>
            <li>Ensure mod structure is correct (contains .ini or .buf files)</li>
            <li>Try importing one file at a time</li>
            <li>Check cache in Tools tab and clear if needed</li>
          </ul>
        </Panel>

        <Panel header="Thumbnails not showing" key="3">
          <ul style={{ lineHeight: '2' }}>
            <li>Thumbnails must be in PNG or JPG format</li>
            <li>Check Thumbnail Algorithm setting in Settings</li>
            <li>Try dragging preview images directly onto the mod table</li>
            <li>Scan cache in Tools tab</li>
          </ul>
        </Panel>

        <Panel header="Game won't launch" key="4">
          <ul style={{ lineHeight: '2' }}>
            <li>Verify game path points to the executable (.exe)</li>
            <li>Check launch arguments are correct</li>
            <li>Try launching game directly first</li>
            <li>Check antivirus isn't blocking the manager</li>
          </ul>
        </Panel>
      </Collapse>
    </Space>
  );

  const tipsContent = (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Alert
        title="Pro Tips & Best Practices"
        type="success"
        showIcon
        icon={<BulbOutlined />}
      />

      <div>
        <Title level={5}>Organization Tips</Title>
        <ul style={{ lineHeight: '2' }}>
          <li>Use descriptive names for your mods</li>
          <li>Add tags for easy filtering (Character, HD, NSFW, etc.)</li>
          <li>Set proper grading to filter content</li>
          <li>Add author names for attribution</li>
          <li>Write descriptions for complex mods</li>
        </ul>
      </div>

      <div>
        <Title level={5}>Performance Tips</Title>
        <ul style={{ lineHeight: '2' }}>
          <li>Clear cache regularly (Tools → Cache Management)</li>
          <li>Don't load too many mods simultaneously</li>
          <li>Use batch operations instead of one-by-one edits</li>
          <li>Enable annotation level "Less" or "Off" for better performance</li>
        </ul>
      </div>

      <div>
        <Title level={5}>Workflow Tips</Title>
        <ul style={{ lineHeight: '2' }}>
          <li>Use import queue for bulk imports</li>
          <li>Create classification hierarchies that match your game structure</li>
          <li>Use context menus (right-click) for quick actions</li>
          <li>Learn keyboard shortcuts for faster workflow</li>
          <li>Export mods before deleting (for backup)</li>
        </ul>
      </div>
    </Space>
  );

  const tabItems = [
    {
      key: 'quickstart',
      label: (
        <span>
          <RocketOutlined />
          Quick Start
        </span>
      ),
      children: quickStartContent,
    },
    {
      key: 'features',
      label: (
        <span>
          <ToolOutlined />
          Features
        </span>
      ),
      children: featuresContent,
    },
    {
      key: 'troubleshooting',
      label: (
        <span>
          <WarningOutlined />
          Troubleshooting
        </span>
      ),
      children: troubleshootingContent,
    },
    {
      key: 'tips',
      label: (
        <span>
          <BulbOutlined />
          Tips & Tricks
        </span>
      ),
      children: tipsContent,
    },
  ];

  return (
    <Modal
      title={
        <>
          <QuestionCircleOutlined style={{ marginRight: 8 }} />
          Help & Documentation
        </>
      }
      open={visible}
      onCancel={onClose}
      footer={null}
      width={800}
      styles={{ body: { maxHeight: '70vh', overflowY: 'auto' } }}
    >
      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={tabItems}
        size="large"
      />
    </Modal>
  );
};
