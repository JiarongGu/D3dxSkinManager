import React, { useState, useMemo } from "react";
import { Input, Tag as AntTag, Form, Pagination, ColorPicker } from "antd";
import {
  SearchOutlined,
  DeleteOutlined,
  TagsOutlined,
  ReloadOutlined,
  PlusOutlined,
  EditOutlined,
} from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { ConfirmDialog } from "../../../../shared/components/dialogs/ConfirmDialog";
import { FormDialog } from "../../../../shared/components/dialogs/FormDialog";
import {
  CompactSpace,
  CompactButton,
  CompactAlert
} from "../../../../shared/components/compact";
import { DataTable, ColumnsType } from "../../../../shared/components/common";
import { Tag } from "../../../../shared/types/mod.types";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { useTagManagement } from "../../../../shared/hooks/useTagManagement";
import { notification } from "../../../../shared/utils/notification";
import { formatDateTime } from "../../../../shared/utils/formatDate";
import "./TagManagementTool.css";

const { Search } = Input;

interface TagFormValues {
  name: string;
  color: string;
}

/**
 * Tag Management Tool
 * Features:
 * - Create new tags
 * - Edit tag names and colors
 * - Delete tags
 * - Search/filter tags
 */
export const TagManagementTool: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [searchTerm, setSearchTerm] = useState("");
  const [tagToDelete, setTagToDelete] = useState<string | null>(null);
  const [editingTag, setEditingTag] = useState<Tag | null>(null);
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [form] = Form.useForm<TagFormValues>();
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const { allTags, loading, loadTags, upsertTag, deleteTag } = useTagManagement(selectedProfileId);

  // Load all tags on mount
  React.useEffect(() => {
    if (selectedProfileId) {
      void loadTags();
    }
  }, [selectedProfileId, loadTags]);

  // Filter tags based on search term
  const filteredTags = useMemo(() => {
    if (!searchTerm) return allTags;
    const lowerSearch = searchTerm.toLowerCase();
    return allTags.filter((tag) =>
      tag.name.toLowerCase().includes(lowerSearch),
    );
  }, [allTags, searchTerm]);

  // Paginated tags
  const paginatedTags = useMemo(() => {
    const startIndex = (currentPage - 1) * pageSize;
    const endIndex = startIndex + pageSize;
    return filteredTags.slice(startIndex, endIndex);
  }, [filteredTags, currentPage, pageSize]);

  // Reset to page 1 when search changes
  React.useEffect(() => {
    setCurrentPage(1);
  }, [searchTerm]);

  const handleCreateTag = async (values: TagFormValues) => {
    if (!selectedProfileId) return;

    // Check for duplicates
    if (allTags.some((t) => t.name.toLowerCase() === values.name.toLowerCase())) {
      notification.error(`Tag "${values.name}" already exists`);
      return;
    }

    try {
      await upsertTag(values.name, values.color);
      await loadTags();
      notification.success(`Tag "${values.name}" created`);
      setShowCreateDialog(false);
      form.resetFields();
    } catch (error: unknown) {
      notification.error(t('tags.error.createFailed'));
    }
  };

  const handleEditTag = async (values: TagFormValues) => {
    if (!selectedProfileId || !editingTag) return;

    // Check for duplicates (excluding current tag)
    if (
      values.name !== editingTag.name &&
      allTags.some((t) => t.name.toLowerCase() === values.name.toLowerCase())
    ) {
      notification.error(`Tag "${values.name}" already exists`);
      return;
    }

    try {
      // If name changed, delete old and create new
      if (values.name !== editingTag.name) {
        await deleteTag(editingTag.name);
      }
      await upsertTag(values.name, values.color);
      await loadTags();
      notification.success(`Tag "${editingTag.name}" updated`);
      setEditingTag(null);
      form.resetFields();
    } catch (error: unknown) {
      notification.error(t('tags.error.updateFailed'));
    }
  };

  const handleDeleteTag = async () => {
    if (!selectedProfileId || !tagToDelete) return;

    try {
      await deleteTag(tagToDelete);
      await loadTags();
      notification.success(`Tag "${tagToDelete}" deleted`);
      setTagToDelete(null);
    } catch (error: unknown) {
      notification.error(t('tags.error.deleteFailed'));
      setTagToDelete(null);
    }
  };

  const openCreateDialog = () => {
    form.resetFields();
    form.setFieldsValue({ color: "#1890ff" });
    setShowCreateDialog(true);
  };

  const openEditDialog = (tag: Tag) => {
    form.setFieldsValue({ name: tag.name, color: tag.color });
    setEditingTag(tag);
  };

  const columns: ColumnsType<Tag> = [
    {
      title: 'Tag',
      key: 'tag',
      width: 200,
      render: (_: any, record: Tag) => (
        <AntTag color={record.color} className="tag-chip">
          {record.name}
        </AntTag>
      ),
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
      render: (date: string) => formatDateTime(date),
    },
    {
      title: 'Updated',
      dataIndex: 'updatedAt',
      key: 'updatedAt',
      width: 180,
      render: (date: string) => formatDateTime(date),
    },
    {
      title: 'Action',
      key: 'action',
      width: 150,
      render: (_: any, record: Tag) => (
        <CompactSpace size="small">
          <CompactButton.Primary
            size="small"
            icon={<EditOutlined />}
            onClick={() => openEditDialog(record)}
          >
            {t("common.edit")}
          </CompactButton.Primary>
          <CompactButton.Danger
            danger
            size="small"
            icon={<DeleteOutlined />}
            onClick={() => setTagToDelete(record.name)}
          >
            {t("common.delete")}
          </CompactButton.Danger>
        </CompactSpace>
      ),
    },
  ];

  return (
    <div className="tag-management-tool-container">
      {/* Header with Search and Actions */}
      <div className="tag-management-tool-header">
        <Search
          placeholder={t('tags.searchPlaceholder')}
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          prefix={<SearchOutlined />}
          allowClear
          className="tag-management-tool-search"
        />
        <CompactSpace size="small">
          <CompactButton
            type="primary"
            icon={<PlusOutlined />}
            onClick={openCreateDialog}
          >
            Create Tag
          </CompactButton>
          <CompactButton
            className="tag-management-tool-reload-button"
            icon={<ReloadOutlined />}
            onClick={() => void loadTags()}
            loading={loading}
          />
        </CompactSpace>
      </div>

      {/* Tags Table */}
      <div className="tag-management-tool-table-container">
        <DataTable
          columns={columns}
          dataSource={paginatedTags}
          rowKey="name"
          compact
          loading={loading}
          locale={{
            emptyText: searchTerm ? t('tags.noTagsFound') : t('tags.noTagsAvailable')
          }}
          pagination={false}
        />
      </div>

      {/* Pagination */}
      {filteredTags.length > 0 && (
        <div className="tag-management-tool-pagination">
          <Pagination
            current={currentPage}
            pageSize={pageSize}
            total={filteredTags.length}
            onChange={(page, newPageSize) => {
              setCurrentPage(page);
              if (newPageSize !== pageSize) {
                setPageSize(newPageSize);
                setCurrentPage(1);
              }
            }}
            showSizeChanger
            showTotal={(total) => t("common.table.total", { count: total })}
            pageSizeOptions={['10', '20', '50', '100']}
          />
        </div>
      )}

      {/* Create Tag Dialog */}
      <FormDialog
        visible={showCreateDialog}
        title={t('tags.createTitle')}
        onOk={() => form.submit()}
        onCancel={() => {
          setShowCreateDialog(false);
          form.resetFields();
        }}
        okText={t('common.create')}
        cancelText={t("common.cancel")}
        destroyOnHidden
      >
        <Form form={form} onFinish={handleCreateTag} layout="vertical">
          <Form.Item
            label={t('tags.tagName')}
            name="name"
            rules={[
              { required: true, message: t('tags.enterTagName') },
              { max: 50, message: t('tags.tagNameMaxLength') },
            ]}
          >
            <Input placeholder={t('tags.namePlaceholder')} />
          </Form.Item>
          <Form.Item
            label={t('tags.color')}
            name="color"
            rules={[{ required: true, message: t('tags.selectColor') }]}
          >
            <ColorPicker showText />
          </Form.Item>
        </Form>
      </FormDialog>

      {/* Edit Tag Dialog */}
      <FormDialog
        visible={editingTag !== null}
        title={t('tags.editTitle')}
        onOk={() => form.submit()}
        onCancel={() => {
          setEditingTag(null);
          form.resetFields();
        }}
        okText={t('common.update')}
        cancelText={t("common.cancel")}
        destroyOnHidden
      >
        <Form form={form} onFinish={handleEditTag} layout="vertical">
          <Form.Item
            label={t('tags.tagName')}
            name="name"
            rules={[
              { required: true, message: t('tags.enterTagName') },
              { max: 50, message: t('tags.tagNameMaxLength') },
            ]}
          >
            <Input placeholder={t('tags.namePlaceholder')} />
          </Form.Item>
          <Form.Item
            label={t('tags.color')}
            name="color"
            rules={[{ required: true, message: t('tags.selectColor') }]}
          >
            <ColorPicker showText />
          </Form.Item>
        </Form>
      </FormDialog>

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        visible={tagToDelete !== null}
        title={t("tags.deleteTag")}
        content={
          <>
            <p>{t("tags.deleteTagConfirm", { tagName: tagToDelete })}</p>
            <p
              style={{
                fontSize: "12px",
                color: "var(--text-secondary)",
                marginTop: "8px",
              }}
            >
              {t("tags.deleteTagNote")}
            </p>
          </>
        }
        okText={t("common.delete")}
        cancelText={t("common.cancel")}
        okType="danger"
        onOk={handleDeleteTag}
        onCancel={() => setTagToDelete(null)}
      />
    </div>
  );
};
