'use client';

import { useTranslation as useI18nTranslation } from 'react-i18next';

export function useTranslation() {
  const { t, i18n } = useI18nTranslation();

  const changeLanguage = (lang: 'ru' | 'en') => {
    i18n.changeLanguage(lang);
  };

  return {
    t,
    currentLanguage: i18n.language as 'ru' | 'en',
    changeLanguage,
  };
}

