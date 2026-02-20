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
import { useTranslation } from 'react-i18next';
import './AboutDialog.css';

const { Title, Text, Link, Paragraph } = Typography;

interface AboutDialogProps {
  visible: boolean;
  onClose: () => void;
}

export const AboutDialog: React.FC<AboutDialogProps> = ({ visible, onClose }) => {
  const { t } = useTranslation();
  const version = '2.0.0';
  const buildDate = '2026-02-17';

  return (
    <Modal
      title={
        <>
          <InfoCircleOutlined style={{ marginRight: 8 }} />
          {t('about.title')}
        </>
      }
      open={visible}
      onCancel={onClose}
      footer={null}
      width={600}
    >
      <Space orientation="vertical" size="large" className="about-dialog-content">
        {/* App Info */}
        <div className="about-dialog-header">
          <Title level={2} className="about-dialog-app-title">
            {t('about.appName')}
          </Title>
          <Space>
            <Tag color="blue" className="about-dialog-tag">
              {t('about.version', { version })}
            </Tag>
            <Tag className="about-dialog-tag">
              {t('about.build', { buildDate })}
            </Tag>
          </Space>
        </div>

        <Divider className="about-dialog-divider" />

        {/* Description */}
        <div>
          <Paragraph className="about-dialog-description">
            {t('about.description')}
          </Paragraph>
        </div>

        {/* Tech Stack */}
        <div>
          <Title level={5}>
            <LinkOutlined style={{ marginRight: 8 }} />
            {t('about.techStack')}
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
          <Title level={5}>{t('about.keyFeatures')}</Title>
          <ul className="about-dialog-features">
            <li>{t('about.features.hierarchical')}</li>
            <li>{t('about.features.pluginArchitecture')}</li>
            <li>{t('about.features.realtimeSearch')}</li>
            <li>{t('about.features.batchOperations')}</li>
            <li>{t('about.features.annotationSystem')}</li>
            <li>{t('about.features.keyboardShortcuts')}</li>
            <li>{t('about.features.modWarehouse')}</li>
          </ul>
        </div>

        <Divider className="about-dialog-divider" />

        {/* Credits */}
        <div>
          <Title level={5}>
            <CopyrightOutlined style={{ marginRight: 8 }} />
            {t('about.credits')}
          </Title>
          <Space orientation="vertical" size="small">
            <Text>
              <strong>{t('about.credits.originalPython')}:</strong> {t('about.credits.basedOn')}
            </Text>
            <Text>
              <strong>{t('about.credits.refactorUI')}:</strong> React + .NET 8 + Photino.NET
            </Text>
            <Text>
              <strong>{t('about.credits.3dmigoto')}:</strong> {t('about.credits.graphicsFramework')}
            </Text>
          </Space>
        </div>

        {/* Links */}
        <div>
          <Title level={5}>
            <GithubOutlined style={{ marginRight: 8 }} />
            {t('about.resources')}
          </Title>
          <Space orientation="vertical">
            <Link href="#" target="_blank">
              {t('about.links.github')}
            </Link>
            <Link href="#" target="_blank">
              {t('about.links.documentation')}
            </Link>
            <Link href="#" target="_blank">
              {t('about.links.reportIssues')}
            </Link>
            <Link href="#" target="_blank">
              {t('about.links.3dmigotoSite')}
            </Link>
          </Space>
        </div>

        <Divider className="about-dialog-divider" />

        {/* License */}
        <div className="about-dialog-license">
          <Text type="secondary" className="about-dialog-license-text">
            {t('about.license.releasedUnder')}
            <br />
            {t('about.license.copyright')}
          </Text>
        </div>
      </Space>
    </Modal>
  );
};
