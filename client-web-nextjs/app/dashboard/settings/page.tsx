'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { useTranslation } from '@/hooks/useTranslation';
import { useAuthStore } from '@/store/authStore';
import { ThemeToggle } from '@/components/ThemeToggle';
import apiClient from '@/lib/axios';
import { Shield, Key, User, Globe, Bell } from 'lucide-react';

export default function SettingsPage() {
  const router = useRouter();
  const { t } = useTranslation();
  const { isAuthenticated, user } = useAuthStore();
  const [twoFactorEnabled, setTwoFactorEnabled] = useState(false);
  const [qrCode, setQrCode] = useState('');
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/login');
      return;
    }
    setTwoFactorEnabled(user?.twoFactorEnabled || false);
  }, [isAuthenticated, user, router]);

  const handleEnable2FA = async () => {
    setLoading(true);
    setError('');
    try {
      const response = await apiClient.post('/api/auth/two-factor/enable');
      setQrCode(response.data.qrCode);
      setRecoveryCodes(response.data.recoveryCodes);
      setTwoFactorEnabled(true);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Ошибка включения 2FA');
    } finally {
      setLoading(false);
    }
  };

  const handleDisable2FA = async () => {
    const password = prompt('Введите пароль для отключения 2FA:');
    if (!password) return;

    setLoading(true);
    setError('');
    try {
      await apiClient.post('/api/auth/two-factor/disable', { password });
      setTwoFactorEnabled(false);
      setQrCode('');
      setRecoveryCodes([]);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Ошибка отключения 2FA');
    } finally {
      setLoading(false);
    }
  };

  if (!isAuthenticated) {
    return null;
  }

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-900 dark:to-black p-4">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="max-w-4xl mx-auto"
      >
        <h1 className="text-3xl font-bold mb-8 text-purple-600 dark:text-purple-400">
          {t('common.settings')}
        </h1>

        {error && (
          <div className="mb-4 p-4 bg-red-900 text-red-200 rounded">{error}</div>
        )}

        <div className="space-y-6">
          {/* Профиль */}
          <div className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg">
            <h2 className="text-xl font-semibold mb-4 flex items-center space-x-2">
              <User className="w-6 h-6 text-purple-600" />
              <span>Профиль пользователя</span>
            </h2>
            <div className="space-y-3">
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Имя пользователя:</span>
                <span className="font-semibold">{user?.username}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Email:</span>
                <span className="font-semibold">{user?.email}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Роль:</span>
                <span className="font-semibold">
                  {user?.isAdmin ? (
                    <span className="text-red-500">Администратор</span>
                  ) : (
                    'Пользователь'
                  )}
                </span>
              </div>
            </div>
          </div>

          {/* Тема */}
          <div className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg">
            <h2 className="text-xl font-semibold mb-4 flex items-center space-x-2">
              <Globe className="w-6 h-6 text-purple-600" />
              <span>Внешний вид</span>
            </h2>
            <div className="flex items-center justify-between">
              <div>
                <p className="font-medium">Тёмная / Светлая тема</p>
                <p className="text-sm text-gray-600 dark:text-gray-400">
                  Выберите удобную для вас тему оформления
                </p>
              </div>
              <ThemeToggle />
            </div>
          </div>

          {/* Язык */}
          <div className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg">
            <h2 className="text-xl font-semibold mb-4 flex items-center space-x-2">
              <Globe className="w-6 h-6 text-purple-600" />
              <span>Язык интерфейса</span>
            </h2>
            <select className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg">
              <option value="ru">🇷🇺 Русский</option>
              <option value="en">🇬🇧 English</option>
            </select>
          </div>

          {/* 2FA */}
          <div className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg">
            <h2 className="text-xl font-semibold mb-4 flex items-center space-x-2">
              <Shield className="w-6 h-6 text-purple-600" />
              <span>{t('auth.twoFactor')}</span>
            </h2>

            {!twoFactorEnabled ? (
              <div>
                <p className="text-gray-600 dark:text-gray-400 mb-4">
                  Двухфакторная аутентификация повышает безопасность вашего аккаунта.
                  После включения потребуется код из приложения-аутентификатора при входе.
                </p>
                <button
                  onClick={handleEnable2FA}
                  disabled={loading}
                  className="px-4 py-2 bg-purple-600 hover:bg-purple-700 rounded-lg text-white disabled:opacity-50"
                >
                  {loading ? 'Загрузка...' : 'Включить 2FA'}
                </button>
              </div>
            ) : (
              <div>
                <p className="text-green-600 dark:text-green-400 mb-4 flex items-center space-x-2">
                  <Shield className="w-5 h-5" />
                  <span>✓ 2FA включен</span>
                </p>
                {qrCode && (
                  <div className="mb-4">
                    <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">
                      Отсканируйте QR-код в приложении-аутентификаторе:
                    </p>
                    <img
                      src={`data:image/png;base64,${qrCode}`}
                      alt="2FA QR Code"
                      className="mx-auto border border-gray-300 dark:border-gray-700 rounded"
                    />
                  </div>
                )}
                {recoveryCodes.length > 0 && (
                  <div className="mb-4 p-4 bg-yellow-900 bg-opacity-20 border border-yellow-700 rounded">
                    <p className="text-sm font-semibold mb-2 text-yellow-200">
                      ⚠️ Сохраните эти recovery codes в безопасном месте:
                    </p>
                    <div className="grid grid-cols-2 gap-2">
                      {recoveryCodes.map((code, index) => (
                        <code
                          key={index}
                          className="block p-2 bg-gray-800 text-gray-100 rounded text-sm font-mono"
                        >
                          {code}
                        </code>
                      ))}
                    </div>
                  </div>
                )}
                <button
                  onClick={handleDisable2FA}
                  disabled={loading}
                  className="px-4 py-2 bg-red-600 hover:bg-red-700 rounded-lg text-white disabled:opacity-50"
                >
                  {loading ? 'Загрузка...' : 'Отключить 2FA'}
                </button>
              </div>
            )}
          </div>

          {/* Уведомления */}
          <div className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg">
            <h2 className="text-xl font-semibold mb-4 flex items-center space-x-2">
              <Bell className="w-6 h-6 text-purple-600" />
              <span>Уведомления</span>
            </h2>
            <div className="space-y-3">
              <label className="flex items-center justify-between">
                <span>Email уведомления при запуске профиля</span>
                <input type="checkbox" className="w-5 h-5" />
              </label>
              <label className="flex items-center justify-between">
                <span>Email уведомления при ошибках</span>
                <input type="checkbox" defaultChecked className="w-5 h-5" />
              </label>
            </div>
          </div>
        </div>
      </motion.div>
    </div>
  );
}