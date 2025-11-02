'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { useTranslation } from '@/hooks/useTranslation';
import { useProfileStore, BrowserConfig } from '@/store/profileStore';

export default function CreateProfilePage() {
  const router = useRouter();
  const { t } = useTranslation();
  const { createProfile } = useProfileStore();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [name, setName] = useState('');
  const [config, setConfig] = useState<BrowserConfig>({
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
    screenResolution: '1920x1080',
    timezone: 'UTC',
    language: 'en-US',
    webRTC: false,
    canvas: false,
    webGL: false,
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      await createProfile(name, config);
      router.push('/dashboard');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Ошибка создания профиля');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-900 dark:to-black p-4">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="max-w-2xl mx-auto"
      >
        <h1 className="text-3xl font-bold mb-8 text-purple-600 dark:text-purple-400">
          {t('profile.create')}
        </h1>
        <form
          onSubmit={handleSubmit}
          className="bg-white dark:bg-gray-900 p-8 rounded-lg border border-gray-200 dark:border-gray-800 shadow-xl space-y-6"
        >
          {error && (
            <div className="p-4 bg-red-900 text-red-200 rounded">{error}</div>
          )}

          <div>
            <label className="block mb-2 text-gray-700 dark:text-gray-300">
              {t('profile.name')}
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
            />
          </div>

          <div>
            <label className="block mb-2 text-gray-700 dark:text-gray-300">
              User Agent
            </label>
            <textarea
              value={config.userAgent}
              onChange={(e) => setConfig({ ...config, userAgent: e.target.value })}
              rows={3}
              className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block mb-2 text-gray-700 dark:text-gray-300">
                Screen Resolution
              </label>
              <input
                type="text"
                value={config.screenResolution}
                onChange={(e) => setConfig({ ...config, screenResolution: e.target.value })}
                placeholder="1920x1080"
                className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
              />
            </div>
            <div>
              <label className="block mb-2 text-gray-700 dark:text-gray-300">
                Timezone
              </label>
              <input
                type="text"
                value={config.timezone}
                onChange={(e) => setConfig({ ...config, timezone: e.target.value })}
                placeholder="UTC"
                className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
              />
            </div>
          </div>

          <div>
            <label className="block mb-2 text-gray-700 dark:text-gray-300">
              Language
            </label>
            <input
              type="text"
              value={config.language}
              onChange={(e) => setConfig({ ...config, language: e.target.value })}
              placeholder="en-US"
              className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
            />
          </div>

          <div className="space-y-2">
            <label className="block text-gray-700 dark:text-gray-300">Настройки</label>
            <div className="space-y-2">
              <label className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  checked={config.webRTC}
                  onChange={(e) => setConfig({ ...config, webRTC: e.target.checked })}
                  className="w-4 h-4 text-purple-600 rounded focus:ring-purple-500"
                />
                <span className="text-gray-700 dark:text-gray-300">WebRTC</span>
              </label>
              <label className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  checked={config.canvas}
                  onChange={(e) => setConfig({ ...config, canvas: e.target.checked })}
                  className="w-4 h-4 text-purple-600 rounded focus:ring-purple-500"
                />
                <span className="text-gray-700 dark:text-gray-300">Canvas Fingerprinting</span>
              </label>
              <label className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  checked={config.webGL}
                  onChange={(e) => setConfig({ ...config, webGL: e.target.checked })}
                  className="w-4 h-4 text-purple-600 rounded focus:ring-purple-500"
                />
                <span className="text-gray-700 dark:text-gray-300">WebGL Fingerprinting</span>
              </label>
            </div>
          </div>

          <div className="flex space-x-4">
            <button
              type="button"
              onClick={() => router.back()}
              className="flex-1 px-4 py-2 bg-gray-200 dark:bg-gray-800 hover:bg-gray-300 dark:hover:bg-gray-700 rounded-lg transition-colors"
            >
              {t('common.cancel')}
            </button>
            <button
              type="submit"
              disabled={loading}
              className="flex-1 px-4 py-2 bg-purple-600 hover:bg-purple-700 rounded-lg font-semibold disabled:opacity-50 transition-colors text-white"
            >
              {loading ? 'Загрузка...' : t('common.create')}
            </button>
          </div>
        </form>
      </motion.div>
    </div>
  );
}

