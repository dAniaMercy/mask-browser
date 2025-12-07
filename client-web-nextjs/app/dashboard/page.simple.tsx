'use client';

// Упрощенная версия без React ошибок
import { useEffect, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { useAuthStore } from '@/store/authStore';
import { useProfileStore } from '@/store/profileStore';
import { useTranslation } from '@/hooks/useTranslation';
import { Plus, Play, Square, Trash2, Monitor } from 'lucide-react';

export default function DashboardPage() {
  const router = useRouter();
  const { isAuthenticated } = useAuthStore();
  const { profiles, loading, error, startProfile, stopProfile, deleteProfile } = useProfileStore();
  const { t } = useTranslation();
  const isMountedRef = useRef(true);
  const fetchIntervalRef = useRef<NodeJS.Timeout | null>(null);

  // Восстановление авторизации
  useEffect(() => {
    const { hydrate } = useAuthStore.getState();
    hydrate();
    
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  // Редирект на логин
  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/login');
    }
  }, [isAuthenticated, router]);

  // Загрузка профилей при монтировании
  useEffect(() => {
    if (!isAuthenticated || !isMountedRef.current) return;
    
    // Загружаем профили сразу
    const { fetchProfiles } = useProfileStore.getState();
    fetchProfiles();
  }, [isAuthenticated]);

  // Автоматическое обновление статусов
  useEffect(() => {
    if (!isAuthenticated || profiles.length === 0) {
      if (fetchIntervalRef.current) {
        clearInterval(fetchIntervalRef.current);
        fetchIntervalRef.current = null;
      }
      return;
    }
    
    const hasStartingOrStopping = profiles.some(
      (p) => p.status === 'Starting' || p.status === 'Stopping'
    );
    
    if (!hasStartingOrStopping) {
      if (fetchIntervalRef.current) {
        clearInterval(fetchIntervalRef.current);
        fetchIntervalRef.current = null;
      }
      return;
    }
    
    // Автоматическое обновление каждые 3 секунды
    if (!fetchIntervalRef.current) {
      fetchIntervalRef.current = setInterval(() => {
        if (isMountedRef.current) {
          const { fetchProfiles } = useProfileStore.getState();
          fetchProfiles();
        }
      }, 3000);
    }
    
    return () => {
      if (fetchIntervalRef.current) {
        clearInterval(fetchIntervalRef.current);
        fetchIntervalRef.current = null;
      }
    };
  }, [isAuthenticated, profiles]);

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
      ) : !profiles || profiles.length === 0 ? (
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
                    disabled={loading}
                    className="flex-1 flex items-center justify-center space-x-2 px-4 py-2 bg-green-600 hover:bg-green-700 rounded-lg text-white disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    <Play className="w-4 h-4" />
                    <span>{t('common.start')}</span>
                  </button>
                )}
                {profile.status === 'Starting' && (
                  <button
                    disabled
                    className="flex-1 flex items-center justify-center space-x-2 px-4 py-2 bg-yellow-600 rounded-lg text-white opacity-75 cursor-not-allowed"
                  >
                    <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                    <span>Запуск...</span>
                  </button>
                )}
                {profile.status === 'Stopping' && (
                  <button
                    disabled
                    className="flex-1 flex items-center justify-center space-x-2 px-4 py-2 bg-yellow-600 rounded-lg text-white opacity-75 cursor-not-allowed"
                  >
                    <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                    <span>Остановка...</span>
                  </button>
                )}
                {profile.status === 'Running' && (
                  <>
                    <button
                      onClick={() => router.push(`/dashboard/profile/${profile.id}/browser`)}
                      disabled={loading}
                      className="flex-1 flex items-center justify-center space-x-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 rounded-lg text-white disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <Monitor className="w-4 h-4" />
                      <span>Открыть браузер</span>
                    </button>
                    <button
                      onClick={() => stopProfile(profile.id)}
                      disabled={loading}
                      className="flex-1 flex items-center justify-center space-x-2 px-4 py-2 bg-yellow-600 hover:bg-yellow-700 rounded-lg text-white disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <Square className="w-4 h-4" />
                      <span>{t('common.stop')}</span>
                    </button>
                  </>
                )}
                {profile.status === 'Error' && (
                  <button
                    onClick={() => startProfile(profile.id)}
                    disabled={loading}
                    className="flex-1 flex items-center justify-center space-x-2 px-4 py-2 bg-orange-600 hover:bg-orange-700 rounded-lg text-white disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    <Play className="w-4 h-4" />
                    <span>Повторить</span>
                  </button>
                )}
                <button
                  onClick={() => deleteProfile(profile.id)}
                  disabled={loading || profile.status === 'Starting' || profile.status === 'Running'}
                  className="px-4 py-2 bg-red-600 hover:bg-red-700 rounded-lg text-white disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </motion.div>
          ))}
        </div>
      )}
    </motion.div>
  );
}

