import React, { useState, useCallback } from 'react';
import { Steps } from 'antd';
import {
  RocketOutlined,
  FolderOpenOutlined,
  CheckCircleOutlined,
  AppstoreOutlined,
  ThunderboltOutlined,
  GlobalOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';
import { FormDialog } from '../../../../shared/components/dialogs';
import { CompactButton } from '../../../../shared/components/compact';
import { StatusTag } from '../../../../shared/components/common/StatusTag';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { profileService } from '../../../../shared/services/ipc';
import { handleError } from '../../../../shared/utils/errorHandler';
import { notification } from '../../../../shared/utils/notification';
import { AVAILABLE_LANGUAGES } from '../../../../shared/types/language.types';
import { changeLanguage } from '../../../../shared/services/i18n';
import { logger } from '../../../../shared/utils/logger';
import { useSettingsStore } from '../../../setting/store/settingsStore';
import { XxmiImporterPicker } from '../../../setting/components/XxmiImporterPicker';
import './OnboardingWizard.css';

/** localStorage flag — per-machine "have we shown first-run onboarding" UX state (like theme).
 *  Re-exported from onboardingConstants so App can read it without importing this (lazy) component. */
export { ONBOARDING_DONE_KEY } from './onboardingConstants';
import { ONBOARDING_DONE_KEY } from './onboardingConstants';

interface OnboardingWizardProps {
  open: boolean;
  onClose: () => void;
}

const STEP_COUNT = 4;

/**
 * First-run guided setup: language → welcome → point the mod library at an XXMI install (or keep
 * app-managed) → ready. The LANGUAGE step comes first so the rest of the wizard (and the app) is
 * read in the user's language; picking one switches i18n immediately and persists the global
 * setting. Reuses XxmiImporterPicker (one pick sets work dir + launcher, exactly like Settings).
 * Optional — every step can be skipped; completion is remembered in localStorage so it shows once.
 */
export const OnboardingWizard: React.FC<OnboardingWizardProps> = ({ open, onClose }) => {
  const { t, i18n } = useTranslation();
  const { selectedProfileId, selectedProfile } = useProfile();
  const { cleanupEnabled, cleanupMaxCaches, setInitialProfileConfig } = useSettingsStore();

  const [step, setStep] = useState(0);
  const [xxmiBound, setXxmiBound] = useState(false);

  // Switch the app language in place — the wizard re-renders in the chosen language right away.
  const pickLanguage = useCallback(async (code: string) => {
    if (i18n.language === code) return;
    try {
      await changeLanguage(code);
    } catch (error) {
      logger.error('[OnboardingWizard] Failed to change language:', error);
    }
  }, [i18n.language]);

  const finish = useCallback(() => {
    try {
      localStorage.setItem(ONBOARDING_DONE_KEY, '1');
    } catch {
      // localStorage unavailable (private mode) — harmless, onboarding just shows again next launch.
    }
    setStep(0);
    setXxmiBound(false);
    onClose();
  }, [onClose]);

  // One-click XXMI bind — mirrors ProfileSettingsTab.handleSelectXxmiImporter so Settings stays in sync.
  const applyXxmi = useCallback(
    async (importerDir: string, _modsDir: string, launcherExe?: string) => {
      if (!selectedProfileId) {
        notification.error(t('errors.noProfileSelected'));
        return;
      }
      try {
        await profileService.updateProfileConfig({
          profileId: selectedProfileId,
          workMode: 'xxmi',
          workDirectory: importerDir,
          ...(launcherExe ? { launchPath: launcherExe } : {}),
        });
        setInitialProfileConfig({ mode: 'xxmi', directory: importerDir, cleanupEnabled, cleanupMaxCaches });
        setXxmiBound(true);
        notification.success(t('settings.profile.modWork.xxmi.applied'));
        setStep(STEP_COUNT - 1);
      } catch (error) {
        handleError(error);
      }
    },
    [selectedProfileId, cleanupEnabled, cleanupMaxCaches, setInitialProfileConfig, t],
  );

  const steps = [
    {
      key: 'language',
      title: t('onboarding.steps.language'),
      icon: <GlobalOutlined />,
    },
    {
      key: 'welcome',
      title: t('onboarding.steps.welcome'),
      icon: <RocketOutlined />,
    },
    {
      key: 'location',
      title: t('onboarding.steps.location'),
      icon: <FolderOpenOutlined />,
    },
    {
      key: 'done',
      title: t('onboarding.steps.done'),
      icon: <CheckCircleOutlined />,
    },
  ];

  const footer = (
    <div className="onboarding-wizard__footer">
      <span className="onboarding-wizard__footer-left">
        {step > 0 && step < STEP_COUNT - 1 && (
          <CompactButton onClick={() => setStep((s) => s - 1)}>{t('common.back')}</CompactButton>
        )}
      </span>
      <span className="onboarding-wizard__footer-right">
        {step < STEP_COUNT - 1 && (
          <CompactButton type="text" onClick={finish}>
            {t('common.skip')}
          </CompactButton>
        )}
        {step === 0 && (
          <CompactButton type="primary" onClick={() => setStep(1)}>
            {t('common.next')}
          </CompactButton>
        )}
        {step === 1 && (
          <CompactButton type="primary" onClick={() => setStep(2)}>
            {t('onboarding.getStarted')}
          </CompactButton>
        )}
        {step === 2 && (
          <CompactButton type="primary" onClick={() => setStep(STEP_COUNT - 1)}>
            {t('common.next')}
          </CompactButton>
        )}
        {step === STEP_COUNT - 1 && (
          <CompactButton type="primary" onClick={finish}>
            {t('common.finish')}
          </CompactButton>
        )}
      </span>
    </div>
  );

  return (
    <FormDialog
      visible={open}
      title={t('onboarding.title')}
      onCancel={finish}
      footer={footer}
      width={560}
    >
      <div className="onboarding-wizard">
        <Steps size="small" current={step} items={steps} className="onboarding-wizard__steps" />

        {step === 0 && (
          <div className="onboarding-wizard__panel">
            {/* Bilingual copy on purpose — the user hasn't picked a language yet. */}
            <h3 className="onboarding-wizard__heading">{t('onboarding.language.title')}</h3>
            <p className="onboarding-wizard__text">{t('onboarding.language.body')}</p>
            <div className="onboarding-wizard__langs">
              {AVAILABLE_LANGUAGES.map((lang) => (
                <button
                  key={lang.code}
                  type="button"
                  className={classNames('onboarding-wizard__lang', {
                    'onboarding-wizard__lang--active': i18n.language === lang.code,
                  })}
                  onClick={() => void pickLanguage(lang.code)}
                >
                  <span className="onboarding-wizard__lang-name">{lang.name}</span>
                  {i18n.language === lang.code && (
                    <CheckCircleOutlined className="onboarding-wizard__lang-check" />
                  )}
                </button>
              ))}
            </div>
          </div>
        )}

        {step === 1 && (
          <div className="onboarding-wizard__panel">
            <h3 className="onboarding-wizard__heading">
              {t('onboarding.welcome.title', { name: selectedProfile?.name ?? '' })}
            </h3>
            <p className="onboarding-wizard__text">{t('onboarding.welcome.body')}</p>
            <ul className="onboarding-wizard__list">
              <li>
                <AppstoreOutlined className="onboarding-wizard__list-icon" />
                {t('onboarding.welcome.point1')}
              </li>
              <li>
                <ThunderboltOutlined className="onboarding-wizard__list-icon" />
                {t('onboarding.welcome.point2')}
              </li>
            </ul>
          </div>
        )}

        {step === 2 && (
          <div className="onboarding-wizard__panel">
            <h3 className="onboarding-wizard__heading">{t('onboarding.location.title')}</h3>
            <p className="onboarding-wizard__text">{t('onboarding.location.body')}</p>
            <XxmiImporterPicker
              profileId={selectedProfileId ?? undefined}
              onSelect={applyXxmi}
            />
            {xxmiBound && (
              <div className="onboarding-wizard__bound">
                <StatusTag tone="success" label={t('onboarding.location.bound')} />
              </div>
            )}
            <p className="onboarding-wizard__hint">{t('onboarding.location.skipHint')}</p>
          </div>
        )}

        {step === STEP_COUNT - 1 && (
          <div className="onboarding-wizard__panel">
            <h3 className="onboarding-wizard__heading">{t('onboarding.done.title')}</h3>
            <p className="onboarding-wizard__text">{t('onboarding.done.body')}</p>
            <ul className="onboarding-wizard__list">
              <li>{t('onboarding.done.point1')}</li>
              <li>{t('onboarding.done.point2')}</li>
              <li>{t('onboarding.done.point3')}</li>
            </ul>
          </div>
        )}
      </div>
    </FormDialog>
  );
};
