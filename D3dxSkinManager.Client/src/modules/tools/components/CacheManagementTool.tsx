import { notification } from '../../../shared/utils/notification';
import React, { useState, useMemo } from 'react';
import { Card,  Table, Tag, Statistic, Row, Col } from 'antd';
import {
  DeleteOutlined,
  ClearOutlined,
  FolderOpenOutlined,
  ReloadOutlined,
  WarningOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons';
import { cacheService, CacheItem, CacheCategory, CacheStatistics } from '../services/cacheService';
import {
  CompactCard,
  CompactSpace,
  CompactDivider,
  CompactAlert,
  CompactSection,
  CompactButton,
} from '../../../shared/components/compact';
import { ConfirmDialog } from '../../../shared/components/dialogs';
import { useProfile } from '../../../shared/context/ProfileContext';
import { useTranslation } from 'react-i18next';
import './CacheManagementTool.css';

/**
 * CacheManagementTool - Manage mod cache files
 *
 * Features:
 * - Scan cache directories
 * - View cache statistics by category (Invalid, Rarely Used, Frequently Used)
 * - Clean cache by category
 * - Browse and delete individual cache items
 */
export const CacheManagementTool: React.FC = () => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [cacheItems, setCacheItems] = useState<CacheItem[]>([]);
  const [cacheStats, setCacheStats] = useState<CacheStatistics>();
  const { state: profileState } = useProfile();
  const [cleanConfirm, setCleanConfirm] = useState<{ visible: boolean; category?: CacheCategory; categoryName?: string }>({ visible: false });
  const [deleteItemConfirm, setDeleteItemConfirm] = useState<{ visible: boolean; item?: CacheItem }>({ visible: false });

  // Categorize cache items by category
  const categorizedCache = useMemo(() => {
    const invalid = cacheItems.filter(item => item.category === CacheCategory.Invalid);
    const rarelyUsed = cacheItems.filter(item => item.category === CacheCategory.RarelyUsed);
    const frequentlyUsed = cacheItems.filter(item => item.category === CacheCategory.FrequentlyUsed);

    return { invalid, rarelyUsed, frequentlyUsed };
  }, [cacheItems]);

  /**
   * Scan cache directories
   */
  const handleScanCache = async () => {
    try {
      setLoading(true);
      const [items, stats] = await Promise.all([
        cacheService.scanCache(),
        cacheService.getStatistics()
      ]);
      setCacheItems(items);
      setCacheStats(stats);
      notification.success(`Cache scan complete: ${items.length} items found`);
    } catch (error) {
      notification.error('Failed to scan cache');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  /**
   * Show clean cache confirmation dialog
   */
  const handleCleanCache = (category: CacheCategory, categoryName: string) => {
    const count = categorizedCache[
      category === CacheCategory.Invalid ? 'invalid' :
      category === CacheCategory.RarelyUsed ? 'rarelyUsed' :
      'frequentlyUsed'
    ].length;

    if (count === 0) {
      notification.info(`No ${categoryName} cache items to clean`);
      return;
    }

    setCleanConfirm({ visible: true, category, categoryName });
  };

  /**
   * Execute clean cache after confirmation
   */
  const handleConfirmCleanCache = async () => {
    const { category, categoryName } = cleanConfirm;
    if (!category || !categoryName || !profileState.selectedProfile?.id) {
      notification.error('No profile selected. Cannot clean cache.');
      setCleanConfirm({ visible: false });
      return;
    }

    const profileId = profileState.selectedProfile.id;
    try {
      setLoading(true);
      const deletedCount = await cacheService.cleanCache(profileId, category);
      notification.success(`${deletedCount} ${categoryName} cache item(s) deleted`);

      // Refresh cache list
      await handleScanCache();
    } catch (error) {
      notification.error(`Failed to clean ${categoryName} cache`);
      console.error(error);
    } finally {
      setLoading(false);
      setCleanConfirm({ visible: false });
    }
  };

  /**
   * Show delete cache item confirmation dialog
   */
  const handleDeleteCacheItem = (item: CacheItem) => {
    setDeleteItemConfirm({ visible: true, item });
  };

  /**
   * Execute delete cache item after confirmation
   */
  const handleConfirmDeleteCacheItem = async () => {
    const item = deleteItemConfirm.item;
    if (!item || !profileState.selectedProfile?.id) {
      notification.error('No profile selected. Cannot delete cache item.');
      setDeleteItemConfirm({ visible: false });
      return;
    }

    const profileId = profileState.selectedProfile.id;
    try {
      setLoading(true);
      const success = await cacheService.deleteCacheItem(profileId, item.sha);
      if (success) {
        notification.success('Cache item deleted');
        // Refresh cache list
        await handleScanCache();
      } else {
        notification.error('Failed to delete cache item');
      }
    } catch (error) {
      notification.error('Failed to delete cache item');
      console.error(error);
    } finally {
      setLoading(false);
      setDeleteItemConfirm({ visible: false });
    }
  };

  /**
   * Open cache directory in file explorer
   */
  const handleOpenCacheDirectory = async () => {
    try {
      // Assuming work_mods is the cache directory
      // This would need to be configured based on actual data path
      notification.info('Opening cache directory...');
      // await fileSystemService.openDirectory(cachePath);
    } catch (error) {
      notification.error('Failed to open cache directory');
      console.error(error);
    }
  };

  /**
   * Get category badge
   */
  const getCategoryBadge = (category: CacheCategory) => {
    switch (category) {
      case CacheCategory.Invalid:
        return <Tag color="error" icon={<ExclamationCircleOutlined />}>Invalid</Tag>;
      case CacheCategory.RarelyUsed:
        return <Tag color="warning" icon={<WarningOutlined />}>Rarely Used</Tag>;
      case CacheCategory.FrequentlyUsed:
        return <Tag color="success" icon={<CheckCircleOutlined />}>Frequently Used</Tag>;
      default:
        return <Tag>Unknown</Tag>;
    }
  };

  const cacheColumns = [
    {
      title: 'Category',
      key: 'category',
      width: 150,
      render: (_: any, record: CacheItem) => getCategoryBadge(record.category),
    },
    {
      title: 'SHA',
      dataIndex: 'sha',
      key: 'sha',
      width: 180,
      ellipsis: true,
    },
    {
      title: 'Path',
      dataIndex: 'path',
      key: 'path',
      ellipsis: true,
    },
    {
      title: 'Size',
      key: 'size',
      width: 120,
      render: (_: any, record: CacheItem) => cacheService.formatBytes(record.sizeBytes),
    },
    {
      title: 'Last Modified',
      dataIndex: 'lastModified',
      key: 'lastModified',
      width: 180,
    },
    {
      title: 'Action',
      key: 'action',
      width: 100,
      render: (_: any, record: CacheItem) => (
        <CompactButton
          type="link"
          danger
          size="small"
          icon={<DeleteOutlined />}
          onClick={() => handleDeleteCacheItem(record)}
        >
          Delete
        </CompactButton>
      ),
    },
  ];

  return (
    <CompactCard
      title={<><DeleteOutlined /> Cache Management</>}
    >
      <CompactSpace vertical className="cache-management-container">

        {/* Scan Cache Section */}
        <CompactSpace className="cache-management-scan-section">
          <CompactButton
            type="primary"
            icon={<ReloadOutlined />}
            onClick={handleScanCache}
            loading={loading}
          >
            Scan Cache
          </CompactButton>
          <CompactButton
            icon={<FolderOpenOutlined />}
            onClick={handleOpenCacheDirectory}
          >
            Open Cache Directory
          </CompactButton>
        </CompactSpace>

        {/* Cache Statistics */}
        {cacheStats && (
          <>
            <CompactAlert
              message="Cache Overview"
              description={`Total: ${cacheStats.totalCount} items (${cacheService.formatBytesToMiB(cacheStats.totalSizeBytes)})`}
              type="info"
              showIcon
            />

            <Row gutter={16}>
              <Col span={8}>
                <Card size="small">
                  <Statistic
                    title="Invalid Cache"
                    value={cacheStats.invalidCount}
                    suffix={`items (${cacheService.formatBytesToMiB(cacheStats.invalidSizeBytes)})`}
                    prefix={<ExclamationCircleOutlined className="cache-stat-icon-error" />}
                    valueStyle={{ fontSize: '16px' }}
                  />
                  <CompactButton
                    type="primary"
                    size="small"
                    icon={<ClearOutlined />}
                    onClick={() => handleCleanCache(CacheCategory.Invalid, 'Invalid')}
                    loading={loading}
                    disabled={cacheStats.invalidCount === 0}
                    className="cache-clean-button"
                  >
                    Clean Invalid
                  </CompactButton>
                </Card>
              </Col>

              <Col span={8}>
                <Card size="small">
                  <Statistic
                    title="Rarely Used Cache"
                    value={cacheStats.rarelyUsedCount}
                    suffix={`items (${cacheService.formatBytesToMiB(cacheStats.rarelyUsedSizeBytes)})`}
                    prefix={<WarningOutlined className="cache-stat-icon-warning" />}
                    valueStyle={{ fontSize: '16px' }}
                  />
                  <CompactButton
                    size="small"
                    icon={<ClearOutlined />}
                    onClick={() => handleCleanCache(CacheCategory.RarelyUsed, 'Rarely Used')}
                    loading={loading}
                    disabled={cacheStats.rarelyUsedCount === 0}
                    className="cache-clean-button"
                  >
                    Clean Rarely Used
                  </CompactButton>
                </Card>
              </Col>

              <Col span={8}>
                <Card size="small">
                  <Statistic
                    title="Frequently Used Cache"
                    value={cacheStats.frequentlyUsedCount}
                    suffix={`items (${cacheService.formatBytesToMiB(cacheStats.frequentlyUsedSizeBytes)})`}
                    prefix={<CheckCircleOutlined className="cache-stat-icon-success" />}
                    valueStyle={{ fontSize: '16px' }}
                  />
                  <CompactButton
                    danger
                    size="small"
                    icon={<ClearOutlined />}
                    onClick={() => handleCleanCache(CacheCategory.FrequentlyUsed, 'Frequently Used')}
                    loading={loading}
                    disabled={cacheStats.frequentlyUsedCount === 0}
                    className="cache-clean-button"
                  >
                    Clean Frequently Used
                  </CompactButton>
                </Card>
              </Col>
            </Row>

            <CompactAlert
              message="Cache Categories Explained"
              description={
                <ul className="cache-description-list">
                  <li><strong>Invalid Cache:</strong> Cache with no matching mod in database. Safe to delete.</li>
                  <li><strong>Rarely Used Cache:</strong> Cache for mods that exist but are not loaded. Can be deleted if space is needed.</li>
                  <li><strong>Frequently Used Cache:</strong> Cache for currently loaded mods. Deleting may require re-importing them.</li>
                </ul>
              }
              type="warning"
              showIcon
            />
          </>
        )}

        <CompactDivider />

        {/* Cache Browser Table */}
        <CompactSection title="Cache Browser">
          <Table
            columns={cacheColumns}
            dataSource={cacheItems}
            rowKey="sha"
            size="small"
            pagination={{ pageSize: 10 }}
            loading={loading}
          />
        </CompactSection>
      </CompactSpace>

      {/* Clean Cache Confirmation Dialog */}
      <ConfirmDialog
        visible={cleanConfirm.visible}
        title={`Clean ${cleanConfirm.categoryName} Cache?`}
        content={`This will delete ${categorizedCache[
          cleanConfirm.category === CacheCategory.Invalid ? 'invalid' :
          cleanConfirm.category === CacheCategory.RarelyUsed ? 'rarelyUsed' :
          'frequentlyUsed'
        ]?.length || 0} cache item(s). ${
          cleanConfirm.category === CacheCategory.FrequentlyUsed
            ? 'WARNING: These are caches for currently loaded mods. Deleting may require re-importing them.'
            : ''
        }`}
        okText="Delete"
        cancelText="Cancel"
        okType={cleanConfirm.category === CacheCategory.FrequentlyUsed ? 'danger' : 'primary'}
        onOk={handleConfirmCleanCache}
        onCancel={() => setCleanConfirm({ visible: false })}
      />

      {/* Delete Cache Item Confirmation Dialog */}
      <ConfirmDialog
        visible={deleteItemConfirm.visible}
        title="Delete cache item?"
        content={`Delete cache for SHA: ${deleteItemConfirm.item?.sha || ''}? (${cacheService.formatBytes(deleteItemConfirm.item?.sizeBytes || 0)})`}
        okText="Delete"
        cancelText="Cancel"
        okType="danger"
        onOk={handleConfirmDeleteCacheItem}
        onCancel={() => setDeleteItemConfirm({ visible: false })}
      />
    </CompactCard>
  );
};
