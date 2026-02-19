import React from 'react';
import { Typography } from 'antd';

const { Title, Paragraph, Text } = Typography;

/**
 * CompactTitle - A title component with reduced margins
 */
export interface CompactTitleProps {
  level?: 1 | 2 | 3 | 4 | 5;
  children: React.ReactNode;
  style?: React.CSSProperties;
  className?: string;
}

export const CompactTitle: React.FC<CompactTitleProps> = ({
  level = 4,
  children,
  style,
  ...rest
}) => {
  const defaultStyle: React.CSSProperties = {
    margin: '0 0 8px 0',
    ...style,
  };

  return (
    <Title level={level} style={defaultStyle} {...rest}>
      {children}
    </Title>
  );
};

/**
 * CompactParagraph - A paragraph component with reduced margins
 */
export interface CompactParagraphProps {
  children: React.ReactNode;
  style?: React.CSSProperties;
  className?: string;
  type?: 'secondary' | 'success' | 'warning' | 'danger';
}

export const CompactParagraph: React.FC<CompactParagraphProps> = ({
  children,
  style,
  ...rest
}) => {
  const defaultStyle: React.CSSProperties = {
    margin: 0,
    marginBottom: '8px',
    ...style,
  };

  return (
    <Paragraph style={defaultStyle} {...rest}>
      {children}
    </Paragraph>
  );
};

/**
 * CompactText - Re-export of Ant Design Text for consistency
 */
export const CompactText = Text;
