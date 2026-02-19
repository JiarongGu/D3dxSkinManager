import React from 'react';
import { Tag, Tooltip, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ModInfo } from '../../../shared/types/mod.types';
import { ModThumbnail } from '../../../shared/components/common/ModThumbnail';
import { StatusIcon } from '../../../shared/components/common/StatusIcon';
import { GradingTag } from '../../../shared/components/common/GradingTag';
import { ModActionButtons } from './ModActionButtons';

interface ModTableColumnsProps {
  objects: string[];
  authors: string[];
  onLoad: (sha: string) => void;
  onUnload: (sha: string) => void;
  onDelete: (sha: string, name: string) => void;
}

export const createModTableColumns = ({
  objects,
  authors,
  onLoad,
  onUnload,
  onDelete
}: ModTableColumnsProps): ColumnsType<ModInfo> => [
  {
    title: 'Thumbnail',
    dataIndex: 'thumbnailPath',
    key: 'thumbnail',
    width: 80,
    render: (thumbnailPath: string | undefined) => (
      <ModThumbnail thumbnailPath={thumbnailPath} />
    )
  },
  {
    title: 'Status',
    dataIndex: 'isLoaded',
    key: 'status',
    width: 60,
    render: (isLoaded: boolean) => <StatusIcon isLoaded={isLoaded} />
  },
  {
    title: 'Object',
    dataIndex: 'category',
    key: 'category',
    width: 130,
    sorter: (a, b) => a.category.localeCompare(b.category),
    filters: objects.map(obj => ({ text: obj, value: obj })),
    onFilter: (value, record) => record.category === value,
  },
  {
    title: 'SHA',
    dataIndex: 'sha',
    key: 'sha',
    width: 100,
    ellipsis: {
      showTitle: false,
    },
    render: (sha: string) => (
      <Tooltip title="Click to copy SHA">
        <span
          onClick={(e) => {
            e.stopPropagation();
            navigator.clipboard.writeText(sha);
            message.success('SHA copied to clipboard');
          }}
          style={{
            cursor: 'pointer',
            fontFamily: 'monospace',
            fontSize: '12px',
            color: '#1890ff',
          }}
        >
          {sha.substring(0, 8)}...
        </span>
      </Tooltip>
    ),
  },
  {
    title: 'Name',
    dataIndex: 'name',
    key: 'name',
    sorter: (a, b) => a.name.localeCompare(b.name),
    ellipsis: {
      showTitle: false,
    },
    render: (name, record: ModInfo) => {
      // Build rich tooltip content
      const tooltipContent = (
        <div style={{ maxWidth: '400px' }}>
          <div style={{ fontWeight: 'bold', marginBottom: '8px', fontSize: '14px' }}>
            {record.name}
          </div>
          {record.author && (
            <div style={{ marginBottom: '4px' }}>
              <span style={{ opacity: 0.8 }}>Author:</span> {record.author}
            </div>
          )}
          {record.tags && record.tags.length > 0 && (
            <div style={{ marginBottom: '4px' }}>
              <span style={{ opacity: 0.8 }}>Tags:</span> {record.tags.join(', ')}
            </div>
          )}
          {record.description && (
            <div style={{ marginTop: '8px', paddingTop: '8px', borderTop: '1px solid rgba(255,255,255,0.1)' }}>
              {record.description}
            </div>
          )}
        </div>
      );

      return (
        <Tooltip placement="topLeft" title={tooltipContent} mouseEnterDelay={0.3}>
          {name}
        </Tooltip>
      );
    },
  },
  {
    title: 'Author',
    dataIndex: 'author',
    key: 'author',
    width: 120,
    filters: authors.map(author => ({ text: author, value: author })),
    onFilter: (value, record) => record.author === value,
  },
  {
    title: 'Grading',
    dataIndex: 'grading',
    key: 'grading',
    width: 80,
    render: (grading: string) => <GradingTag grading={grading} />,
    filters: [
      { text: 'G - General', value: 'G' },
      { text: 'P - Parental Guidance', value: 'P' },
      { text: 'R - Restricted', value: 'R' },
      { text: 'X - Extreme', value: 'X' },
    ],
    onFilter: (value, record) => record.grading === value,
  },
  {
    title: 'Tags',
    dataIndex: 'tags',
    key: 'tags',
    width: 200,
    render: (tags: string[]) => (
      <>
        {tags.slice(0, 3).map(tag => (
          <Tag color="blue" key={tag} style={{ marginBottom: 4 }}>
            {tag}
          </Tag>
        ))}
        {tags.length > 3 && <Tag>+{tags.length - 3}</Tag>}
      </>
    ),
  },
  {
    title: 'Action',
    key: 'action',
    width: 140,
    render: (_, record) => (
      <ModActionButtons
        mod={record}
        onLoad={onLoad}
        onUnload={onUnload}
        onDelete={onDelete}
      />
    ),
  },
];
