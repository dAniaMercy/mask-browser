'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { useTranslation } from '@/hooks/useTranslation';
import { useAuthStore } from '@/store/authStore';
import { ThemeToggle } from '@/components/ThemeToggle';
import axios from 'axios';
import { Shield, Key } from 'lucide-react';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5050';

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
      const token = JSON.parse(localStorage.getItem('auth-storage') || '{}').state?.token;
      const response = await axios.post(
        `${API_URL}/api/auth/two-factor/enable`,
        {},
        {
          headers: { Authorization: `Bearer ${token}` }
        }
      );
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
      const token = JSON.parse(localStorage.getItem('auth-storage') || '{}').state?.token;
      await axios.post(
        `${API_URL}/api/auth/two-factor/disable`,
        { password },
        {
          headers: { Authorization: `Bearer ${token}` }
        }
      );
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
        className="max-w-2xl mx-auto"
      >
        <h1 className="text-3xl font-bold mb-8 text-purple-600 dark:text-purple-400">
          {t('common.settings')}
        </h1>

        {error && (
          <div className="mb-4 p-4 bg-red-900 text-red-200 rounded">{error}</div>
        )}

        <div className="space-y-6">
          {/* Theme Settings */}
          <div className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg">
            <h2 className="text-xl font-semibold mb-4 flex items-center space-x-2">
              <span>{t('common.theme')}</span>
            </h2>
            <div className="flex items-center justify-between">
              <span className="text-gray-600 dark:text-gray-400">
                {t('common.theme')}: Тёмная / Светлая
              </span>
              <ThemeToggle />
            </div>
          </div>

          {/* Two-Factor Authentication */}
          <div className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg">
            <h2 className="text-xl font-semibold mb-4 flex items-center space-x-2">
              <Shield className="w-6 h-6 text-purple-600" />
              <span>{t('auth.twoFactor')}</span>
            </h2>

            {!twoFactorEnabled ? (
              <div>
                <p className="text-gray-600 dark:text-gray-400 mb-4">
                  Двухфакторная аутентификация повышает безопасность вашего аккаунта
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
                <p className="text-green-600 dark:text-green-400 mb-4">
                  ✓ 2FA включен
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
        </div>
      </motion.div>
    </div>
  );
}

