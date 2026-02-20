import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Form, Input, Select, Upload, Button,  Space } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import type { UploadFile } from 'antd';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { useSlideInScreen } from '../../../../shared/context/SlideInScreenContext';

const { TextArea } = Input;

interface ClassificationScreenProps {
  /**
   * Parent node ID (undefined for root classification)
   */
  parentId?: string;

  /**
   * Classification tree for parent selection
   */
  tree: ClassificationNode[];

  /**
   * Callback when classification is saved
   */
  onSave: (data: {
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
 * Content component for classification creation/editing
 */
export const ClassificationScreenContent: React.FC<ClassificationScreenProps & { screenId: string }> = ({
  parentId,
  tree,
  onSave,
  screenId
}) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [thumbnailFile, setThumbnailFile] = useState<UploadFile[]>([]);
  const [thumbnailPreview, setThumbnailPreview] = useState<string | undefined>();
  const { closeScreen } = useSlideInScreen();

  // Initialize form
  useEffect(() => {
    form.resetFields();
    setThumbnailFile([]);
    setThumbnailPreview(undefined);

    // Set parent if provided
    if (parentId) {
      form.setFieldsValue({ parentId });
    }
  }, [parentId, form]);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      await onSave({
        name: values.name,
        parentId: values.parentId,
        thumbnail: thumbnailPreview,
        description: values.description
      });

      notification.success('Classification saved successfully');
      closeScreen(screenId);
    } catch (error: any) {
      if (error.errorFields) {
        // Form validation error
        return;
      }
      console.error('Failed to save classification:', error);
      notification.error('Failed to save classification');
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    closeScreen(screenId);
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
    <div>
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
            size="large"
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
            size="large"
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
            rows={4}
            maxLength={500}
            showCount
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
            <Button icon={<UploadOutlined />} size="large">Select Image</Button>
          </Upload>

          {thumbnailPreview && (
            <div style={{ marginTop: '16px' }}>
              <img
                src={thumbnailPreview}
                alt="Thumbnail preview"
                style={{
                  maxWidth: '300px',
                  maxHeight: '300px',
                  objectFit: 'contain',
                  border: '1px solid var(--color-border-secondary)',
                  borderRadius: '8px',
                  padding: '8px',
                  background: 'var(--color-bg-elevated)'
                }}
              />
            </div>
          )}
        </Form.Item>
      </Form>

      <div className="slide-in-screen-footer">
        <Space>
          <Button onClick={handleCancel} size="large">
            Cancel
          </Button>
          <Button
            type="primary"
            onClick={handleSubmit}
            loading={loading}
            size="large"
          >
            Save Classification
          </Button>
        </Space>
      </div>
    </div>
  );
};

/**
 * Hook to open classification screen
 */
export function useClassificationScreen() {
  const { openScreen } = useSlideInScreen();

  const openClassificationScreen = (props: ClassificationScreenProps) => {
    // Create a wrapper that will receive the screenId
    let actualScreenId = '';

    const ContentWrapper = () => (
      <ClassificationScreenContent {...props} screenId={actualScreenId} />
    );

    actualScreenId = openScreen({
      title: 'Add Classification',
      width: '50%',
      content: <ContentWrapper />,
    });

    return actualScreenId;
  };

  return { openClassificationScreen };
}
