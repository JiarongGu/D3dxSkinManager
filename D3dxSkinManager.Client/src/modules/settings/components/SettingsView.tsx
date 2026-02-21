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
import { logger, Logger, LogLevelName } from '../../../shared/utils/logger';
import { settingsService } from '../services/settingsService';
import { useProfile } from '../../../shared/context/ProfileContext';
import styles from './SettingsView.module.css';

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
      notification.success(t('settings.notifications.annotationLevelChanged', { level: getAnnotationLevelLabel(value) }));
    } catch (error) {
      notification.error(t('settings.notifications.annotationLevelFailed'));
      console.error('[SettingsView] Failed to save annotation level:', error);
    }
  };

  const handleLogLevelChange = async (value: LogLevelName) => {
    setLogLevel(value);
    logger.setLevel(value);

    // Save to backend
    try {
      await settingsService.updateGlobalSetting('logLevel', value);
      notification.success(t('settings.notifications.logLevelChanged', { level: value }));
      logger.info('Log level changed', { newLevel: value });
    } catch (error) {
      notification.error(t('settings.notifications.logLevelFailed'));
      console.error('[SettingsView] Failed to save log level:', error);
    }
  };

  const handleThemeChange = (value: ThemeMode) => {
    setTheme(value);
    const themeLabel = value === 'auto' ? t('settings.theme.auto') :
                       value === 'light' ? t('settings.theme.light') :
                       t('settings.theme.dark');
    notification.success(t('settings.notifications.themeChanged', { theme: themeLabel }));
  };

  const handleLanguageChange = async (value: string) => {
    try {
      await changeLanguage(value);
      const selectedLang = AVAILABLE_LANGUAGES.find(l => l.code === value);
      notification.success(t('settings.notifications.languageChanged', { language: selectedLang?.name || value }));
    } catch (error) {
      notification.error(t('settings.notifications.languageFailed'));
      console.error('[SettingsView] Failed to change language:', error);
    }
  };

  const handleThumbnailAlgorithmChange = async (value: string) => {
    if (!selectedProfileId) {
      notification.error(t('errors.noProfileSelected'));
      return;
    }

    try {
      // Import the service dynamically to avoid circular dependencies
      const { updateActiveProfileConfigField } = await import('../../profiles/services/profileConfigService');
      await updateActiveProfileConfigField(selectedProfileId, 'thumbnailAlgorithm', value);
      notification.success(t('settings.notifications.thumbnailAlgorithmUpdated'));
    } catch (error) {
      notification.error(t('settings.notifications.thumbnailAlgorithmFailed'));
      console.error('Failed to update thumbnail algorithm:', error);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.contentWrapper}>
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
        <CompactCard title={<><SettingOutlined /> {t('settings.global.title')}</>} className={styles.cardMargin}>
          <div className={styles.infoAlert}>
            {t('settings.global.description')}
          </div>

          <Form.Item
            label={t('settings.global.theme.label')}
            name="theme"
            tooltip={t('settings.global.theme.tooltip')}
          >
            <Select onChange={handleThemeChange}>
              <Option value="light">{t('settings.theme.light')}</Option>
              <Option value="dark">{t('settings.theme.dark')}</Option>
              <Option value="auto">{t('settings.theme.auto')}</Option>
            </Select>
          </Form.Item>

          <Form.Item
            label={t('settings.global.language.label')}
            name="language"
            tooltip={t('settings.global.language.tooltip')}
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
            label={t('settings.global.logLevel.label')}
            name="logLevel"
            tooltip={t('settings.global.logLevel.tooltip')}
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
                <span>{t('settings.global.annotationLevel.label')}</span>
                <InfoCircleOutlined className={styles.primaryIcon} />
              </CompactSpace>
            }
            name="annotationLevel"
            tooltip={t('settings.global.annotationLevel.tooltip')}
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
            <div className={styles.annotationDescription}>
              {getAnnotationLevelDescription(selectedAnnotationLevel)}
            </div>
          )}
        </CompactCard>

        <CompactCard title={t('settings.profile.title')} className={styles.cardMargin}>
          <div className={styles.warningAlert}>
            {t('settings.profile.description')}
          </div>

          <Form.Item
            label={t('settings.profile.thumbnailAlgorithm.label')}
            name="thumbnailAlgorithm"
            tooltip={t('settings.profile.thumbnailAlgorithm.tooltip')}
          >
            <Select onChange={handleThumbnailAlgorithmChange}>
              <Option value="key-in-only">{t('settings.profile.thumbnailAlgorithm.keyInOnly')}</Option>
              <Option value="similarity-only">{t('settings.profile.thumbnailAlgorithm.similarityOnly')}</Option>
              <Option value="similarity-threshold">{t('settings.profile.thumbnailAlgorithm.similarityThreshold')}</Option>
              <Option value="similarity-keyin">{t('settings.profile.thumbnailAlgorithm.similarityKeyin')}</Option>
            </Select>
          </Form.Item>
        </CompactCard>
      </Form>
      </div>
    </div>
  );
};
