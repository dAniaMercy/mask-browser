'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { useTranslation } from '@/hooks/useTranslation';
import { useAuthStore } from '@/store/authStore';
import {
  User,
  Mail,
  Shield,
  Calendar,
  Save,
  Settings,
  BarChart3,
  Lock,
  Bell,
  CreditCard,
  Activity,
  Globe,
  Key,
  Eye,
  EyeOff,
  CheckCircle2,
  XCircle,
  Clock,
  TrendingUp,
  Users,
  FileText,
  Wallet
} from 'lucide-react';

interface UserStatistics {
  profiles: {
    total: number;
    byStatus: Record<string, number>;
    createdLast30Days: number;
  };
  payments: {
    total: number;
    completed: number;
    totalSpent: number;
    lastPayment: string | null;
  };
  deposits: {
    total: number;
    completed: number;
    pending: number;
    expired: number;
    totalDeposited: number;
    lastDeposit: string | null;
  };
  recentActivity: Array<{
    profileId: number;
    profileName: string;
    lastStartedAt: string;
    status: string;
  }>;
}

interface UserInfo {
  id: number;
  username: string;
  email: string;
  balance: number;
  createdAt: string;
  lastLoginAt: string | null;
  isActive: boolean;
  isAdmin: boolean;
  twoFactorEnabled: boolean;
  subscription: {
    tier: string;
    maxProfiles: number;
    startDate: string;
    endDate: string | null;
    isActive: boolean;
  } | null;
  stats: {
    totalProfiles: number;
    activeProfiles: number;
    totalPayments: number;
    totalDeposits: number;
    completedDeposits: number;
  };
}

type TabType = 'overview' | 'statistics' | 'settings' | 'security';

export default function ProfilePage() {
  const router = useRouter();
  const { t } = useTranslation();
  const { user, isAuthenticated, token } = useAuthStore();
  const [activeTab, setActiveTab] = useState<TabType>('overview');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [userInfo, setUserInfo] = useState<UserInfo | null>(null);
  const [statistics, setStatistics] = useState<UserStatistics | null>(null);
  const [showPassword, setShowPassword] = useState({
    current: false,
    new: false,
    confirm: false
  });

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
    loadUserData();
  }, [isAuthenticated, router]);

  const loadUserData = async () => {
    try {
      setLoading(true);
      const [userRes, statsRes] = await Promise.all([
        fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5050'}/api/user/me`, {
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
          }
        }),
        fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5050'}/api/user/statistics`, {
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
          }
        })
      ]);

      if (userRes.ok) {
        const userData = await userRes.json();
        setUserInfo(userData);
        setFormData({
          username: userData.username,
          email: userData.email,
          currentPassword: '',
          newPassword: '',
          confirmPassword: '',
        });
      }

      if (statsRes.ok) {
        const statsData = await statsRes.json();
        setStatistics(statsData);
      }
    } catch (err) {
      console.error('Failed to load user data:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleUpdateProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    try {
      setLoading(true);
      const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5050'}/api/user/profile`, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          username: formData.username,
          email: formData.email
        })
      });

      if (response.ok) {
        setSuccess('Профиль успешно обновлен');
        await loadUserData();
      } else {
        const data = await response.json();
        setError(data.message || 'Ошибка при обновлении профиля');
      }
    } catch (err) {
      setError('Ошибка при обновлении профиля');
    } finally {
      setLoading(false);
    }
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

    try {
      setLoading(true);
      const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5050'}/api/user/change-password`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          currentPassword: formData.currentPassword,
          newPassword: formData.newPassword
        })
      });

      if (response.ok) {
        setSuccess('Пароль успешно изменен');
        setFormData({
          ...formData,
          currentPassword: '',
          newPassword: '',
          confirmPassword: ''
        });
      } else {
        const data = await response.json();
        setError(data.message || 'Ошибка при изменении пароля');
      }
    } catch (err) {
      setError('Ошибка при изменении пароля');
    } finally {
      setLoading(false);
    }
  };

  if (!isAuthenticated || !user) {
    return null;
  }

  const tabs = [
    { id: 'overview' as TabType, label: 'Обзор', icon: User },
    { id: 'statistics' as TabType, label: 'Статистика', icon: BarChart3 },
    { id: 'settings' as TabType, label: 'Настройки', icon: Settings },
    { id: 'security' as TabType, label: 'Безопасность', icon: Shield },
  ];

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-900 dark:to-black p-4">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="max-w-7xl mx-auto"
      >
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-4xl font-bold mb-2 text-purple-600 dark:text-purple-400">
            Мой профиль
          </h1>
          <p className="text-gray-600 dark:text-gray-400">
            Управление аккаунтом, настройками и безопасностью
          </p>
        </div>

        {/* Tabs */}
        <div className="flex flex-wrap gap-2 mb-6 border-b border-gray-200 dark:border-gray-800">
          {tabs.map((tab) => {
            const Icon = tab.icon;
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-2 px-6 py-3 font-medium transition-all ${
                  activeTab === tab.id
                    ? 'text-purple-600 dark:text-purple-400 border-b-2 border-purple-600 dark:border-purple-400'
                    : 'text-gray-600 dark:text-gray-400 hover:text-purple-600 dark:hover:text-purple-400'
                }`}
              >
                <Icon className="w-5 h-5" />
                {tab.label}
              </button>
            );
          })}
        </div>

        {/* Messages */}
        {error && (
          <div className="mb-4 p-4 bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300 rounded-lg flex items-center gap-2">
            <XCircle className="w-5 h-5" />
            {error}
          </div>
        )}

        {success && (
          <div className="mb-4 p-4 bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-300 rounded-lg flex items-center gap-2">
            <CheckCircle2 className="w-5 h-5" />
            {success}
          </div>
        )}

        {/* Tab Content */}
        {activeTab === 'overview' && (
          <OverviewTab userInfo={userInfo} loading={loading} />
        )}

        {activeTab === 'statistics' && (
          <StatisticsTab statistics={statistics} loading={loading} />
        )}

        {activeTab === 'settings' && (
          <SettingsTab
            formData={formData}
            setFormData={setFormData}
            handleUpdateProfile={handleUpdateProfile}
            loading={loading}
          />
        )}

        {activeTab === 'security' && (
          <SecurityTab
            formData={formData}
            setFormData={setFormData}
            handleChangePassword={handleChangePassword}
            showPassword={showPassword}
            setShowPassword={setShowPassword}
            loading={loading}
            twoFactorEnabled={userInfo?.twoFactorEnabled || false}
          />
        )}
      </motion.div>
    </div>
  );
}

// Overview Tab Component
function OverviewTab({ userInfo, loading }: { userInfo: UserInfo | null; loading: boolean }) {
  if (loading || !userInfo) {
    return <div className="text-center py-12">Загрузка...</div>;
  }

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
      {/* User Info Card */}
      <div className="lg:col-span-2 bg-white dark:bg-gray-900 p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-lg">
        <h2 className="text-2xl font-semibold mb-6 flex items-center gap-2">
          <User className="w-6 h-6 text-purple-600" />
          Информация об аккаунте
        </h2>

        <div className="space-y-4">
          <div className="flex items-center gap-4 p-4 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <User className="w-5 h-5 text-gray-500" />
            <div className="flex-1">
              <p className="text-sm text-gray-500 dark:text-gray-400">Имя пользователя</p>
              <p className="font-semibold text-lg">{userInfo.username}</p>
            </div>
          </div>

          <div className="flex items-center gap-4 p-4 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <Mail className="w-5 h-5 text-gray-500" />
            <div className="flex-1">
              <p className="text-sm text-gray-500 dark:text-gray-400">Email</p>
              <p className="font-semibold text-lg">{userInfo.email}</p>
            </div>
          </div>

          <div className="flex items-center gap-4 p-4 bg-purple-50 dark:bg-purple-900/20 rounded-lg">
            <Wallet className="w-5 h-5 text-purple-600" />
            <div className="flex-1">
              <p className="text-sm text-purple-600 dark:text-purple-400">Баланс</p>
              <p className="font-bold text-2xl text-purple-600 dark:text-purple-400">
                ${userInfo.balance.toFixed(2)}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-4 p-4 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <Shield className="w-5 h-5 text-gray-500" />
            <div className="flex-1">
              <p className="text-sm text-gray-500 dark:text-gray-400">Роль</p>
              <p className="font-semibold">{userInfo.isAdmin ? 'Администратор' : 'Пользователь'}</p>
            </div>
          </div>

          {userInfo.twoFactorEnabled && (
            <div className="flex items-center gap-4 p-4 bg-green-50 dark:bg-green-900/20 rounded-lg">
              <Shield className="w-5 h-5 text-green-600" />
              <div className="flex-1">
                <p className="text-sm text-green-600 dark:text-green-400">2FA включен</p>
                <p className="text-xs text-gray-600 dark:text-gray-400">Ваш аккаунт защищён</p>
              </div>
            </div>
          )}

          <div className="flex items-center gap-4 p-4 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <Calendar className="w-5 h-5 text-gray-500" />
            <div className="flex-1">
              <p className="text-sm text-gray-500 dark:text-gray-400">Дата регистрации</p>
              <p className="font-semibold">
                {new Date(userInfo.createdAt).toLocaleDateString('ru-RU', {
                  year: 'numeric',
                  month: 'long',
                  day: 'numeric'
                })}
              </p>
            </div>
          </div>

          {userInfo.lastLoginAt && (
            <div className="flex items-center gap-4 p-4 bg-gray-50 dark:bg-gray-800 rounded-lg">
              <Clock className="w-5 h-5 text-gray-500" />
              <div className="flex-1">
                <p className="text-sm text-gray-500 dark:text-gray-400">Последний вход</p>
                <p className="font-semibold">
                  {new Date(userInfo.lastLoginAt).toLocaleString('ru-RU')}
                </p>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Subscription Card */}
      <div className="bg-white dark:bg-gray-900 p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-lg">
        <h2 className="text-xl font-semibold mb-4 flex items-center gap-2">
          <CreditCard className="w-5 h-5 text-purple-600" />
          Подписка
        </h2>

        {userInfo.subscription ? (
          <div className="space-y-4">
            <div className="p-4 bg-gradient-to-r from-purple-500 to-purple-600 rounded-lg text-white">
              <p className="text-sm opacity-90">Текущий план</p>
              <p className="text-2xl font-bold capitalize">{userInfo.subscription.tier}</p>
            </div>

            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Макс. профилей:</span>
                <span className="font-semibold">{userInfo.subscription.maxProfiles}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Статус:</span>
                <span className={`font-semibold ${userInfo.subscription.isActive ? 'text-green-600' : 'text-red-600'}`}>
                  {userInfo.subscription.isActive ? 'Активна' : 'Неактивна'}
                </span>
              </div>
              {userInfo.subscription.endDate && (
                <div className="flex justify-between">
                  <span className="text-gray-600 dark:text-gray-400">До:</span>
                  <span className="font-semibold">
                    {new Date(userInfo.subscription.endDate).toLocaleDateString('ru-RU')}
                  </span>
                </div>
              )}
            </div>

            <a
              href="/dashboard/subscription"
              className="block w-full text-center px-4 py-2 bg-purple-600 hover:bg-purple-700 rounded-lg text-white transition-colors"
            >
              Управление подпиской →
            </a>
          </div>
        ) : (
          <div className="text-center py-8">
            <p className="text-gray-600 dark:text-gray-400 mb-4">Подписка не активна</p>
            <a
              href="/dashboard/subscription"
              className="inline-block px-4 py-2 bg-purple-600 hover:bg-purple-700 rounded-lg text-white transition-colors"
            >
              Выбрать план →
            </a>
          </div>
        )}
      </div>

      {/* Quick Stats */}
      <div className="lg:col-span-3 grid grid-cols-1 md:grid-cols-4 gap-4">
        <StatCard
          icon={Users}
          label="Всего профилей"
          value={userInfo.stats.totalProfiles}
          color="blue"
        />
        <StatCard
          icon={Activity}
          label="Активных"
          value={userInfo.stats.activeProfiles}
          color="green"
        />
        <StatCard
          icon={FileText}
          label="Платежей"
          value={userInfo.stats.totalPayments}
          color="purple"
        />
        <StatCard
          icon={TrendingUp}
          label="Пополнений"
          value={userInfo.stats.completedDeposits}
          color="orange"
        />
      </div>
    </div>
  );
}

// Statistics Tab Component
function StatisticsTab({ statistics, loading }: { statistics: UserStatistics | null; loading: boolean }) {
  if (loading || !statistics) {
    return <div className="text-center py-12">Загрузка статистики...</div>;
  }

  return (
    <div className="space-y-6">
      {/* Profile Statistics */}
      <div className="bg-white dark:bg-gray-900 p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-lg">
        <h2 className="text-2xl font-semibold mb-6 flex items-center gap-2">
          <Users className="w-6 h-6 text-purple-600" />
          Статистика профилей
        </h2>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
          <div className="p-4 bg-blue-50 dark:bg-blue-900/20 rounded-lg">
            <p className="text-sm text-blue-600 dark:text-blue-400 mb-1">Всего профилей</p>
            <p className="text-3xl font-bold text-blue-600">{statistics.profiles.total}</p>
          </div>
          <div className="p-4 bg-green-50 dark:bg-green-900/20 rounded-lg">
            <p className="text-sm text-green-600 dark:text-green-400 mb-1">Активных</p>
            <p className="text-3xl font-bold text-green-600">
              {statistics.profiles.byStatus['Running'] || 0}
            </p>
          </div>
          <div className="p-4 bg-purple-50 dark:bg-purple-900/20 rounded-lg">
            <p className="text-sm text-purple-600 dark:text-purple-400 mb-1">Создано за 30 дней</p>
            <p className="text-3xl font-bold text-purple-600">
              {statistics.profiles.createdLast30Days}
            </p>
          </div>
        </div>

        <div className="mt-4">
          <h3 className="font-semibold mb-2">По статусам:</h3>
          <div className="flex flex-wrap gap-2">
            {Object.entries(statistics.profiles.byStatus).map(([status, count]) => (
              <div key={status} className="px-3 py-1 bg-gray-100 dark:bg-gray-800 rounded">
                <span className="text-sm font-medium">{status}: </span>
                <span className="font-bold">{count}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Payment Statistics */}
      <div className="bg-white dark:bg-gray-900 p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-lg">
        <h2 className="text-2xl font-semibold mb-6 flex items-center gap-2">
          <CreditCard className="w-6 h-6 text-purple-600" />
          Статистика платежей
        </h2>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="p-4 bg-blue-50 dark:bg-blue-900/20 rounded-lg">
            <p className="text-sm text-blue-600 dark:text-blue-400 mb-1">Всего платежей</p>
            <p className="text-3xl font-bold text-blue-600">{statistics.payments.total}</p>
          </div>
          <div className="p-4 bg-green-50 dark:bg-green-900/20 rounded-lg">
            <p className="text-sm text-green-600 dark:text-green-400 mb-1">Завершено</p>
            <p className="text-3xl font-bold text-green-600">{statistics.payments.completed}</p>
          </div>
          <div className="p-4 bg-purple-50 dark:bg-purple-900/20 rounded-lg">
            <p className="text-sm text-purple-600 dark:text-purple-400 mb-1">Потрачено</p>
            <p className="text-3xl font-bold text-purple-600">
              ${statistics.payments.totalSpent.toFixed(2)}
            </p>
          </div>
        </div>

        {statistics.payments.lastPayment && (
          <div className="mt-4 text-sm text-gray-600 dark:text-gray-400">
            Последний платеж: {new Date(statistics.payments.lastPayment).toLocaleString('ru-RU')}
          </div>
        )}
      </div>

      {/* Deposit Statistics */}
      <div className="bg-white dark:bg-gray-900 p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-lg">
        <h2 className="text-2xl font-semibold mb-6 flex items-center gap-2">
          <TrendingUp className="w-6 h-6 text-purple-600" />
          Статистика пополнений
        </h2>

        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div className="p-4 bg-blue-50 dark:bg-blue-900/20 rounded-lg">
            <p className="text-sm text-blue-600 dark:text-blue-400 mb-1">Всего</p>
            <p className="text-3xl font-bold text-blue-600">{statistics.deposits.total}</p>
          </div>
          <div className="p-4 bg-green-50 dark:bg-green-900/20 rounded-lg">
            <p className="text-sm text-green-600 dark:text-green-400 mb-1">Завершено</p>
            <p className="text-3xl font-bold text-green-600">{statistics.deposits.completed}</p>
          </div>
          <div className="p-4 bg-yellow-50 dark:bg-yellow-900/20 rounded-lg">
            <p className="text-sm text-yellow-600 dark:text-yellow-400 mb-1">Ожидание</p>
            <p className="text-3xl font-bold text-yellow-600">{statistics.deposits.pending}</p>
          </div>
          <div className="p-4 bg-purple-50 dark:bg-purple-900/20 rounded-lg">
            <p className="text-sm text-purple-600 dark:text-purple-400 mb-1">Пополнено</p>
            <p className="text-3xl font-bold text-purple-600">
              ${statistics.deposits.totalDeposited.toFixed(2)}
            </p>
          </div>
        </div>
      </div>

      {/* Recent Activity */}
      {statistics.recentActivity.length > 0 && (
        <div className="bg-white dark:bg-gray-900 p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-lg">
          <h2 className="text-2xl font-semibold mb-6 flex items-center gap-2">
            <Activity className="w-6 h-6 text-purple-600" />
            Недавняя активность
          </h2>

          <div className="space-y-2">
            {statistics.recentActivity.map((activity) => (
              <div
                key={activity.profileId}
                className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-800 rounded-lg"
              >
                <div>
                  <p className="font-semibold">{activity.profileName}</p>
                  <p className="text-sm text-gray-600 dark:text-gray-400">
                    {new Date(activity.lastStartedAt).toLocaleString('ru-RU')}
                  </p>
                </div>
                <span className={`px-3 py-1 rounded text-sm ${
                  activity.status === 'Running' 
                    ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300'
                    : 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300'
                }`}>
                  {activity.status}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// Settings Tab Component
function SettingsTab({
  formData,
  setFormData,
  handleUpdateProfile,
  loading
}: {
  formData: any;
  setFormData: any;
  handleUpdateProfile: (e: React.FormEvent) => void;
  loading: boolean;
}) {
  return (
    <div className="bg-white dark:bg-gray-900 p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-lg">
      <h2 className="text-2xl font-semibold mb-6 flex items-center gap-2">
        <Settings className="w-6 h-6 text-purple-600" />
        Настройки профиля
      </h2>

      <form onSubmit={handleUpdateProfile} className="space-y-6">
        <div>
          <label className="block mb-2 text-gray-700 dark:text-gray-300 font-medium">
            Имя пользователя
          </label>
          <input
            type="text"
            value={formData.username}
            onChange={(e) => setFormData({ ...formData, username: e.target.value })}
            className="w-full px-4 py-3 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
            required
          />
        </div>

        <div>
          <label className="block mb-2 text-gray-700 dark:text-gray-300 font-medium">
            Email
          </label>
          <input
            type="email"
            value={formData.email}
            onChange={(e) => setFormData({ ...formData, email: e.target.value })}
            className="w-full px-4 py-3 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100"
            required
          />
        </div>

        <button
          type="submit"
          disabled={loading}
          className="w-full flex items-center justify-center gap-2 px-6 py-3 bg-purple-600 hover:bg-purple-700 rounded-lg text-white font-semibold disabled:opacity-50 transition-colors"
        >
          <Save className="w-5 h-5" />
          {loading ? 'Сохранение...' : 'Сохранить изменения'}
        </button>
      </form>
    </div>
  );
}

// Security Tab Component
function SecurityTab({
  formData,
  setFormData,
  handleChangePassword,
  showPassword,
  setShowPassword,
  loading,
  twoFactorEnabled
}: {
  formData: any;
  setFormData: any;
  handleChangePassword: (e: React.FormEvent) => void;
  showPassword: any;
  setShowPassword: any;
  loading: boolean;
  twoFactorEnabled: boolean;
}) {
  return (
    <div className="space-y-6">
      {/* Change Password */}
      <div className="bg-white dark:bg-gray-900 p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-lg">
        <h2 className="text-2xl font-semibold mb-6 flex items-center gap-2">
          <Lock className="w-6 h-6 text-purple-600" />
          Изменить пароль
        </h2>

        <form onSubmit={handleChangePassword} className="space-y-4">
          <div>
            <label className="block mb-2 text-gray-700 dark:text-gray-300 font-medium">
              Текущий пароль
            </label>
            <div className="relative">
              <input
                type={showPassword.current ? 'text' : 'password'}
                value={formData.currentPassword}
                onChange={(e) => setFormData({ ...formData, currentPassword: e.target.value })}
                className="w-full px-4 py-3 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100 pr-12"
                required
              />
              <button
                type="button"
                onClick={() => setShowPassword({ ...showPassword, current: !showPassword.current })}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700"
              >
                {showPassword.current ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
              </button>
            </div>
          </div>

          <div>
            <label className="block mb-2 text-gray-700 dark:text-gray-300 font-medium">
              Новый пароль
            </label>
            <div className="relative">
              <input
                type={showPassword.new ? 'text' : 'password'}
                value={formData.newPassword}
                onChange={(e) => setFormData({ ...formData, newPassword: e.target.value })}
                className="w-full px-4 py-3 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100 pr-12"
                required
                minLength={6}
              />
              <button
                type="button"
                onClick={() => setShowPassword({ ...showPassword, new: !showPassword.new })}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700"
              >
                {showPassword.new ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
              </button>
            </div>
            <p className="text-sm text-gray-500 mt-1">Минимум 6 символов</p>
          </div>

          <div>
            <label className="block mb-2 text-gray-700 dark:text-gray-300 font-medium">
              Подтвердите новый пароль
            </label>
            <div className="relative">
              <input
                type={showPassword.confirm ? 'text' : 'password'}
                value={formData.confirmPassword}
                onChange={(e) => setFormData({ ...formData, confirmPassword: e.target.value })}
                className="w-full px-4 py-3 bg-gray-50 dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 text-gray-900 dark:text-gray-100 pr-12"
                required
                minLength={6}
              />
              <button
                type="button"
                onClick={() => setShowPassword({ ...showPassword, confirm: !showPassword.confirm })}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700"
              >
                {showPassword.confirm ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
              </button>
            </div>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full px-6 py-3 bg-blue-600 hover:bg-blue-700 rounded-lg text-white font-semibold disabled:opacity-50 transition-colors"
          >
            {loading ? 'Изменение...' : 'Изменить пароль'}
          </button>
        </form>
      </div>

      {/* 2FA Section */}
      <div className="bg-white dark:bg-gray-900 p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-lg">
        <h2 className="text-2xl font-semibold mb-6 flex items-center gap-2">
          <Shield className="w-6 h-6 text-purple-600" />
          Двухфакторная аутентификация
        </h2>

        <div className="flex items-center justify-between p-4 bg-gray-50 dark:bg-gray-800 rounded-lg">
          <div>
            <p className="font-semibold">2FA {twoFactorEnabled ? 'включена' : 'отключена'}</p>
            <p className="text-sm text-gray-600 dark:text-gray-400">
              {twoFactorEnabled
                ? 'Ваш аккаунт защищен двухфакторной аутентификацией'
                : 'Включите 2FA для дополнительной защиты аккаунта'}
            </p>
          </div>
          <button
            className={`px-4 py-2 rounded-lg font-semibold transition-colors ${
              twoFactorEnabled
                ? 'bg-red-600 hover:bg-red-700 text-white'
                : 'bg-green-600 hover:bg-green-700 text-white'
            }`}
          >
            {twoFactorEnabled ? 'Отключить' : 'Включить'}
          </button>
        </div>
      </div>
    </div>
  );
}

// Stat Card Component
function StatCard({
  icon: Icon,
  label,
  value,
  color
}: {
  icon: any;
  label: string;
  value: number;
  color: string;
}) {
  const colorClasses = {
    blue: 'bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400',
    green: 'bg-green-50 dark:bg-green-900/20 text-green-600 dark:text-green-400',
    purple: 'bg-purple-50 dark:bg-purple-900/20 text-purple-600 dark:text-purple-400',
    orange: 'bg-orange-50 dark:bg-orange-900/20 text-orange-600 dark:text-orange-400',
  };

  return (
    <div className={`p-4 rounded-xl ${colorClasses[color as keyof typeof colorClasses]}`}>
      <div className="flex items-center gap-3 mb-2">
        <Icon className="w-6 h-6" />
        <p className="text-sm font-medium opacity-80">{label}</p>
      </div>
      <p className="text-3xl font-bold">{value}</p>
    </div>
  );
}
