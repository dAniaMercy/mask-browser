'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { useAuthStore } from '@/store/authStore';
import { useTranslation } from '@/hooks/useTranslation';
import Layout from '@/components/Layout';
import { Server, Users, DollarSign, Activity } from 'lucide-react';

export default function AdminPanel() {
  const router = useRouter();
  const { isAuthenticated, isAdmin } = useAuthStore();
  const { t } = useTranslation();

  useEffect(() => {
    if (!isAuthenticated || !isAdmin) {
      router.push('/dashboard');
    }
  }, [isAuthenticated, isAdmin, router]);

  if (!isAuthenticated || !isAdmin) {
    return null;
  }

  const stats = [
    { icon: Server, label: 'Серверы', value: 0, color: 'text-blue-500' },
    { icon: Users, label: 'Пользователи', value: 0, color: 'text-green-500' },
    { icon: DollarSign, label: 'Платежи', value: 0, color: 'text-yellow-500' },
    { icon: Activity, label: 'Активные профили', value: 0, color: 'text-purple-500' },
  ];

  return (
    <Layout>
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
      >
        <h1 className="text-3xl font-bold mb-8">{t('common.admin')}</h1>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          {stats.map((stat, index) => (
            <motion.div
              key={index}
              initial={{ opacity: 0, scale: 0.9 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ delay: index * 0.1 }}
              className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg"
            >
              <div className="flex items-center space-x-4">
                <stat.icon className={`w-12 h-12 ${stat.color}`} />
                <div>
                  <h3 className="text-lg font-semibold">{stat.label}</h3>
                  <p className="text-2xl font-bold">{stat.value}</p>
                </div>
              </div>
            </motion.div>
          ))}
        </div>

        <div className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg">
          <h2 className="text-xl font-semibold mb-4">Мониторинг</h2>
          <p className="text-gray-600 dark:text-gray-400">
            Здесь будет отображаться статистика и метрики системы
          </p>
        </div>
      </motion.div>
    </Layout>
  );
}

