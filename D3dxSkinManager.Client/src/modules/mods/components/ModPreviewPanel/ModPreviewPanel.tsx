import React, { useState } from 'react';
import { Card, Typography, Tag, Space, Button, Empty, message, Carousel } from 'antd';
import {
  CopyOutlined,
  UserOutlined,
  TagsOutlined,
  FileTextOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  LeftOutlined,
  RightOutlined,
} from '@ant-design/icons';
import { GradingTag } from '../../../../shared/components/common/GradingTag';
import { FullScreenPreview } from './FullScreenPreview';
import { toAppUrl } from '../../../../shared/utils/imageUrlHelper';
import { ModPreviewProvider, useModView } from './ModPreviewContext';
import { ModInfo } from '../../../../shared/types/mod.types';

const { Text, Paragraph, Title } = Typography;

export const ModPreviewPanelContent: React.FC = () => {
  const { state } = useModView();
  const [fullScreenVisible, setFullScreenVisible] = useState(false);
  const [fullScreenImageSrc, setFullScreenImageSrc] = useState<string>('');

  const mod = state.currentMod;

  if (!mod) {
    return (
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          height: '100%',
          minHeight: '400px',
        }}
      >
        <Empty
          description="Select a mod to view details"
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
      </div>
    );
  }

  const handleCopySHA = () => {
    navigator.clipboard.writeText(mod.sha);
    message.success('SHA copied to clipboard');
  };

  const handleImageClick = (imageSrc: string) => {
    setFullScreenImageSrc(imageSrc);
    setFullScreenVisible(true);
  };

  // Determine which images to show (thumbnail + preview paths)
  const allImagePaths: string[] = [];

  // Add thumbnail first if available
  if (mod.thumbnailPath) {
    allImagePaths.push(mod.thumbnailPath);
  }

  // Add preview paths
  if (state.previewPaths && state.previewPaths.length > 0) {
    allImagePaths.push(...state.previewPaths);
  }

  const hasMultipleImages = allImagePaths.length > 1;

  return (
    <div style={{ padding: '16px' }}>
      {/* Preview Image(s) */}
      <Card
        style={{ marginBottom: '16px' }}
        styles={{ body: { padding: 0 } }}
        cover={
          allImagePaths.length > 0 ? (
            hasMultipleImages ? (
              <Carousel
                arrows
                prevArrow={<LeftOutlined />}
                nextArrow={<RightOutlined />}
                style={{ height: '200px' }}
              >
                {allImagePaths.map((path, index) => (
                  <div key={index}>
                    <img
                      alt={`${mod.name} - Preview ${index + 1}`}
                      src={toAppUrl(path) || undefined}
                      style={{
                        width: '100%',
                        height: '200px',
                        objectFit: 'cover',
                        cursor: 'pointer',
                      }}
                      onClick={() => handleImageClick(toAppUrl(path) || '')}
                      title="Click to view full screen"
                      onError={(e) => {
                        // Fallback to transparent placeholder on error
                        (e.target as HTMLImageElement).src = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
                      }}
                    />
                  </div>
                ))}
              </Carousel>
            ) : (
              <img
                alt={mod.name}
                src={toAppUrl(allImagePaths[0]) || undefined}
                style={{
                  width: '100%',
                  height: '200px',
                  objectFit: 'cover',
                  cursor: 'pointer',
                }}
                onClick={() => handleImageClick(toAppUrl(allImagePaths[0]) || '')}
                title="Click to view full screen"
                onError={(e) => {
                  // Fallback to transparent placeholder on error
                  (e.target as HTMLImageElement).src = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
                }}
              />
            )
          ) : (
            <div
              style={{
                width: '100%',
                height: '200px',
                background: '#f0f0f0',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#999',
              }}
            >
              No Preview Available
            </div>
          )
        }
      />

      {/* Mod Name */}
      <Title level={5} style={{ marginBottom: '8px', fontSize: '16px' }}>
        {mod.name}
      </Title>

      {/* Object Name */}
      <div style={{ marginBottom: '12px' }}>
        <Text type="secondary" style={{ fontSize: '13px' }}>
          <FileTextOutlined /> {mod.category}
        </Text>
      </div>

      {/* Status */}
      <div style={{ marginBottom: '12px' }}>
        <Space>
          {mod.isLoaded ? (
            <Tag icon={<CheckCircleOutlined />} color="success">
              Loaded
            </Tag>
          ) : (
            <Tag icon={<CloseCircleOutlined />} color="default">
              Not Loaded
            </Tag>
          )}
          <GradingTag grading={mod.grading} />
        </Space>
      </div>

      {/* Author */}
      {mod.author && (
        <div style={{ marginBottom: '12px' }}>
          <Text type="secondary" style={{ fontSize: '12px' }}>
            <UserOutlined /> Author:
          </Text>
          <br />
          <Text style={{ fontSize: '13px' }}>{mod.author}</Text>
        </div>
      )}

      {/* Tags */}
      {mod.tags && mod.tags.length > 0 && (
        <div style={{ marginBottom: '12px' }}>
          <Text type="secondary" style={{ fontSize: '12px', display: 'block', marginBottom: '4px' }}>
            <TagsOutlined /> Tags:
          </Text>
          <Space size={[4, 4]} wrap>
            {mod.tags.map((tag, index) => (
              <Tag key={index} style={{ fontSize: '11px' }}>
                {tag}
              </Tag>
            ))}
          </Space>
        </div>
      )}

      {/* Description */}
      {mod.description && (
        <div style={{ marginBottom: '16px' }}>
          <Text type="secondary" style={{ fontSize: '12px', display: 'block', marginBottom: '4px' }}>
            Description:
          </Text>
          <Paragraph
            style={{
              fontSize: '13px',
              marginBottom: 0,
              maxHeight: '120px',
              overflow: 'auto',
            }}
          >
            {mod.description}
          </Paragraph>
        </div>
      )}

      {/* SHA Hash */}
      <Card
        size="small"
        style={{ marginTop: '16px', background: '#fafafa' }}
        styles={{ body: { padding: '8px 12px' } }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ flex: 1, overflow: 'hidden' }}>
            <Text type="secondary" style={{ fontSize: '11px', display: 'block' }}>
              SHA256:
            </Text>
            <Text
              style={{
                fontSize: '11px',
                fontFamily: 'monospace',
                wordBreak: 'break-all',
                display: 'block',
                cursor: 'pointer',
                color: '#1890ff',
              }}
              onClick={handleCopySHA}
              title="Click to copy full SHA"
            >
              {mod.sha.substring(0, 16)}...
            </Text>
          </div>
          <Button
            type="text"
            size="small"
            icon={<CopyOutlined />}
            onClick={handleCopySHA}
            title="Copy SHA to clipboard"
          />
        </div>
      </Card>

      {/* Full Screen Preview Dialog */}
      <FullScreenPreview
        visible={fullScreenVisible}
        imageSrc={fullScreenImageSrc}
        imageAlt={mod.name}
        onClose={() => setFullScreenVisible(false)}
      />
    </div>
  );
};

export const ModPreviewPanel: React.FC<{ mod: ModInfo | null }> = ({ mod }) => {
  return (
    <ModPreviewProvider mod={mod}>
      <ModPreviewPanelContent />
    </ModPreviewProvider>
  );
};
