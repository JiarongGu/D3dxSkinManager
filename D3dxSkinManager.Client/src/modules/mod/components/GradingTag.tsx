import React from 'react';
import { Tag } from 'antd';

export interface GradingTagProps {
  grading: string;
}

export const GradingTag: React.FC<GradingTagProps> = ({ grading }) => {
  return <Tag color={getGradingColor(grading)}>{grading}</Tag>;
};

function getGradingColor(grading: string): string {
  switch (grading) {
    case 'G': return 'green';
    case 'P': return 'blue';
    case 'R': return 'orange';
    case 'X': return 'red';
    default: return 'default';
  }
};