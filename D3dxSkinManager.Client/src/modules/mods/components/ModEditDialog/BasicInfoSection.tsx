import React from 'react';
import { Form, Input } from 'antd';

const { TextArea } = Input;

/**
 * Basic information section for mod editing
 * Includes mod name and description fields
 */
export const BasicInfoSection: React.FC = () => {
  return (
    <>
      {/* Name */}
      <Form.Item
        label="Mod Name"
        name="name"
        rules={[{ required: true, message: 'Please enter mod name' }]}
      >
        <Input placeholder="Enter mod name" />
      </Form.Item>

      {/* Description */}
      <Form.Item
        label="Description"
        name="description"
        tooltip="Detailed description or explanation of the mod"
      >
        <TextArea
          placeholder="Enter mod description..."
          rows={3}
          showCount
          maxLength={500}
        />
      </Form.Item>
    </>
  );
};
