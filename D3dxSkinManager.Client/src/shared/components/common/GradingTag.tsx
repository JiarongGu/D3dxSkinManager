import React from 'react';
import { Tag } from 'antd';
import { getGradingColor } from '../../../shared/utils/grading.utils';

export interface GradingTagProps {
  grading: string;
}

export const GradingTag: React.FC<GradingTagProps> = ({ grading }) => {
  return <Tag color={getGradingColor(grading)}>{grading}</Tag>;
};
