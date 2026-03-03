/**
 * Help Window - Comprehensive Documentation
 * Uses SlideInScreen with vertical tabs for navigation
 */

import React, { useState } from 'react';
import { Typography, Space, Tag, Alert } from 'antd';
import {
  RocketOutlined,
  UserOutlined,
  FileTextOutlined,
  FolderOutlined,
  UploadOutlined,
  TagsOutlined,
  PlayCircleOutlined,
  ToolOutlined,
  BulbOutlined,
} from '@ant-design/icons';
import classNames from 'classnames';
import './HelpWindow.css';

const { Title, Paragraph } = Typography;

type HelpSection =
  | 'quickstart'
  | 'profiles'
  | 'mods'
  | 'categories'
  | 'import'
  | 'tags'
  | 'launch'
  | 'tools'
  | 'tips';

export const HelpWindow: React.FC = () => {
  const [activeSection, setActiveSection] = useState<HelpSection>('quickstart');

  // Navigation items for vertical tabs
  const navItems = [
    { key: 'quickstart', label: 'Quick Start', icon: <RocketOutlined /> },
    { key: 'profiles', label: 'Profiles', icon: <UserOutlined /> },
    { key: 'mods', label: 'Mod Management', icon: <FileTextOutlined /> },
    { key: 'categories', label: 'Category System', icon: <FolderOutlined /> },
    { key: 'import', label: 'Import Queue', icon: <UploadOutlined /> },
    { key: 'tags', label: 'Tag Management', icon: <TagsOutlined /> },
    { key: 'launch', label: 'Game Launch', icon: <PlayCircleOutlined /> },
    { key: 'tools', label: 'Tools & Utilities', icon: <ToolOutlined /> },
    { key: 'tips', label: 'Tips & Best Practices', icon: <BulbOutlined /> },
  ];

  // Content for Quick Start section
  const quickStartContent = (
    <Space className="help-window-content" vertical size="large">
      <Alert
        type="info"
        showIcon
        icon={<RocketOutlined />}
        description={
          <>
            <strong>Welcome to d3dx Skin Manager!</strong>
            <br />
            Get started with mod management in just a few steps.
          </>
        }
      />

      <div>
        <Title level={4}>1. Create or Select a Profile</Title>
        <Paragraph>
          Click the <Tag>Profile Selector</Tag> in the header to create a new profile or switch between existing ones.
          Each profile has its own mod database and settings, perfect for managing different games or configurations.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>2. Configure Game Settings</Title>
        <Paragraph>
          Navigate to <Tag>Settings</Tag> in the sidebar menu. Set your game executable path and configure
          other preferences like theme, language, and log level.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>3. Import Mods</Title>
        <Paragraph>
          You can import mods in several ways:
        </Paragraph>
        <ul className="help-window-list">
          <li><strong>Drag & Drop:</strong> Drag .zip files or folders directly into the mod table</li>
          <li><strong>Import Queue:</strong> Use the Workflow view to manage bulk imports with preview and editing</li>
          <li><strong>Context Menu:</strong> Right-click in the mod table for additional import options</li>
        </ul>
      </div>

      <div>
        <Title level={4}>4. Organize with Categories</Title>
        <Paragraph>
          Use the tree-based category panel on the left to organize your mods.
          Drag and drop mods into categories, create subcategories, and build a hierarchy that works for you.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>5. Load Mods & Launch Game</Title>
        <Paragraph>
          Toggle mods on/off using the <Tag color="green">Load</Tag>/<Tag color="red">Unload</Tag> buttons.
          When ready, go to <Tag>Launch</Tag> view to start your game with D3DMigoto and active mods.
        </Paragraph>
      </div>
    </Space>
  );

  // Content for Profiles section
  const profilesContent = (
    <Space className="help-window-content" vertical size="large">
      <Alert
        type="info"
        showIcon
        description={
          <>
            <strong>Profile Management</strong>
            <br />
            Manage multiple game configurations with separate profiles.
          </>
        }
      />

      <div>
        <Title level={4}>What are Profiles?</Title>
        <Paragraph>
          Profiles allow you to manage multiple game configurations independently. Each profile has:
        </Paragraph>
        <ul className="help-window-list">
          <li>Separate mod database (SQLite file)</li>
          <li>Independent game settings and paths</li>
          <li>Own category structure</li>
          <li>Isolated cache directory</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Creating a Profile</Title>
        <Paragraph>
          Click the <Tag>Profile Selector</Tag> dropdown in the header and select "Create New Profile".
          Give it a descriptive name (e.g., "Genshin Impact", "Honkai Star Rail").
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Switching Profiles</Title>
        <Paragraph>
          Use the profile selector in the header to switch between profiles instantly.
          All your mods and settings are preserved per profile.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>External Cache Support</Title>
        <Paragraph>
          You can configure profiles to use external cache directories for sharing resources
          across multiple profiles or keeping cache on a different drive.
        </Paragraph>
      </div>
    </Space>
  );

  // Content for Mod Management section
  const modsContent = (
    <Space className="help-window-content" vertical size="large">
      <Alert
        type="info"
        showIcon
        description={
          <>
            <strong>Mod Management</strong>
            <br />
            Load, unload, edit, and organize your mods.
          </>
        }
      />

      <div>
        <Title level={4}>Loading & Unloading Mods</Title>
        <Paragraph>
          Click the <Tag color="green">Load</Tag> button next to any mod to activate it.
          Loaded mods show a green status indicator. Use <Tag color="red">Unload</Tag> to deactivate.
          Multiple mods can be loaded simultaneously.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Editing Mod Metadata</Title>
        <Paragraph>
          Right-click any mod and select "Edit" to modify:
        </Paragraph>
        <ul className="help-window-list">
          <li><strong>Name:</strong> Custom display name</li>
          <li><strong>Author:</strong> Mod creator attribution</li>
          <li><strong>Description:</strong> Detailed notes about the mod</li>
          <li><strong>Tags:</strong> Custom tags for filtering and organization</li>
          <li><strong>Grading:</strong> Content rating (G, P, R, X)</li>
          <li><strong>Preview:</strong> Upload thumbnail images</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Search & Filtering</Title>
        <Paragraph>
          Use the search bar to filter mods by name, author, or tags.
          Prefix searches with <Tag>!</Tag> for negation (e.g., <Tag>!NSFW</Tag>).
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Context Menu Actions</Title>
        <Paragraph>
          Right-click on any mod for quick actions:
        </Paragraph>
        <ul className="help-window-list">
          <li>Load/Unload mod</li>
          <li>Edit mod metadata</li>
          <li>Export mod archive</li>
          <li>Copy SHA hash or name</li>
          <li>View file locations (original/work/cache)</li>
          <li>View preview image</li>
          <li>Delete mod</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Preview Panel</Title>
        <Paragraph>
          Click the Preview toggle button to show/hide the preview panel on the right side.
          Select any mod to see its thumbnail, metadata, and quick actions. Changes are debounced
          to prevent excessive IPC calls for smooth performance.
        </Paragraph>
      </div>
    </Space>
  );

  // Content for Category System section
  const categoriesContent = (
    <Space className="help-window-content" vertical size="large">
      <Alert
        type="info"
        showIcon
        description={
          <>
            <strong>Category System</strong>
            <br />
            Organize mods with a hierarchical tree-based category structure.
          </>
        }
      />

      <div>
        <Title level={4}>Tree-Based Organization</Title>
        <Paragraph>
          The category system uses a GUID-based tree structure for stable organization.
          Create unlimited levels of categories and subcategories to match your workflow.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Creating Categories</Title>
        <Paragraph>
          Right-click in the category panel and select "Create Category".
          To create subcategories, right-click on a parent category and select "Create Subcategory".
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Drag & Drop</Title>
        <Paragraph>
          Drag mods from the mod table into categories to organize them.
          You can also drag categories to reorder them or nest them under other categories.
        </Paragraph>
        <ul className="help-window-list">
          <li><strong>Top 15% of category:</strong> Drop above</li>
          <li><strong>Middle 70%:</strong> Drop into (make child)</li>
          <li><strong>Bottom 15%:</strong> Drop below</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Viewing Category Contents</Title>
        <Paragraph>
          Click any category to view all mods in that category and its subcategories.
          Parent nodes automatically show all descendant mods, making it easy to see everything at a glance.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Unclassified Mods</Title>
        <Paragraph>
          Mods without a category appear in the "Unclassified" section.
          These can be loaded simultaneously with categorized mods for flexible organization.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Renaming & Deleting</Title>
        <Paragraph>
          Right-click any category to rename or delete it. The GUID-based system ensures
          stable references even when renaming, with no cascading updates required.
        </Paragraph>
      </div>
    </Space>
  );

  // Content for Import Queue section
  const importContent = (
    <Space className="help-window-content" vertical size="large">
      <Alert
        type="info"
        showIcon
        description={
          <>
            <strong>Import Queue</strong>
            <br />
            Manage bulk mod imports with a workflow-based queue system.
          </>
        }
      />

      <div>
        <Title level={4}>What is the Import Queue?</Title>
        <Paragraph>
          The Import Queue (formerly "Workflow") provides a download-manager-style interface
          for managing bulk mod imports. Add multiple mods to the queue, edit their metadata,
          and import them when ready.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Adding Mods to Queue</Title>
        <Paragraph>
          Drag and drop multiple .zip files or folders into the mod table to add them to the import queue.
          Each mod appears in the Workflow view where you can preview and edit before final import.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Editing Before Import</Title>
        <Paragraph>
          While mods are in the queue, you can:
        </Paragraph>
        <ul className="help-window-list">
          <li>Edit name, author, description</li>
          <li>Add tags and set grading</li>
          <li>Upload preview images</li>
          <li>Review extracted metadata</li>
        </ul>
        <Paragraph>
          Background compression happens during editing, so imports are fast when you're ready.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Batch Operations</Title>
        <Paragraph>
          Select multiple items in the queue to perform batch operations:
        </Paragraph>
        <ul className="help-window-list">
          <li><strong>Delete:</strong> Remove multiple workflows (with partial failure handling)</li>
          <li><strong>Resume:</strong> Restart failed imports</li>
          <li><strong>Import All:</strong> Complete all pending imports at once</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Persistent Storage</Title>
        <Paragraph>
          The import queue is stored in SQLite, so your pending imports persist across sessions.
          You can close the app and return to complete imports later.
        </Paragraph>
      </div>
    </Space>
  );

  // Content for Tags section
  const tagsContent = (
    <Space className="help-window-content" vertical size="large">
      <Alert
        type="info"
        showIcon
        description={
          <>
            <strong>Tag Management</strong>
            <br />
            Create, edit, and manage tags with colors for better mod organization.
          </>
        }
      />

      <div>
        <Title level={4}>What are Tags?</Title>
        <Paragraph>
          Tags are custom labels with color coding that help you organize and filter mods.
          Common tags include: Character names, HD, NSFW, Favorite, etc.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Tag Management Tool</Title>
        <Paragraph>
          Access the Tag Management tool from the <Tag>Tools</Tag> menu in the sidebar.
          This dedicated interface lets you:
        </Paragraph>
        <ul className="help-window-list">
          <li><strong>Create:</strong> Add new tags with custom colors</li>
          <li><strong>Edit:</strong> Rename tags or change colors (updates all mods)</li>
          <li><strong>Delete:</strong> Remove tags (removes from all mods)</li>
          <li><strong>Search:</strong> Find specific tags quickly</li>
          <li><strong>View Usage:</strong> See how many mods use each tag</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Applying Tags to Mods</Title>
        <Paragraph>
          When editing mod metadata, type tag names in the tags field.
          Tags automatically appear with their assigned colors throughout the UI.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Color Coding</Title>
        <Paragraph>
          Choose from preset colors or use custom hex values. Color changes update
          in real-time across all UI components where the tag appears.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Filtering by Tags</Title>
        <Paragraph>
          Use the search bar with tag names to filter mods. Prefix with <Tag>!</Tag> to exclude
          mods with specific tags (e.g., <Tag>!NSFW</Tag> shows all non-NSFW mods).
        </Paragraph>
      </div>
    </Space>
  );

  // Content for Launch section
  const launchContent = (
    <Space className="help-window-content" vertical size="large">
      <Alert
        type="info"
        showIcon
        description={
          <>
            <strong>Game Launch</strong>
            <br />
            Launch your game with D3DMigoto and active mods.
          </>
        }
      />

      <div>
        <Title level={4}>Launching with D3DMigoto</Title>
        <Paragraph>
          Navigate to the <Tag>Launch</Tag> view in the sidebar. This tab provides:
        </Paragraph>
        <ul className="help-window-list">
          <li><strong>Game Launch:</strong> Start your game executable with D3DMigoto</li>
          <li><strong>D3DMigoto Launch:</strong> Launch the D3DMigoto loader directly</li>
          <li><strong>Configuration:</strong> Set environment variables and launch arguments</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Prerequisites</Title>
        <Paragraph>
          Before launching, ensure:
        </Paragraph>
        <ul className="help-window-list">
          <li>Game executable path is set in Settings</li>
          <li>D3DMigoto is properly installed in your game directory</li>
          <li>At least one mod is loaded (green status)</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Launch Options</Title>
        <Paragraph>
          Configure additional launch options:
        </Paragraph>
        <ul className="help-window-list">
          <li><strong>Launch Arguments:</strong> Pass command-line arguments to the game</li>
          <li><strong>Environment Variables:</strong> Set custom environment variables</li>
          <li><strong>Working Directory:</strong> Specify the working directory</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Troubleshooting Launch Issues</Title>
        <Paragraph>
          If the game won't launch:
        </Paragraph>
        <ul className="help-window-list">
          <li>Verify game path points to the .exe file (not a shortcut)</li>
          <li>Check D3DMigoto is in the game directory</li>
          <li>Try launching the game directly first to verify it works</li>
          <li>Check antivirus isn't blocking the manager or D3DMigoto</li>
          <li>Review logs in the Settings view for error messages</li>
        </ul>
      </div>
    </Space>
  );

  // Content for Tools section
  const toolsContent = (
    <Space className="help-window-content" vertical size="large">
      <Alert
        type="info"
        showIcon
        description={
          <>
            <strong>Tools & Utilities</strong>
            <br />
            Additional tools for maintenance, migration, and troubleshooting.
          </>
        }
      />

      <div>
        <Title level={4}>Python Migration Tool</Title>
        <Paragraph>
          Import legacy Python-based mod manager configurations. This tool:
        </Paragraph>
        <ul className="help-window-list">
          <li>Parses old Python config files</li>
          <li>Migrates mod metadata and organization</li>
          <li>Preserves tags and categories</li>
          <li>Handles large mod collections efficiently</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Tag Management Tool</Title>
        <Paragraph>
          Bulk tag operations with a dedicated interface (see Tag Management section for details).
          Access from <Tag>Tools</Tag> menu for full CRUD operations on tags.
        </Paragraph>
      </div>

      <div>
        <Title level={4}>Cache Management</Title>
        <Paragraph>
          Manage cached files and thumbnails:
        </Paragraph>
        <ul className="help-window-list">
          <li><strong>Scan Cache:</strong> Verify cache integrity</li>
          <li><strong>Clear Cache:</strong> Remove temporary files</li>
          <li><strong>Rebuild Thumbnails:</strong> Regenerate preview images</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Startup Validation</Title>
        <Paragraph>
          Validates configuration on startup:
        </Paragraph>
        <ul className="help-window-list">
          <li>Checks game paths exist</li>
          <li>Verifies D3DMigoto installation</li>
          <li>Validates database integrity</li>
          <li>Reports configuration issues</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Plugin System</Title>
        <Paragraph>
          View and manage plugins from the <Tag>Plugins</Tag> view.
          The plugin system supports multiple games and mod formats with extensible architecture.
        </Paragraph>
      </div>
    </Space>
  );

  // Content for Tips section
  const tipsContent = (
    <Space className="help-window-content" vertical size="large">
      <Alert
        type="success"
        showIcon
        icon={<BulbOutlined />}
        description={
          <>
            <strong>Tips & Best Practices</strong>
            <br />
            Power user tips for efficient mod management.
          </>
        }
      />

      <div>
        <Title level={4}>Organization Tips</Title>
        <ul className="help-window-list">
          <li>Use descriptive category names that match your game structure</li>
          <li>Create tag hierarchies (e.g., "Character - Raiden", "Character - Furina")</li>
          <li>Add author names for proper attribution and easy searching</li>
          <li>Set grading consistently for content filtering</li>
          <li>Write descriptions for complex mods with special requirements</li>
          <li>Use the Unclassified section for temporary or testing mods</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Performance Tips</Title>
        <ul className="help-window-list">
          <li>Clear cache regularly via Tools → Cache Management</li>
          <li>Use external cache directories on SSDs for faster loading</li>
          <li>Disable Preview panel when not needed (reduces IPC overhead)</li>
          <li>Use batch operations in Import Queue instead of one-by-one imports</li>
          <li>Set log level to "Warning" or "Error" in production (reduces disk I/O)</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Workflow Tips</Title>
        <ul className="help-window-list">
          <li>Use Import Queue for bulk imports with consistent metadata</li>
          <li>Create category templates before importing large collections</li>
          <li>Learn keyboard shortcuts (press <Tag>F1</Tag> or <Tag>?</Tag> to view all)</li>
          <li>Use right-click context menus for quick actions</li>
          <li>Export mods before deleting for backup purposes</li>
          <li>Use profile-specific configurations for different games</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Advanced Tips</Title>
        <ul className="help-window-list">
          <li>Use search negation (<Tag>!tag</Tag>) to exclude content</li>
          <li>Combine categories with tags for multi-dimensional organization</li>
          <li>Use external cache to share resources across profiles</li>
          <li>Check logs (Settings view) for troubleshooting issues</li>
          <li>Use Tag Management tool to standardize tag colors across your collection</li>
        </ul>
      </div>

      <div>
        <Title level={4}>Keyboard Shortcuts</Title>
        <Paragraph>
          Press <Tag>F1</Tag> or <Tag>?</Tag> to view all keyboard shortcuts. Common ones:
        </Paragraph>
        <ul className="help-window-list">
          <li><Tag>Ctrl + F</Tag> - Focus search</li>
          <li><Tag>F5</Tag> - Refresh list</li>
          <li><Tag>Delete</Tag> - Delete selected item</li>
          <li><Tag>Ctrl + A</Tag> - Select all</li>
          <li><Tag>Escape</Tag> - Close dialog</li>
        </ul>
      </div>
    </Space>
  );

  // Render content based on active section
  const renderContent = () => {
    switch (activeSection) {
      case 'quickstart':
        return quickStartContent;
      case 'profiles':
        return profilesContent;
      case 'mods':
        return modsContent;
      case 'categories':
        return categoriesContent;
      case 'import':
        return importContent;
      case 'tags':
        return tagsContent;
      case 'launch':
        return launchContent;
      case 'tools':
        return toolsContent;
      case 'tips':
        return tipsContent;
      default:
        return quickStartContent;
    }
  };

  return (
    <div className="help-window-layout">
      {/* Vertical navigation sidebar */}
      <div className="help-window-nav">
        {navItems.map((item) => (
          <div
            key={item.key}
            className={classNames('help-window-nav-item', {
              'help-window-nav-item--active': activeSection === item.key,
            })}
            onClick={() => setActiveSection(item.key as HelpSection)}
          >
            <span className="help-window-nav-item-icon">{item.icon}</span>
            <span className="help-window-nav-item-label">{item.label}</span>
          </div>
        ))}
      </div>

      {/* Content area */}
      <div className="help-window-content-area">{renderContent()}</div>
    </div>
  );
};
