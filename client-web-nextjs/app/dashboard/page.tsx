'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { useAuthStore } from '@/store/authStore';
import { useProfileStore } from '@/store/profileStore';
import { useTranslation } from '@/hooks/useTranslation';
import Layout from '@/components/Layout';
import { Plus, Play, Square, Trash2 } from 'lucide-react';

export default function DashboardPage() {
  const router = useRouter();
  const { isAuthenticated, user } = useAuthStore();
  const { profiles, loading, error, fetchProfiles, createProfile, startProfile, stopProfile, deleteProfile } =
    useProfileStore();
  const { t } = useTranslation();

  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/login');
      return;
    }
    fetchProfiles();
  }, [isAuthenticated, fetchProfiles, router]);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Running':
        return 'text-green-400';
      case 'Starting':
        return 'text-yellow-400';
      case 'Stopping':
        return 'text-yellow-400';
      case 'Error':
        return 'text-red-400';
      default:
        return 'text-gray-400';
    }
  };

  if (!isAuthenticated) {
    return null;
  }

  return (
    <Layout>
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          className="mb-8"
        >
          <div className="flex justify-between items-center mb-8">
            <h1 className="text-3xl font-bold">{t('profile.title')}</h1>
            <button
              onClick={() => router.push('/dashboard/profile/create')}
              className="flex items-center space-x-2 px-4 py-2 bg-purple-600 hover:bg-purple-700 rounded-lg text-white"
            >
              <Plus className="w-5 h-5" />
              <span>{t('profile.create')}</span>
            </button>
          </div>

          {error && (
            <div className="mb-4 p-4 bg-red-900 text-red-200 rounded">{error}</div>
          )}

          {loading ? (
            <div className="text-center py-8">Загрузка...</div>
          ) : profiles.length === 0 ? (
            <div className="text-center py-8 text-gray-400">
              {t('profile.noProfiles') || 'Нет профилей. Создайте первый профиль.'}
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {profiles.map((profile) => (
                <motion.div
                  key={profile.id}
                  initial={{ opacity: 0, scale: 0.9 }}
                  animate={{ opacity: 1, scale: 1 }}
                  whileHover={{ scale: 1.05 }}
                  className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg"
                >
                  <h3 className="text-xl font-semibold mb-2">{profile.name}</h3>
                  <div className="space-y-2 mb-4">
                    <div className="flex justify-between">
                      <span className="text-gray-400">{t('profile.status')}:</span>
                      <span className={getStatusColor(profile.status)}>{profile.status}</span>
                    </div>
                    {profile.serverNodeIp && (
                      <div className="flex justify-between">
                        <span className="text-gray-400">{t('profile.node')}:</span>
                        <span>{profile.serverNodeIp}</span>
                      </div>
                    )}
                    {profile.port > 0 && (
                      <div className="flex justify-between">
                        <span className="text-gray-400">{t('profile.port')}:</span>
                        <span>{profile.port}</span>
                      </div>
                    )}
                  </div>
                  <div className="flex space-x-2">
                    {profile.status === 'Stopped' && (
                      <button
                        onClick={() => startProfile(profile.id)}
                        className="flex-1 flex items-center justify-center space-x-2 px-4 py-2 bg-green-600 hover:bg-green-700 rounded-lg text-white"
                      >
                        <Play className="w-4 h-4" />
                        <span>{t('common.start')}</span>
                      </button>
                    )}
                    {profile.status === 'Running' && (
                      <button
                        onClick={() => stopProfile(profile.id)}
                        className="flex-1 flex items-center justify-center space-x-2 px-4 py-2 bg-yellow-600 hover:bg-yellow-700 rounded-lg text-white"
                      >
                        <Square className="w-4 h-4" />
                        <span>{t('common.stop')}</span>
                      </button>
                    )}
                    <button
                      onClick={() => deleteProfile(profile.id)}
                      className="px-4 py-2 bg-red-600 hover:bg-red-700 rounded-lg text-white"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                </motion.div>
              ))}
            </div>
          )}
        </motion.div>
    </Layout>
  );
}

