import { notification } from '../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Form, Select } from 'antd';
import {
  SettingOutlined,
  InfoCircleOutlined,
} from '@ant-design/icons';
import { CompactCard, CompactSpace } from '../../../shared/components/compact';
import { useAnnotation, getAnnotationLevelLabel, getAnnotationLevelDescription, AnnotationLevel } from '../../../shared/components/common/TooltipSystem';
import { useTheme, ThemeMode } from '../../../shared/context/ThemeContext';
import { useTranslation } from 'react-i18next';
import { changeLanguage } from '../../../i18n/i18n';
import { AVAILABLE_LANGUAGES } from '../../../shared/types/language.types';
import { logger, Logger, LogLevelName } from '../../core/utils/logger';
import { settingsService } from '../services/settingsService';
import { useProfile } from '../../../shared/context/ProfileContext';

const { Option } = Select;

export const SettingsView: React.FC = () => {
  const [form] = Form.useForm();
  const { annotationLevel, setAnnotationLevel } = useAnnotation();
  const { theme, setTheme } = useTheme();
  const { t, i18n } = useTranslation();
  const { selectedProfile, selectedProfileId } = useProfile();
  const [selectedAnnotationLevel, setSelectedAnnotationLevel] = useState<AnnotationLevel>(annotationLevel);
  const [logLevel, setLogLevel] = useState<LogLevelName>(logger.getCurrentLevelName());
  const [thumbnailAlgorithm, setThumbnailAlgorithm] = useState<string>('similarity-threshold');

  // Load thumbnail algorithm from profile config
  useEffect(() => {
    const loadThumbnailAlgorithm = async () => {
      if (!selectedProfileId) {
        console.log('[SettingsView] No profile selected, skipping thumbnail algorithm load');
        return;
      }

      try {
        const { getActiveProfileConfig } = await import('../../profiles/services/profileConfigService');
        const config = await getActiveProfileConfig(selectedProfileId);
        if (config?.thumbnailAlgorithm) {
          setThumbnailAlgorithm(config.thumbnailAlgorithm);
          form.setFieldValue('thumbnailAlgorithm', config.thumbnailAlgorithm);
        }
      } catch (error) {
        console.error('[SettingsView] Failed to load thumbnail algorithm:', error);
      }
    };
    loadThumbnailAlgorithm();
  }, [form, selectedProfileId]);

  // Initialize form with annotation level, log level, theme, and language
  useEffect(() => {
    form.setFieldsValue({
      theme: theme,
      language: i18n.language,
      annotationLevel: annotationLevel,
      logLevel: logger.getCurrentLevelName(),
    });
    setSelectedAnnotationLevel(annotationLevel);
  }, [annotationLevel, theme, i18n.language, form]);

  const handleAnnotationLevelChange = async (value: AnnotationLevel) => {
    setSelectedAnnotationLevel(value);
    setAnnotationLevel(value);

    // Save to backend
    try {
      await settingsService.updateGlobalSetting('annotationLevel', value);
      notification.success(`Annotation level changed to: ${getAnnotationLevelLabel(value)}`);
    } catch (error) {
      notification.error('Failed to save annotation level setting');
      console.error('[SettingsView] Failed to save annotation level:', error);
    }
  };

  const handleLogLevelChange = async (value: LogLevelName) => {
    setLogLevel(value);
    logger.setLevel(value);

    // Save to backend
    try {
      await settingsService.updateGlobalSetting('logLevel', value);
      notification.success(`Log level changed to: ${value}`);
      logger.info('Log level changed', { newLevel: value });
    } catch (error) {
      notification.error('Failed to save log level setting');
      console.error('[SettingsView] Failed to save log level:', error);
    }
  };

  const handleThemeChange = (value: ThemeMode) => {
    setTheme(value);
    const themeLabel = value === 'auto' ? 'Auto (System)' : value.charAt(0).toUpperCase() + value.slice(1);
    notification.success(`Theme changed to: ${themeLabel}`);
  };

  const handleLanguageChange = async (value: string) => {
    try {
      await changeLanguage(value);
      const selectedLang = AVAILABLE_LANGUAGES.find(l => l.code === value);
      notification.success(`Language changed to: ${selectedLang?.name || value}`);
    } catch (error) {
      notification.error('Failed to change language');
      console.error('[SettingsView] Failed to change language:', error);
    }
  };

  const handleThumbnailAlgorithmChange = async (value: string) => {
    if (!selectedProfileId) {
      notification.error('No profile selected');
      return;
    }

    try {
      // Import the service dynamically to avoid circular dependencies
      const { updateActiveProfileConfigField } = await import('../../profiles/services/profileConfigService');
      await updateActiveProfileConfigField(selectedProfileId, 'thumbnailAlgorithm', value);
      notification.success('Thumbnail algorithm updated');
    } catch (error) {
      notification.error('Failed to update thumbnail algorithm');
      console.error('Failed to update thumbnail algorithm:', error);
    }
  };

  return (
    <div style={{ height: '100%', overflow: 'auto', padding: '24px' }}>
      <div style={{ maxWidth: '1200px', margin: '0 auto' }}>
      <Form
        form={form}
        layout="vertical"
        initialValues={{
          theme: theme,
          language: i18n.language,
          logLevel: logger.getCurrentLevelName(),
          thumbnailAlgorithm: thumbnailAlgorithm,
          migotoVersion: '3dmigoto',
        }}
      >
        <CompactCard title={<><SettingOutlined /> Global Settings</>} style={{ marginBottom: '24px' }}>
          <div style={{ marginBottom: '16px', padding: '12px', background: 'var(--color-info-bg)', borderRadius: '4px', fontSize: '13px', color: 'var(--color-text-base)' }}>
            These settings apply globally across all profiles and are saved automatically.
          </div>

          <Form.Item
            label="Theme"
            name="theme"
            tooltip="Choose the application theme"
          >
            <Select onChange={handleThemeChange}>
              <Option value="light">Light</Option>
              <Option value="dark">Dark</Option>
              <Option value="auto">Auto (System)</Option>
            </Select>
          </Form.Item>

          <Form.Item
            label="Language"
            name="language"
            tooltip="Select application language"
          >
            <Select value={i18n.language} onChange={handleLanguageChange}>
              {AVAILABLE_LANGUAGES.map(lang => (
                <Option key={lang.code} value={lang.code}>
                  {lang.name}
                </Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            label="Log Level"
            name="logLevel"
            tooltip="Set logging verbosity level for application logs. Changes are saved automatically."
          >
            <Select value={logLevel} onChange={handleLogLevelChange}>
              {Logger.getLevelOptions().map(option => (
                <Option key={option.value} value={option.value}>
                  {option.label} - {option.description}
                </Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            label={
              <CompactSpace>
                <span>Annotation Level</span>
                <InfoCircleOutlined style={{ color: 'var(--color-primary)' }} />
              </CompactSpace>
            }
            name="annotationLevel"
            tooltip="Control tooltip detail level throughout the application"
          >
            <Select
              value={selectedAnnotationLevel}
              onChange={handleAnnotationLevelChange}
            >
              <Option value="all">{getAnnotationLevelLabel('all')}</Option>
              <Option value="more">{getAnnotationLevelLabel('more')}</Option>
              <Option value="less">{getAnnotationLevelLabel('less')}</Option>
              <Option value="off">{getAnnotationLevelLabel('off')}</Option>
            </Select>
          </Form.Item>

          {selectedAnnotationLevel && (
            <div
              style={{
                padding: '12px',
                background: 'var(--color-info-bg)',
                border: '1px solid var(--color-info-border)',
                borderRadius: '4px',
                marginTop: '-12px',
                fontSize: '13px',
                color: 'var(--color-text-secondary)',
              }}
            >
              {getAnnotationLevelDescription(selectedAnnotationLevel)}
            </div>
          )}
        </CompactCard>

        <CompactCard title="Profile-Specific Settings" style={{ marginBottom: '24px' }}>
          <div style={{ marginBottom: '16px', padding: '12px', background: 'var(--color-warning-bg)', borderRadius: '4px', fontSize: '13px', color: 'var(--color-text-base)' }}>
            These settings are specific to the current profile and are saved automatically.
          </div>

          <Form.Item
            label="Thumbnail Algorithm"
            name="thumbnailAlgorithm"
            tooltip="Select the algorithm for matching mod thumbnails with preview images"
          >
            <Select onChange={handleThumbnailAlgorithmChange}>
              <Option value="key-in-only">Key-in Only - Manual filename matching</Option>
              <Option value="similarity-only">Similarity Only - Image similarity detection</Option>
              <Option value="similarity-threshold">Similarity Threshold - Similarity with confidence threshold (Recommended)</Option>
              <Option value="similarity-keyin">Similarity + Key-in - Combined approach</Option>
            </Select>
          </Form.Item>
        </CompactCard>
      </Form>
      </div>
    </div>
  );
};
