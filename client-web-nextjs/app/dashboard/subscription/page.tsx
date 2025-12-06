'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { useAuthStore } from '@/store/authStore';
import { useTranslation } from '@/hooks/useTranslation';
import { Check, X, Crown, Zap, Rocket, Building2 } from 'lucide-react';
import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://109.172.101.73:5050';

interface SubscriptionInfo {
  tier: string;
  tierValue: number;
  maxProfiles: number;
  isActive: boolean;
  startDate: string;
  endDate: string | null;
}

const subscriptionTiers = [
  {
    id: 0,
    name: 'Free',
    label: 'Бесплатный',
    maxProfiles: 3,
    price: 0,
    features: [
      '3 профиля браузера',
      'Базовые функции',
      'Поддержка сообщества'
    ],
    icon: Zap,
    color: 'gray'
  },
  {
    id: 1,
    name: 'Basic',
    label: 'Базовый',
    maxProfiles: 10,
    price: 9.99,
    features: [
      '10 профилей браузера',
      'Все функции Free',
      'Приоритетная поддержка',
      'Расширенные настройки'
    ],
    icon: Crown,
    color: 'blue'
  },
  {
    id: 2,
    name: 'Pro',
    label: 'Профессиональный',
    maxProfiles: 50,
    price: 29.99,
    features: [
      '50 профилей браузера',
      'Все функции Basic',
      'Приоритетная поддержка 24/7',
      'API доступ',
      'Расширенная аналитика'
    ],
    icon: Rocket,
    color: 'purple'
  },
  {
    id: 3,
    name: 'Enterprise',
    label: 'Корпоративный',
    maxProfiles: 999,
    price: 99.99,
    features: [
      'Неограниченное количество профилей',
      'Все функции Pro',
      'Персональный менеджер',
      'Кастомные интеграции',
      'SLA гарантии'
    ],
    icon: Building2,
    color: 'gold'
  }
];

export default function SubscriptionPage() {
  const router = useRouter();
  const { t } = useTranslation();
  const { isAuthenticated, user, token } = useAuthStore();
  const [loading, setLoading] = useState(true);
  const [subscription, setSubscription] = useState<SubscriptionInfo | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/login');
      return;
    }
    fetchSubscription();
  }, [isAuthenticated, router]);

  const fetchSubscription = async () => {
    try {
      setLoading(true);
      const response = await axios.get(`${API_URL}/api/auth/me`, {
        headers: {
          Authorization: `Bearer ${token}`
        }
      });
      
      if (response.data.subscription) {
        setSubscription(response.data.subscription);
      }
    } catch (err: any) {
      console.error('Ошибка загрузки подписки:', err);
      setError('Не удалось загрузить информацию о подписке');
    } finally {
      setLoading(false);
    }
  };

  const handleUpgrade = async (tierId: number) => {
    // TODO: Реализовать оплату и обновление подписки
    alert(`Функция оплаты подписки ${subscriptionTiers[tierId].label} будет реализована позже`);
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <div className="w-8 h-8 border-4 border-purple-600 border-t-transparent rounded-full animate-spin mx-auto mb-4" />
          <p className="text-gray-400">Загрузка...</p>
        </div>
      </div>
    );
  }

  const currentTier = subscriptionTiers.find(t => t.id === subscription?.tierValue ?? 0) || subscriptionTiers[0];

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-900 dark:to-black p-4">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="max-w-6xl mx-auto"
      >
        <h1 className="text-3xl font-bold mb-2 text-purple-600 dark:text-purple-400">
          Управление подпиской
        </h1>
        <p className="text-gray-600 dark:text-gray-400 mb-8">
          Выберите подходящий план для ваших нужд
        </p>

        {error && (
          <div className="mb-4 p-4 bg-red-500/10 border border-red-500/50 rounded-lg text-red-400">
            {error}
          </div>
        )}

        {/* Текущая подписка */}
        {subscription && (
          <div className="mb-8 p-6 bg-gradient-to-r from-purple-600 to-blue-600 rounded-lg text-white">
            <h2 className="text-xl font-semibold mb-2">Текущая подписка</h2>
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{currentTier.label}</p>
                <p className="text-purple-100">
                  {subscription.maxProfiles} профилей • 
                  {subscription.isActive ? ' Активна' : ' Неактивна'}
                </p>
                {subscription.endDate && (
                  <p className="text-sm text-purple-100 mt-1">
                    Действует до: {new Date(subscription.endDate).toLocaleDateString('ru-RU')}
                  </p>
                )}
              </div>
              <currentTier.icon className="w-12 h-12 opacity-80" />
            </div>
          </div>
        )}

        {/* Планы подписки */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          {subscriptionTiers.map((tier) => {
            const Icon = tier.icon;
            const isCurrent = subscription?.tierValue === tier.id;
            const isUpgrade = (subscription?.tierValue ?? 0) < tier.id;
            
            return (
              <motion.div
                key={tier.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: tier.id * 0.1 }}
                className={`relative bg-white dark:bg-gray-900 p-6 rounded-lg border-2 ${
                  isCurrent
                    ? 'border-purple-500 shadow-lg'
                    : 'border-gray-200 dark:border-gray-800'
                } shadow-lg`}
              >
                {isCurrent && (
                  <div className="absolute -top-3 left-1/2 transform -translate-x-1/2 bg-purple-600 text-white px-4 py-1 rounded-full text-sm font-semibold">
                    Текущий план
                  </div>
                )}
                
                <div className="text-center mb-6">
                  <div className={`inline-flex items-center justify-center w-16 h-16 rounded-full bg-${tier.color}-100 dark:bg-${tier.color}-900/30 mb-4`}>
                    <Icon className={`w-8 h-8 text-${tier.color}-600 dark:text-${tier.color}-400`} />
                  </div>
                  <h3 className="text-2xl font-bold mb-2">{tier.label}</h3>
                  <div className="mb-4">
                    <span className="text-3xl font-bold">${tier.price}</span>
                    {tier.price > 0 && <span className="text-gray-500">/мес</span>}
                  </div>
                </div>

                <ul className="space-y-3 mb-6">
                  {tier.features.map((feature, idx) => (
                    <li key={idx} className="flex items-start space-x-2">
                      <Check className="w-5 h-5 text-green-500 flex-shrink-0 mt-0.5" />
                      <span className="text-gray-700 dark:text-gray-300">{feature}</span>
                    </li>
                  ))}
                </ul>

                <button
                  onClick={() => handleUpgrade(tier.id)}
                  disabled={isCurrent || !isUpgrade}
                  className={`w-full py-3 rounded-lg font-semibold transition-colors ${
                    isCurrent
                      ? 'bg-gray-300 dark:bg-gray-700 text-gray-500 cursor-not-allowed'
                      : isUpgrade
                      ? `bg-${tier.color}-600 hover:bg-${tier.color}-700 text-white`
                      : 'bg-gray-300 dark:bg-gray-700 text-gray-500 cursor-not-allowed'
                  }`}
                >
                  {isCurrent ? 'Текущий план' : isUpgrade ? 'Выбрать план' : 'Недоступно'}
                </button>
              </motion.div>
            );
          })}
        </div>
      </motion.div>
    </div>
  );
}

