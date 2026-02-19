import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Select, Upload, Button, message } from 'antd';
import { UploadOutlined, PictureOutlined } from '@ant-design/icons';
import type { UploadFile } from 'antd';
import { ClassificationNode } from '../../../shared/types/classification.types';

const { TextArea } = Input;

interface ClassificationDialogProps {
  /**
   * Whether the dialog is visible
   */
  visible: boolean;

  /**
   * Parent node ID (undefined for root classification)
   */
  parentId?: string;

  /**
   * Classification tree for parent selection
   */
  tree: ClassificationNode[];

  /**
   * Callback when dialog is closed
   */
  onClose: () => void;

  /**
   * Callback when classification is saved
   */
  onSave: (data: {
    id: string;
    name: string;
    parentId?: string;
    thumbnail?: string;
    description?: string;
  }) => Promise<void>;
}

/**
 * Flatten tree to get all nodes for parent selection
 */
function flattenTree(nodes: ClassificationNode[]): ClassificationNode[] {
  const result: ClassificationNode[] = [];

  const traverse = (node: ClassificationNode) => {
    result.push(node);
    node.children.forEach(child => traverse(child));
  };

  nodes.forEach(node => traverse(node));
  return result;
}

/**
 * Dialog for creating/editing classifications
 */
export const ClassificationDialog: React.FC<ClassificationDialogProps> = ({
  visible,
  parentId,
  tree,
  onClose,
  onSave
}) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [thumbnailFile, setThumbnailFile] = useState<UploadFile[]>([]);
  const [thumbnailPreview, setThumbnailPreview] = useState<string | undefined>();
  const [idManuallyEdited, setIdManuallyEdited] = useState(false);

  // Reset form when dialog opens
  useEffect(() => {
    if (visible) {
      form.resetFields();
      setThumbnailFile([]);
      setThumbnailPreview(undefined);
      setIdManuallyEdited(false);

      // Set parent if provided
      if (parentId) {
        form.setFieldsValue({ parentId });
      }
    }
  }, [visible, parentId, form]);

  // Auto-sync ID with name when name changes (unless ID was manually edited)
  const handleNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newName = e.target.value;
    if (!idManuallyEdited) {
      form.setFieldsValue({ id: newName });
    }
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      await onSave({
        id: values.id,
        name: values.name,
        parentId: values.parentId,
        thumbnail: thumbnailPreview,
        description: values.description
      });

      message.success('Classification saved successfully');
      onClose();
    } catch (error: any) {
      if (error.errorFields) {
        // Form validation error
        return;
      }
      console.error('Failed to save classification:', error);
      message.error('Failed to save classification');
    } finally {
      setLoading(false);
    }
  };

  const handleThumbnailChange = (info: any) => {
    setThumbnailFile(info.fileList.slice(-1)); // Keep only the last file

    const file = info.file.originFileObj || info.file;
    if (file && file.type?.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = (e) => {
        setThumbnailPreview(e.target?.result as string);
      };
      reader.readAsDataURL(file);
    }
  };

  const allNodes = flattenTree(tree);

  return (
    <Modal
      title="Add Classification"
      open={visible}
      onCancel={onClose}
      onOk={handleSubmit}
      confirmLoading={loading}
      width={600}
      okText="Save"
      cancelText="Cancel"
    >
      <Form
        form={form}
        layout="vertical"
        initialValues={{
          parentId: parentId
        }}
      >
        <Form.Item
          name="name"
          label="Classification Name"
          rules={[
            { required: true, message: 'Please enter a classification name' },
            { min: 1, max: 100, message: 'Name must be between 1 and 100 characters' }
          ]}
        >
          <Input
            placeholder="e.g., Character, Weapon, Outfit"
            autoFocus
            onChange={handleNameChange}
          />
        </Form.Item>

        <Form.Item
          name="id"
          label="Classification ID"
          rules={[
            { required: true, message: 'Please enter a classification ID' },
            {
              pattern: /^[a-zA-Z0-9_\u4e00-\u9fa5\-]+$/,
              message: 'ID can only contain letters, numbers, underscores, hyphens, and Chinese characters'
            },
            { min: 1, max: 100, message: 'ID must be between 1 and 100 characters' }
          ]}
          help="By default, ID will match the name. You can modify it if needed."
        >
          <Input
            placeholder="Auto-generated from name"
            onChange={() => setIdManuallyEdited(true)}
          />
        </Form.Item>

        <Form.Item
          name="parentId"
          label="Parent Classification"
          help="Leave empty to create a root classification"
        >
          <Select
            placeholder="Select parent (optional)"
            allowClear
            showSearch
            optionFilterProp="label"
            options={[
              { value: '', label: '(Root - No Parent)' },
              ...allNodes.map(node => ({
                value: node.id,
                label: node.name
              }))
            ]}
          />
        </Form.Item>

        <Form.Item
          name="description"
          label="Description (Optional)"
        >
          <TextArea
            placeholder="Optional description for this classification"
            rows={3}
            maxLength={500}
          />
        </Form.Item>

        <Form.Item
          label="Thumbnail (Optional)"
          help="Upload an image to represent this classification"
        >
          <Upload
            fileList={thumbnailFile}
            onChange={handleThumbnailChange}
            beforeUpload={() => false} // Prevent auto upload
            accept="image/*"
            maxCount={1}
            listType="picture"
          >
            <Button icon={<UploadOutlined />}>Select Image</Button>
          </Upload>

          {thumbnailPreview && (
            <div style={{ marginTop: '12px' }}>
              <img
                src={thumbnailPreview}
                alt="Thumbnail preview"
                style={{
                  maxWidth: '200px',
                  maxHeight: '200px',
                  objectFit: 'contain',
                  border: '1px solid #d9d9d9',
                  borderRadius: '4px',
                  padding: '4px'
                }}
              />
            </div>
          )}
        </Form.Item>
      </Form>
    </Modal>
  );
};
