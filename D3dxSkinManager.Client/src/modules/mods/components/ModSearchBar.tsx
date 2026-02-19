import React from 'react';
import { Input } from 'antd';
import { SearchOutlined } from '@ant-design/icons';

interface ModSearchBarProps {
  value: string;
  onChange: (value: string) => void;
  onSearch: (value: string) => void;
}

export const ModSearchBar: React.FC<ModSearchBarProps> = ({ value, onChange, onSearch }) => {
  return (
    <Input.Search
      placeholder="Search mods (supports ! for negation)"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      onSearch={onSearch}
      style={{ width: 350 }}
      enterButton={<SearchOutlined />}
    />
  );
};
