import React, { useEffect } from 'react';
import { Tabs } from 'antd';
import { useTranslation } from 'react-i18next';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { ModPackageProvider, useModPackage } from './context/ModPackageContext';
import { ExportTab } from './components/ExportTab';
import { ImportTab } from './components/ImportTab';
import './ModPackageTool.css';

interface ModPackageToolProps {
  visible: boolean;
  onClose: () => void;
  initialCategoryId?: string;
}

export const ModPackageTool: React.FC<ModPackageToolProps> = ({ visible, onClose, initialCategoryId }) => {
  const { t } = useTranslation();

  const content = (
    <ModPackageProvider initialCategoryId={initialCategoryId}>
      <ModPackageToolInner />
    </ModPackageProvider>
  );

  useSlideInScreen({
    visible,
    title: t('tools.modPackage.title'),
    content,
    width: '85%',
    onClose,
  });

  return null;
};

const ModPackageToolInner: React.FC = () => {
  const { t } = useTranslation();
  const { loadModsAndCategories } = useModPackage();

  useEffect(() => {
    void loadModsAndCategories();
  }, [loadModsAndCategories]);

  const items = [
    {
      key: 'export',
      label: t('tools.modPackage.export.title'),
      children: <ExportTab />,
    },
    {
      key: 'import',
      label: t('tools.modPackage.import.title'),
      children: <ImportTab />,
    },
  ];

  return (
    <div className="mod-transfer">
      <Tabs
        items={items}
        tabPlacement="start"
        className="mod-transfer__tabs"
      />
    </div>
  );
};
