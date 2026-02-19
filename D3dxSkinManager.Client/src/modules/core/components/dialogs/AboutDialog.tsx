/**
 * About Dialog (Phase 14)
 * Shows version information, credits, and links
 */

import React from 'react';
import { Modal, Typography, Space, Divider, Tag } from 'antd';
import {
  GithubOutlined,
  InfoCircleOutlined,
  CopyrightOutlined,
  LinkOutlined
} from '@ant-design/icons';

const { Title, Text, Link, Paragraph } = Typography;

interface AboutDialogProps {
  visible: boolean;
  onClose: () => void;
}

export const AboutDialog: React.FC<AboutDialogProps> = ({ visible, onClose }) => {
  const version = '2.0.0';
  const buildDate = '2026-02-17';

  return (
    <Modal
      title={
        <>
          <InfoCircleOutlined style={{ marginRight: 8 }} />
          About d3dx Skin Manager
        </>
      }
      open={visible}
      onCancel={onClose}
      footer={null}
      width={600}
    >
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        {/* App Info */}
        <div style={{ textAlign: 'center', padding: '20px 0' }}>
          <Title level={2} style={{ marginBottom: 8 }}>
            d3dx Skin Manager
          </Title>
          <Space>
            <Tag color="blue" style={{ fontSize: '14px', padding: '4px 12px' }}>
              Version {version}
            </Tag>
            <Tag style={{ fontSize: '14px', padding: '4px 12px' }}>
              Build {buildDate}
            </Tag>
          </Space>
        </div>

        <Divider style={{ margin: '12px 0' }} />

        {/* Description */}
        <div>
          <Paragraph style={{ fontSize: '15px', lineHeight: '1.8', color: '#595959' }}>
            A modern, cross-platform skin/mod management tool for 3DMigoto-based games.
            Built with React, TypeScript, and .NET 8, featuring a comprehensive plugin system
            and intuitive hierarchical mod organization.
          </Paragraph>
        </div>

        {/* Tech Stack */}
        <div>
          <Title level={5}>
            <LinkOutlined style={{ marginRight: 8 }} />
            Technology Stack
          </Title>
          <Space wrap>
            <Tag color="geekblue">React 18</Tag>
            <Tag color="blue">TypeScript</Tag>
            <Tag color="purple">.NET 8</Tag>
            <Tag color="cyan">Ant Design v5</Tag>
            <Tag color="green">Photino.NET</Tag>
            <Tag color="orange">SQLite</Tag>
          </Space>
        </div>

        {/* Features */}
        <div>
          <Title level={5}>Key Features</Title>
          <ul style={{ lineHeight: '2', paddingLeft: '20px', color: '#595959' }}>
            <li>Hierarchical mod organization with classification system</li>
            <li>Plugin-based architecture (26+ plugins supported)</li>
            <li>Real-time search and filtering</li>
            <li>Batch operations and import queue management</li>
            <li>Comprehensive annotation and tooltip system</li>
            <li>Keyboard shortcuts for power users</li>
            <li>Mod warehouse for online browsing and downloads</li>
          </ul>
        </div>

        <Divider style={{ margin: '12px 0' }} />

        {/* Credits */}
        <div>
          <Title level={5}>
            <CopyrightOutlined style={{ marginRight: 8 }} />
            Credits
          </Title>
          <Space orientation="vertical" size="small">
            <Text>
              <strong>Original Python Version:</strong> Based on d3dxSkinManage
            </Text>
            <Text>
              <strong>Refactor & UI:</strong> React + .NET 8 + Photino.NET
            </Text>
            <Text>
              <strong>3DMigoto:</strong> Graphics mod injection framework
            </Text>
          </Space>
        </div>

        {/* Links */}
        <div>
          <Title level={5}>
            <GithubOutlined style={{ marginRight: 8 }} />
            Resources
          </Title>
          <Space orientation="vertical">
            <Link href="#" target="_blank">
              GitHub Repository
            </Link>
            <Link href="#" target="_blank">
              Documentation
            </Link>
            <Link href="#" target="_blank">
              Report Issues
            </Link>
            <Link href="#" target="_blank">
              3DMigoto Official Site
            </Link>
          </Space>
        </div>

        <Divider style={{ margin: '12px 0' }} />

        {/* License */}
        <div style={{ textAlign: 'center', padding: '12px', background: '#f0f0f0', borderRadius: '4px' }}>
          <Text type="secondary" style={{ fontSize: '13px' }}>
            Released under the MIT License
            <br />
            © 2026 d3dx Skin Manager Contributors
          </Text>
        </div>
      </Space>
    </Modal>
  );
};
