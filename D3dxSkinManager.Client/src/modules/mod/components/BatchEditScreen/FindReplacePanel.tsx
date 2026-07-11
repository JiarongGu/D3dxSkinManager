import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Space } from 'antd';
import { SearchOutlined, CloseOutlined, SwapOutlined, EnterOutlined } from '@ant-design/icons';
import { debounce } from 'lodash-es';
import { useTranslation } from 'react-i18next';
import './FindReplacePanel.css';
import { CompactInput, CompactIconButton, CompactCheckbox } from '../../../../shared/components/compact';

interface FindReplacePanelProps {
  visible: boolean;
  onClose: () => void;
  onReplace: (config: ReplaceConfig) => void;
  onSearchChange?: (config: ReplaceConfig | null) => void;
  columns: Array<{ label: string; value: string }>;
}

export interface ReplaceConfig {
  find: string;
  replace: string;
  useRegex: boolean;
  caseSensitive: boolean;
}

export const FindReplacePanel: React.FC<FindReplacePanelProps> = ({
  visible,
  onClose,
  onReplace,
  onSearchChange,
  columns,
}) => {
  const { t } = useTranslation();
  const [config, setConfig] = useState<ReplaceConfig>({
    find: '',
    replace: '',
    useRegex: false,
    caseSensitive: false,
  });

  const [error, setError] = useState<string>('');
  const findInputRef = useRef<any>(null);

  // Debounced search change callback
  const debouncedSearchChange = useCallback(
    debounce((searchConfig: ReplaceConfig | null) => {
      if (onSearchChange) {
        onSearchChange(searchConfig);
      }
    }, 300),
    [onSearchChange]
  );

  // Focus find input when panel opens
  useEffect(() => {
    if (visible && findInputRef.current) {
      setTimeout(() => {
        findInputRef.current?.focus();
      }, 100);
    }
  }, [visible]);

  // Clean up debounce on unmount
  useEffect(() => {
    return () => {
      debouncedSearchChange.cancel();
    };
  }, [debouncedSearchChange]);

  const validateRegex = (pattern: string): boolean => {
    if (!config.useRegex) return true;
    try {
      new RegExp(pattern);
      return true;
    } catch (e) {
      return false;
    }
  };

  const handleReplaceAll = () => {
    setError('');

    if (!config.find) {
      setError(t('mods.batchEdit.findReplace.errorEnterText'));
      return;
    }

    if (config.useRegex && !validateRegex(config.find)) {
      setError(t('mods.batchEdit.findReplace.errorInvalidRegex'));
      return;
    }

    onReplace(config);
    // Don't close panel after replace, user might want to do more
  };

  const handleFindChange = (value: string) => {
    const newConfig = { ...config, find: value };
    setConfig(newConfig);
    setError('');
    // Use debounced callback for search highlighting
    debouncedSearchChange(value ? newConfig : null);
  };

  // Notify on config changes for highlighting (debounced)
  useEffect(() => {
    if (config.find) {
      debouncedSearchChange(config);
    } else {
      // Clear immediately when find is empty
      debouncedSearchChange.cancel();
      if (onSearchChange) {
        onSearchChange(null);
      }
    }
  }, [config.caseSensitive, config.useRegex, config.find]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Escape') {
      onClose();
    } else if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      handleReplaceAll();
    }
  };

  if (!visible) return null;

  return (
    <div className="find-replace-panel" onKeyDown={handleKeyDown}>
      <div className="find-replace-row">
        <div className="find-replace-input-group">
          <SearchOutlined className="find-replace-icon" />
          <CompactInput
            ref={findInputRef}
            value={config.find}
            onChange={(e) => handleFindChange(e.target.value)}
            placeholder={t('mods.batchEdit.findReplace.findPlaceholder')}
            size="small"
            className="find-replace-input"
            status={error && config.find ? 'error' : undefined}
            variant='borderless'
          />
          <CompactCheckbox
            checked={config.caseSensitive}
            onChange={(e) => setConfig({ ...config, caseSensitive: e.target.checked })}
            className="find-replace-checkbox-inline"
            title={t('mods.batchEdit.findReplace.matchCase')}
          >
            Aa
          </CompactCheckbox>
          <CompactCheckbox
            checked={config.useRegex}
            onChange={(e) => setConfig({ ...config, useRegex: e.target.checked })}
            className="find-replace-checkbox-inline"
            title={t('mods.batchEdit.findReplace.useRegex')}
          >
            .*
          </CompactCheckbox>
        </div>

        <div className="find-replace-actions">
          <CompactIconButton
            size={24}
            icon={<CloseOutlined />}
            onClick={onClose}
            className="find-replace-close"
            title={t('mods.batchEdit.findReplace.close')}
          />
        </div>
      </div>

      <div className="find-replace-row">
        <div className="find-replace-input-group">
          <SwapOutlined className="find-replace-icon" />
          <CompactInput
            value={config.replace}
            onChange={(e) => setConfig({ ...config, replace: e.target.value })}
            placeholder={t('mods.batchEdit.findReplace.replacePlaceholder')}
            size="small"
            className="find-replace-input"
            onPressEnter={(e) => {
              if (e.ctrlKey || e.metaKey) handleReplaceAll();
            }}
            variant='borderless'
          />
        </div>

        <div className="find-replace-actions">
          <CompactIconButton
            size={24}
            icon={<EnterOutlined />}
            onClick={handleReplaceAll}
            className="find-replace-icon-button"
            title={t('mods.batchEdit.findReplace.replaceAll')}
          />
        </div>
      </div>

      {error && (
        <div className="find-replace-error">{error}</div>
      )}

      {config.useRegex && !error && (
        <div
          className="find-replace-hint"
          dangerouslySetInnerHTML={{ __html: t('mods.batchEdit.findReplace.regexExamples') }}
        />
      )}
    </div>
  );
};
