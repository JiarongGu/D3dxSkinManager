import React from 'react';
import { Space, Select, Button } from 'antd';
import { FilterOutlined, ReloadOutlined } from '@ant-design/icons';
import { gradingOptions } from '../../../shared/utils/grading.utils';

const { Option } = Select;

interface ModFilterPanelProps {
  selectedObject: string;
  selectedGrading: string;
  objects: string[];
  loading: boolean;
  onObjectChange: (value: string) => void;
  onGradingChange: (value: string) => void;
  onClearFilters: () => void;
  onRefresh: () => void;
}

export const ModFilterPanel: React.FC<ModFilterPanelProps> = ({
  selectedObject,
  selectedGrading,
  objects,
  loading,
  onObjectChange,
  onGradingChange,
  onClearFilters,
  onRefresh
}) => {
  return (
    <Space wrap>
      <Select
        placeholder="Filter by Object"
        value={selectedObject || undefined}
        onChange={onObjectChange}
        style={{ width: 150 }}
        allowClear
      >
        {objects.map(obj => (
          <Option key={obj} value={obj}>{obj}</Option>
        ))}
      </Select>
      <Select
        placeholder="Filter by Grading"
        value={selectedGrading || undefined}
        onChange={onGradingChange}
        style={{ width: 120 }}
        allowClear
      >
        {gradingOptions.map(option => (
          <Option key={option.value} value={option.value}>
            {option.label}
          </Option>
        ))}
      </Select>
      <Button icon={<FilterOutlined />} onClick={onClearFilters}>
        Clear Filters
      </Button>
      <Button icon={<ReloadOutlined />} onClick={onRefresh} loading={loading}>
        Refresh
      </Button>
    </Space>
  );
};
