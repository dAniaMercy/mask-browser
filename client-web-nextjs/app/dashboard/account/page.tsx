'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { useTranslation } from '@/hooks/useTranslation';
import { useAuthStore } from '@/store/authStore';
import { User, Mail, Shield, Calendar, Save } from 'lucide-react';

export default function AccountPage() {
  const router = useRouter();
  const { t } = useTranslation();
  const { user, isAuthenticated, token } = useAuthStore();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const [formData, setFormData] = useState({
    username: '',
    email: '',
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  });

  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/login');
      return;
    }
    if (user) {
      setFormData({
        username: user.username,
        email: user.email,
        currentPassword: '',
        newPassword: '',
        confirmPassword: '',
      });
    }
  }, [isAuthenticated, user, router]);

  const handleUpdateProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    // В текущей версии API сервер не поддерживает обновление профиля пользователя.
    // Чтобы избежать 404, просто информируем пользователя.
    setError('Обновление профиля пока не поддерживается на сервере.');
  };

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    if (formData.newPassword !== formData.confirmPassword) {
      setError('Новые пароли не совпадают');
      return;
    }

    if (formData.newPassword.length < 6) {
      setError('Пароль должен быть минимум 6 символов');
      return;
    }

    // В текущей версии API сервер не поддерживает смену пароля.
    // Чтобы избежать 404, просто информируем пользователя.
    setError('Смена пароля пока не поддерживается на сервере.');
  };

  if (!isAuthenticated || !user) {
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
          {t('common.account') || 'Мой аккаунт'}
        </h1>

        {error && (
          <div className="mb-4 p-4 bg-red-900 text-red-200 rounded">{error}</div>
        )}

        {success && (
          <div className="mb-4 p-4 bg-green-900 text-green-200 rounded">{success}</div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Информация об аккаунте */}
          <motion.div
            initial={{ opacity: 0, x: -20 }}
            animate={{ opacity: 1, x: 0 }}
            className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg"
          >
            <h2 className="text-xl font-semibold mb-4 flex items-center space-x-2">
              <User className="w-6 h-6 text-purple-600" />
              <span>Информация об аккаунте</span>
            </h2>

            <div className="space-y-4">
              <div className="flex items-center space-x-3 p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <User className="w-5 h-5 text-gray-500" />
                <div>
                  <p className="text-sm text-gray-500 dark:text-gray-400">Имя пользователя</p>
                  <p className="font-semibold">{user.username}</p>
                </div>
              </div>

              <div className="flex items-center space-x-3 p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <Mail className="w-5 h-5 text-gray-500" />
                <div>
                  <p className="text-sm text-gray-500 dark:text-gray-400">Email</p>
                  <p className="font-semibold">{user.email}</p>
                </div>
              </div>

              <div className="flex items-center space-x-3 p-3 bg-gray-50 dark:bg-gray-800 rounded">
                <Shield className="w-5 h-5 text-gray-500" />
                <div>
                  <p className="text-sm text-gray-500 dark:text-gray-400">Роль</p>
                  <p className="font-semibold">{user.isAdmin ? 'Администратор' : 'Пользователь'}</p>
                </div>
              </div>

              {user.twoFactorEnabled && (
                <div className="flex items-center space-x-3 p-3 bg-green-50 dark:bg-green-900/20 rounded">
                  <Shield className="w-5 h-5 text-green-600" />
                  <div>
                    <p className="text-sm text-green-600 dark:text-green-400">2FA включен</p>
                    <p className="text-xs text-gray-600 dark:text-gray-400">Ваш аккаунт защищён</p>
                  </div>
                </div>
              )}

              <div className="flex items-center space-x-3 p-3 bg-purple-50 dark:bg-purple-900/20 rounded">
                <Calendar className="w-5 h-5 text-purple-600" />
                <div>
                  <p className="text-sm text-purple-600 dark:text-purple-400">Подписка</p>
                  <p className="text-xs text-gray-600 dark:text-gray-400">
                    <a 
                      href="/dashboard/subscription" 
                      className="hover:underline text-purple-600 dark:text-purple-400"
                    >
                      Управление подпиской →
                    </a>
                  </p>
                </div>
              </div>
            </div>
          </motion.div>

          {/* Редактирование профиля */}
          <motion.div
            initial={{ opacity: 0, x: 20 }}
            animate={{ opacity: 1, x: 0 }}
            className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg"
          >
            <h2 className="text-xl font-semibold mb-4">Редактировать профиль</h2>

            <form onSubmit={handleUpdateProfile} className="space-y-4">
              <div>
                <label className="block mb-2 text-gray-700 dark:text-gray-300">
                  Имя пользователя
                </label>
                <input
                  type="text"
                  value={formData.username}
                  onChange={(e) => setFormData({ ...formData, username: e.target.value })}
                  className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
                />
              </div>

              <div>
                <label className="block mb-2 text-gray-700 dark:text-gray-300">
                  Email
                </label>
                <input
                  type="email"
                  value={formData.email}
                  onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                  className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
                />
              </div>

              <button
                type="submit"
                disabled={loading}
                className="w-full flex items-center justify-center space-x-2 px-4 py-2 bg-purple-600 hover:bg-purple-700 rounded-lg text-white disabled:opacity-50"
              >
                <Save className="w-5 h-5" />
                <span>{loading ? 'Сохранение...' : 'Сохранить'}</span>
              </button>
            </form>
          </motion.div>

          {/* Изменение пароля */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2 }}
            className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg lg:col-span-2"
          >
            <h2 className="text-xl font-semibold mb-4">Изменить пароль</h2>

            <form onSubmit={handleChangePassword} className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <label className="block mb-2 text-gray-700 dark:text-gray-300">
                    Текущий пароль
                  </label>
                  <input
                    type="password"
                    value={formData.currentPassword}
                    onChange={(e) => setFormData({ ...formData, currentPassword: e.target.value })}
                    required
                    className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
                  />
                </div>

                <div>
                  <label className="block mb-2 text-gray-700 dark:text-gray-300">
                    Новый пароль
                  </label>
                  <input
                    type="password"
                    value={formData.newPassword}
                    onChange={(e) => setFormData({ ...formData, newPassword: e.target.value })}
                    required
                    className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
                  />
                </div>

                <div>
                  <label className="block mb-2 text-gray-700 dark:text-gray-300">
                    Подтвердите пароль
                  </label>
                  <input
                    type="password"
                    value={formData.confirmPassword}
                    onChange={(e) => setFormData({ ...formData, confirmPassword: e.target.value })}
                    required
                    className="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
                  />
                </div>
              </div>

              <button
                type="submit"
                disabled={loading}
                className="px-6 py-2 bg-blue-600 hover:bg-blue-700 rounded-lg text-white disabled:opacity-50"
              >
                {loading ? 'Изменение...' : 'Изменить пароль'}
              </button>
            </form>
          </motion.div>
        </div>
      </motion.div>
    </div>
  );
}