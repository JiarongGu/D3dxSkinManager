import { notification } from "../../../../shared/utils/notification";
import React, { useState, useEffect, useCallback } from "react";
import { Form, Space, Row, Col, Select } from "antd";
import { FolderOpenOutlined } from "@ant-design/icons";
import { CategoryInfo } from "../../../../shared/types/category.types";
import { useSlideInScreenContext } from "../../../../shared/context/SlideInScreenContext";
import { systemService } from "../../../../shared/services/systemService";
import { toAppUrl } from "../../../../shared/utils/imageUrlHelper";
import { categoryService } from "../../../../shared/services/categoryService";
import { useProfile } from "../../../../shared/context/ProfileContext";
import {
  CompactInput,
  CompactTextArea,
  CompactSelect,
  CompactButton,
  CompactPrimaryButton,
  CompactUpload,
} from "../../../../shared/components/compact";
import { useTranslation } from "react-i18next";
import { debounce } from "lodash-es";
import "./CategoryScreen.css";

const { Option } = Select;

interface CategoryScreenProps {
  /**
   * Parent node ID (undefined for root Category)
   */
  parentId?: string;

  /**
   * Category tree for parent selection
   */
  tree: CategoryInfo[];

  /**
   * Node to edit (if editing existing Category)
   */
  editNode?: CategoryInfo;

  /**
   * Callback when Category is saved
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
function flattenTree(nodes: CategoryInfo[]): CategoryInfo[] {
  const result: CategoryInfo[] = [];

  const traverse = (node: CategoryInfo) => {
    result.push(node);
    node.children.forEach((child) => traverse(child));
  };

  nodes.forEach((node) => traverse(node));
  return result;
}

/**
 * Content component for Category creation/editing
 */
export const CategoryScreenContent: React.FC<
  CategoryScreenProps & { screenId: string }
> = ({ parentId, tree, editNode, onSave, screenId }) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [thumbnailPath, setThumbnailPath] = useState<string>();
  const [thumbnailFileName, setThumbnailFileName] = useState<string>();
  const { closeScreen } = useSlideInScreenContext();
  const { selectedProfileId} = useProfile();

  // Debounced name existence check - only call backend after user stops typing for 500ms
  const checkNameExistsDebounced = useCallback(
    debounce(
      async (
        profileId: string,
        name: string,
        excludeId?: string,
      ): Promise<boolean> => {
        return await categoryService.nameExists(profileId, name, excludeId);
      },
      500, // Wait 500ms after user stops typing
    ),
    [],
  );

  // Handle thumbnail drop from CompactUpload's OS-level drop zone
  const handleThumbnailDrop = (files: string[]) => {
    if (files.length === 0) return;

    const filePath = files[0]; // Take first file

    // Check if it's an image file
    const imageExtensions = [
      ".png",
      ".jpg",
      ".jpeg",
      ".gif",
      ".bmp",
      ".webp",
    ];
    const ext = filePath.toLowerCase().match(/\.[^.]+$/)?.[0];

    if (!ext || !imageExtensions.includes(ext)) {
      notification.warning(t("category.screen.dropImageOnly"));
      return;
    }

    // Use the real file path directly
    setThumbnailPath(filePath);
    const fileName = filePath.split(/[\\/]/).pop() || filePath;
    setThumbnailFileName(fileName);
  };

  // Initialize form
  useEffect(() => {
    form.resetFields();

    if (editNode) {
      // Edit mode - populate with existing node data
      form.setFieldsValue({
        name: editNode.name,
        parentId: editNode.parentId || "",
        description: editNode.description,
      });
      setThumbnailPath(editNode.thumbnail || undefined);
      if (editNode.thumbnail) {
        const fileName =
          editNode.thumbnail.split(/[\\/]/).pop() || editNode.thumbnail;
        setThumbnailFileName(fileName);
      }
    } else {
      // Create mode - set defaults based on parent
      if (parentId) {
        // Find parent node to get its details
        const allNodes = flattenTree(tree);
        const parentNode = allNodes.find((node) => node.id === parentId);

        if (parentNode) {
          // Default name to "{parentName}-"
          form.setFieldsValue({
            parentId,
            name: `${parentNode.name}-`
          });

          // Default thumbnail to parent's thumbnail
          if (parentNode.thumbnail) {
            setThumbnailPath(parentNode.thumbnail);
            const fileName =
              parentNode.thumbnail.split(/[\\/]/).pop() || parentNode.thumbnail;
            setThumbnailFileName(fileName);
          }
        } else {
          form.setFieldsValue({ parentId });
        }
      } else {
        // Root category - clear everything
        setThumbnailPath(undefined);
        setThumbnailFileName(undefined);
      }
    }
  }, [parentId, editNode, form, tree]);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      await onSave({
        name: values.name,
        parentId: values.parentId,
        thumbnail: thumbnailPath,
        description: values.description,
      });

      notification.success(t("category.screen.saved"));
      closeScreen(screenId);
    } catch (error: unknown) {
      // Check if this is a form validation error
      if (error && typeof error === 'object' && 'errorFields' in error) {
        // Form validation error - don't show notification
        return;
      }
      notification.error(t("category.screen.saveFailed"));
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    closeScreen(screenId);
  };

  const handleBrowseThumbnail = async () => {
    try {
      const result = await systemService.openFileDialog({
        title: t("category.screen.selectThumbnailTitle"),
        filters: [
          {
            name: t("category.screen.imageFiles"),
            extensions: ["png", "jpg", "jpeg", "gif", "bmp", "webp"],
          },
        ],
        rememberPathKey: "category-thumbnail", // Remember last path used for thumbnail selection
      });

      if (result.success && result.filePath) {
        setThumbnailPath(result.filePath);
        const fileName =
          result.filePath.split(/[\\/]/).pop() || result.filePath;
        setThumbnailFileName(fileName);
      }
    } catch (error: unknown) {
      const errorMessage =
        error instanceof Error ? error.message : "Unknown error";
      notification.error(
        `${t("category.screen.selectThumbnailFailed")}: ${errorMessage}`,
      );
    }
  };

  const allNodes = flattenTree(tree);

  return (
    <div>
      <Form
        form={form}
        layout="vertical"
        initialValues={{
          parentId: parentId,
        }}
      >
        {/* Compact row: Name and Parent side by side */}
        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              name="name"
              label={t("category.screen.nameLabel")}
              rules={[
                {
                  required: true,
                  message: t("category.screen.nameRequired"),
                },
                {
                  min: 1,
                  max: 100,
                  message: t("category.screen.nameLength"),
                },
                {
                  validator: async (_: any, value: string) => {
                    if (!value || !selectedProfileId) return Promise.resolve();
                    // Debounced check - only calls backend after user stops typing for 500ms
                    const exists = await checkNameExistsDebounced(
                      selectedProfileId,
                      value,
                      editNode?.id, // Exclude current node when editing
                    );
                    if (exists) {
                      return Promise.reject(t("category.screen.nameExists"));
                    }
                    return Promise.resolve();
                  },
                },
              ]}
            >
              <CompactInput
                placeholder={t("category.screen.namePlaceholder")}
                autoFocus
              />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item name="parentId" label={t("category.screen.parentLabel")}>
              <CompactSelect
                placeholder={t("category.screen.parentPlaceholder")}
                allowClear
                showSearch
                filterOption={(input, option) =>
                  (option?.label?.toString().toLowerCase() ?? "").includes(
                    input.toLowerCase(),
                  )
                }
                options={[
                  { value: "", label: t("category.screen.rootNoParent") },
                  ...allNodes.map((node) => ({
                    value: node.id,
                    label: node.name,
                  })),
                ]}
              />
            </Form.Item>
          </Col>
        </Row>

        {/* Compact description - smaller textarea */}
        <Form.Item
          name="description"
          label={t("category.screen.descriptionLabel")}
        >
          <CompactTextArea
            placeholder={t("category.screen.descriptionPlaceholder")}
            rows={2}
            maxLength={500}
            showCount
          />
        </Form.Item>

        {/* Thumbnail with drag-drop area or preview */}
        <Form.Item label={t("category.screen.thumbnailLabel")}>
          {!thumbnailPath ? (
            // Drag-drop area when no image selected - with OS-level drop zone
            <CompactUpload
              onSelect={handleBrowseThumbnail}
              onDrop={handleThumbnailDrop}
              enabled={!thumbnailPath}
              title={t("category.screen.thumbnailUploadTitle")}
              subtitle={t("category.screen.thumbnailUploadSubtitle")}
            />
          ) : (
            // Image preview when selected
            <div className="category-screen-thumbnail-preview">
              <img
                src={toAppUrl(thumbnailPath) || undefined}
                alt={t("category.screen.thumbnailPreview")}
                className="category-screen-thumbnail-image"
                onError={(e) => {
                  (e.target as HTMLImageElement).src =
                    "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
                }}
              />
            </div>
          )}

          {/* Image info and actions - outside dropzone */}
          {thumbnailPath && (
            <div className="category-screen-thumbnail-info">
              <div className="category-screen-thumbnail-filename">
                {thumbnailFileName}
              </div>
              <Space size={4}>
                <CompactButton
                  icon={<FolderOpenOutlined />}
                  onClick={handleBrowseThumbnail}
                  size="small"
                >
                  {t("category.screen.changeButton")}
                </CompactButton>
                <CompactButton
                  danger
                  onClick={() => {
                    setThumbnailPath(undefined);
                    setThumbnailFileName(undefined);
                  }}
                  size="small"
                >
                  {t("category.screen.removeButton")}
                </CompactButton>
              </Space>
            </div>
          )}
        </Form.Item>
      </Form>

      <div className="slide-in-screen-footer">
        <Space>
          <CompactButton onClick={handleCancel} size="large">
            {t("category.screen.cancelButton")}
          </CompactButton>
          <CompactPrimaryButton
            onClick={handleSubmit}
            loading={loading}
            size="large"
          >
            {t("category.screen.saveButton")}
          </CompactPrimaryButton>
        </Space>
      </div>
    </div>
  );
};

/**
 * Hook to open Category screen
 */
export function useCategoryScreen() {
  const { openScreen } = useSlideInScreenContext();
  const { t } = useTranslation();

  const openCategoryScreen = (props: CategoryScreenProps) => {
    // Create a wrapper that will receive the screenId
    let actualScreenId = "";

    const ContentWrapper = () => (
      <CategoryScreenContent {...props} screenId={actualScreenId} />
    );

    const title = props.editNode
      ? t("category.screen.title.edit")
      : t("category.screen.title.add");

    actualScreenId = openScreen({
      title,
      width: "50%",
      content: <ContentWrapper />,
    });

    return actualScreenId;
  };

  return { openCategoryScreen };
}
