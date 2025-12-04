'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { useAuthStore } from '@/store/authStore';
import { useTranslation } from '@/hooks/useTranslation';
import { 
  Server, Users, DollarSign, Activity, 
  Trash2, Edit, CheckCircle, XCircle,
  TrendingUp, Database, Shield
} from 'lucide-react';
import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://109.172.101.73:5050';

interface User {
  id: number;
  username: string;
  email: string;
  isAdmin: boolean;
  isActive: boolean;
  createdAt: string;
  subscription?: {
    tier: number;
    maxProfiles: number;
  };
}

interface ServerNode {
  id: number;
  name: string;
  ipAddress: string;
  maxContainers: number;
  activeContainers: number;
  isHealthy: boolean;
  cpuUsage: number;
  memoryUsage: number;
  lastHealthCheck: string;
}

interface AdminStats {
  totalUsers: number;
  activeUsers: number;
  totalProfiles: number;
  runningProfiles: number;
  totalNodes: number;
  healthyNodes: number;
  totalRevenue: number;
}

export default function AdminPanel() {
  const router = useRouter();
  const { isAuthenticated, isAdmin, token } = useAuthStore();
  const { t } = useTranslation();

  const [activeTab, setActiveTab] = useState<'stats' | 'users' | 'servers' | 'payments'>('stats');
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [users, setUsers] = useState<User[]>([]);
  const [servers, setServers] = useState<ServerNode[]>([]);
  const [loading, setLoading] = useState(false);

  const subscriptionTierLabels: Record<number, string> = {
    0: 'Free',
    1: 'Basic',
    2: 'Pro',
    3: 'Enterprise',
  };

  useEffect(() => {
    if (!isAuthenticated || !isAdmin) {
      router.push('/dashboard');
    } else {
      loadStats();
    }
  }, [isAuthenticated, isAdmin, router]);

  const loadStats = async () => {
    setLoading(true);
    try {
      const response = await axios.get(`${API_URL}/api/admin/stats`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setStats(response.data);
    } catch (error) {
      console.error('Error loading stats:', error);
    } finally {
      setLoading(false);
    }
  };

  const loadUsers = async () => {
    setLoading(true);
    try {
      const response = await axios.get(`${API_URL}/api/admin/users`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setUsers(response.data);
    } catch (error) {
      console.error('Error loading users:', error);
    } finally {
      setLoading(false);
    }
  };

  const loadServers = async () => {
    setLoading(true);
    try {
      const response = await axios.get(`${API_URL}/api/admin/servers`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setServers(response.data);
    } catch (error) {
      console.error('Error loading servers:', error);
    } finally {
      setLoading(false);
    }
  };

  const toggleUserStatus = async (userId: number, isActive: boolean) => {
    try {
      await axios.put(
        `${API_URL}/api/admin/users/${userId}/status`,
        { isActive: !isActive },
        { headers: { Authorization: `Bearer ${token}` } }
      );
      loadUsers();
    } catch (error) {
      console.error('Error toggling user status:', error);
    }
  };

  const deleteUser = async (userId: number) => {
    if (!confirm('Вы уверены, что хотите удалить этого пользователя?')) {
      return;
    }
    try {
      await axios.delete(`${API_URL}/api/admin/users/${userId}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      loadUsers();
    } catch (error) {
      console.error('Error deleting user:', error);
    }
  };

  useEffect(() => {
    if (activeTab === 'users') loadUsers();
    if (activeTab === 'servers') loadServers();
  }, [activeTab]);

  if (!isAuthenticated || !isAdmin) {
    return null;
  }

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-900 dark:to-black p-4">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="max-w-7xl mx-auto"
      >
        <h1 className="text-3xl font-bold mb-8 text-purple-600 dark:text-purple-400">
          {t('common.admin')} - Панель управления
        </h1>

        {/* Tabs */}
        <div className="flex space-x-4 mb-8 overflow-x-auto">
          {[
            { key: 'stats', label: 'Статистика', icon: TrendingUp },
            { key: 'users', label: 'Пользователи', icon: Users },
            { key: 'servers', label: 'Серверы', icon: Server },
            { key: 'payments', label: 'Платежи', icon: DollarSign },
          ].map((tab) => (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key as any)}
              className={`flex items-center space-x-2 px-6 py-3 rounded-lg transition-colors ${
                activeTab === tab.key
                  ? 'bg-purple-600 text-white'
                  : 'bg-gray-200 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-700'
              }`}
            >
              <tab.icon className="w-5 h-5" />
              <span>{tab.label}</span>
            </button>
          ))}
        </div>

        {/* Stats Tab */}
        {activeTab === 'stats' && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="space-y-6"
          >
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              {[
                { icon: Users, label: 'Всего пользователей', value: stats?.totalUsers || 0, color: 'text-blue-500' },
                { icon: Activity, label: 'Активных пользователей', value: stats?.activeUsers || 0, color: 'text-green-500' },
                { icon: Database, label: 'Всего профилей', value: stats?.totalProfiles || 0, color: 'text-purple-500' },
                { icon: Server, label: 'Активные профили', value: stats?.runningProfiles || 0, color: 'text-orange-500' },
              ].map((stat, index) => (
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
                      <h3 className="text-sm font-medium text-gray-600 dark:text-gray-400">{stat.label}</h3>
                      <p className="text-3xl font-bold">{stat.value}</p>
                    </div>
                  </div>
                </motion.div>
              ))}
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <div className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg">
                <h2 className="text-xl font-semibold mb-4 flex items-center space-x-2">
                  <Server className="w-6 h-6 text-purple-600" />
                  <span>Серверы</span>
                </h2>
                <div className="space-y-2">
                  <p>Всего нод: <span className="font-bold">{stats?.totalNodes || 0}</span></p>
                  <p>Здоровых нод: <span className="font-bold text-green-600">{stats?.healthyNodes || 0}</span></p>
                </div>
              </div>

              <div className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg">
                <h2 className="text-xl font-semibold mb-4 flex items-center space-x-2">
                  <DollarSign className="w-6 h-6 text-purple-600" />
                  <span>Финансы</span>
                </h2>
                <p>Общий доход: <span className="font-bold text-green-600">${stats?.totalRevenue || 0}</span></p>
              </div>
            </div>
          </motion.div>
        )}

        {/* Users Tab */}
        {activeTab === 'users' && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg"
          >
            <h2 className="text-xl font-semibold mb-4">Управление пользователями</h2>
            {loading ? (
              <p>Загрузка...</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead>
                    <tr className="border-b border-gray-200 dark:border-gray-800">
                      <th className="text-left p-3">ID</th>
                      <th className="text-left p-3">Имя</th>
                      <th className="text-left p-3">Email</th>
                      <th className="text-left p-3">Роль</th>
                      <th className="text-left p-3">Подписка</th>
                      <th className="text-left p-3">Статус</th>
                      <th className="text-left p-3">Действия</th>
                    </tr>
                  </thead>
                  <tbody>
                    {users.map((user) => (
                      <tr key={user.id} className="border-b border-gray-100 dark:border-gray-800">
                        <td className="p-3">{user.id}</td>
                        <td className="p-3">{user.username}</td>
                        <td className="p-3">{user.email}</td>
                        <td className="p-3">
                          <span className={`px-2 py-1 rounded text-xs ${user.isAdmin ? 'bg-purple-600 text-white' : 'bg-gray-200 dark:bg-gray-700'}`}>
                            {user.isAdmin ? 'Админ' : 'Пользователь'}
                          </span>
                        </td>
                        <td className="p-3">
                          {user.subscription
                            ? `${subscriptionTierLabels[user.subscription.tier] ?? user.subscription.tier} (${user.subscription.maxProfiles})`
                            : 'Free (0)'}
                        </td>
                        <td className="p-3">
                          {user.isActive ? (
                            <CheckCircle className="w-5 h-5 text-green-500" />
                          ) : (
                            <XCircle className="w-5 h-5 text-red-500" />
                          )}
                        </td>
                        <td className="p-3 flex space-x-2">
                          <button
                            onClick={() => toggleUserStatus(user.id, user.isActive)}
                            className="p-2 hover:bg-gray-100 dark:hover:bg-gray-800 rounded"
                            title={user.isActive ? 'Деактивировать' : 'Активировать'}
                          >
                            <Shield className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => deleteUser(user.id)}
                            className="p-2 hover:bg-red-100 dark:hover:bg-red-900/20 rounded"
                            title="Удалить"
                          >
                            <Trash2 className="w-4 h-4 text-red-600" />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </motion.div>
        )}

        {/* Servers Tab */}
        {activeTab === 'servers' && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg"
          >
            <h2 className="text-xl font-semibold mb-4">Серверные ноды</h2>
            {loading ? (
              <p>Загрузка...</p>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {servers.map((server) => (
                  <div
                    key={server.id}
                    className="p-4 border border-gray-200 dark:border-gray-800 rounded-lg"
                  >
                    <div className="flex items-center justify-between mb-2">
                      <h3 className="font-semibold">{server.name}</h3>
                      {server.isHealthy ? (
                        <CheckCircle className="w-5 h-5 text-green-500" />
                      ) : (
                        <XCircle className="w-5 h-5 text-red-500" />
                      )}
                    </div>
                    <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">{server.ipAddress}</p>
                    <div className="space-y-1 text-sm">
                      <p>Контейнеры: {server.activeContainers}/{server.maxContainers}</p>
                      <p>CPU: {server.cpuUsage.toFixed(1)}%</p>
                      <p>RAM: {server.memoryUsage.toFixed(1)}%</p>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </motion.div>
        )}

        {/* Payments Tab */}
        {activeTab === 'payments' && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg"
          >
            <h2 className="text-xl font-semibold mb-4">Платежи</h2>
            <p className="text-gray-600 dark:text-gray-400">
              Здесь будет отображаться информация о платежах пользователей
            </p>
          </motion.div>
        )}
      </motion.div>
    </div>
  );
}