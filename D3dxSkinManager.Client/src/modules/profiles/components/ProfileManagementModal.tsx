import React, { useState } from 'react';
import { Modal, Table, Button, Input, Form, Space, Popconfirm, message } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, CopyOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { Profile } from '../../../shared/types/profile.types';

interface ProfileManagementModalProps {
  visible: boolean;
  onClose: () => void;
}

/**
 * Profile management modal
 * Allows creating, editing, deleting, and switching profiles
 */
export const ProfileManagementModal: React.FC<ProfileManagementModalProps> = ({ visible, onClose }) => {
  const { state, actions } = useProfile();
  const [createModalVisible, setCreateModalVisible] = useState(false);
  const [editingProfile, setEditingProfile] = useState<Profile | null>(null);
  const [form] = Form.useForm();

  const handleCreate = async (values: { name: string; description?: string }) => {
    try {
      await actions.createProfile(values.name, values.description);
      message.success('Profile created successfully');
      setCreateModalVisible(false);
      form.resetFields();
    } catch (error) {
      message.error('Failed to create profile');
    }
  };

  const handleEdit = async (values: { name: string; description?: string }) => {
    if (!editingProfile) return;

    try {
      await actions.updateProfile(editingProfile.id, values.name, values.description);
      message.success('Profile updated successfully');
      setEditingProfile(null);
      form.resetFields();
    } catch (error) {
      message.error('Failed to update profile');
    }
  };

  const handleDelete = async (profileId: string) => {
    try {
      await actions.deleteProfile(profileId);
      message.success('Profile deleted successfully');
    } catch (error) {
      message.error('Failed to delete profile');
    }
  };

  const handleSwitch = async (profileId: string) => {
    try {
      await actions.selectProfile(profileId);
      message.success('Profile switched successfully');
    } catch (error) {
      message.error('Failed to switch profile');
    }
  };

  const columns = [
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
      render: (text: string, record: Profile) => (
        <Space>
          {text}
          {record.isActive && <span style={{ color: '#52c41a' }}>(Active)</span>}
        </Space>
      ),
    },
    {
      title: 'Description',
      dataIndex: 'description',
      key: 'description',
    },
    {
      title: 'Mods',
      dataIndex: 'modCount',
      key: 'modCount',
      width: 80,
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
      render: (date: string) => new Date(date).toLocaleDateString(),
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 200,
      render: (_: any, record: Profile) => (
        <Space>
          {!record.isActive && (
            <Button
              size="small"
              type="primary"
              onClick={() => handleSwitch(record.id)}
            >
              Switch
            </Button>
          )}
          <Button
            size="small"
            icon={<EditOutlined />}
            onClick={() => {
              setEditingProfile(record);
              form.setFieldsValue({
                name: record.name,
                description: record.description,
              });
            }}
          />
          {!record.isActive && (
            <Popconfirm
              title="Delete profile?"
              description="This will permanently delete all profile data."
              onConfirm={() => handleDelete(record.id)}
              okText="Yes"
              cancelText="No"
            >
              <Button size="small" danger icon={<DeleteOutlined />} />
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  return (
    <>
      <Modal
        title="Profile Management"
        open={visible}
        onCancel={onClose}
        width={900}
        footer={[
          <Button key="close" onClick={onClose}>
            Close
          </Button>,
        ]}
      >
        <Space orientation="vertical" style={{ width: '100%' }} size="large">
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => setCreateModalVisible(true)}
          >
            Create New Profile
          </Button>

          <Table
            dataSource={state.profiles}
            columns={columns}
            rowKey="id"
            loading={state.loading}
            pagination={false}
            size="small"
          />
        </Space>
      </Modal>

      {/* Create Profile Modal */}
      <Modal
        title="Create New Profile"
        open={createModalVisible}
        onCancel={() => {
          setCreateModalVisible(false);
          form.resetFields();
        }}
        onOk={() => form.submit()}
        okText="Create"
      >
        <Form form={form} layout="vertical" onFinish={handleCreate}>
          <Form.Item
            name="name"
            label="Profile Name"
            rules={[{ required: true, message: 'Please enter profile name' }]}
          >
            <Input placeholder="e.g., Gaming, Work, Testing" />
          </Form.Item>
          <Form.Item name="description" label="Description">
            <Input.TextArea rows={3} placeholder="Optional description..." />
          </Form.Item>
        </Form>
      </Modal>

      {/* Edit Profile Modal */}
      <Modal
        title="Edit Profile"
        open={!!editingProfile}
        onCancel={() => {
          setEditingProfile(null);
          form.resetFields();
        }}
        onOk={() => form.submit()}
        okText="Save"
      >
        <Form form={form} layout="vertical" onFinish={handleEdit}>
          <Form.Item
            name="name"
            label="Profile Name"
            rules={[{ required: true, message: 'Please enter profile name' }]}
          >
            <Input />
          </Form.Item>
          <Form.Item name="description" label="Description">
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
};
