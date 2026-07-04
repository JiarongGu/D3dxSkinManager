import React, { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { CompactSelect } from './compact';
import type { CompactSelectSize } from './compact';
import type { CategoryInfo } from '../types/category.types';
import { flattenCategoryOptions } from '../utils/categoryTree';

interface CategorySelectProps {
  categories: CategoryInfo[];
  value?: string;
  onChange: (id?: string) => void;
  /** Placeholder when nothing selected — defaults to i18n "All Categories" */
  placeholder?: string;
  size?: CompactSelectSize;
  className?: string;
  style?: React.CSSProperties;
}

/**
 * Shared category selector — flat dropdown with breadcrumb labels.
 * Used in ModAnalyzerTool, ModPackageTool export/import, etc.
 */
export const CategorySelect: React.FC<CategorySelectProps> = ({
  categories, value, onChange, placeholder, size, className, style,
}) => {
  const { t } = useTranslation();
  const ALL = '__all__';

  const options = useMemo(() => [
    { value: ALL, label: placeholder || t('tools.modAnalyzer.allCategories') },
    ...flattenCategoryOptions(categories),
  ], [categories, placeholder, t]);

  return (
    <CompactSelect
      value={value || ALL}
      onChange={(v: string) => onChange(v === ALL ? undefined : v)}
      options={options}
      size={size}
      className={className}
      style={style}
      popupMatchSelectWidth={false}
      showSearch={{
        optionFilterProp: "label"
      }}
    />
  );
};
