'use client';

import { ReactNode } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useAuthStore } from '@/store/authStore';
import { useTranslation } from '@/hooks/useTranslation';
import { ThemeToggle } from './ThemeToggle';
import { LogOut, User, Settings, Shield } from 'lucide-react';

interface LayoutProps {
  children: ReactNode;
}

export default function Layout({ children }: LayoutProps) {
  const { user, logout, isAuthenticated, isAdmin } = useAuthStore();
  const router = useRouter();
  const { t } = useTranslation();

  const handleLogout = () => {
    logout();
    router.push('/');
  };

  if (!isAuthenticated) {
    return <>{children}</>;
  }

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-900 dark:to-black">
      <nav className="bg-white dark:bg-gray-900 border-b border-gray-200 dark:border-gray-800">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-8">
              <Link href="/" className="text-2xl font-bold text-purple-600 dark:text-purple-400">
                MASK BROWSER
              </Link>
              <Link
                href="/dashboard"
                className="text-gray-700 dark:text-gray-300 hover:text-purple-600 dark:hover:text-purple-400"
              >
                {t('common.dashboard')}
              </Link>
              {isAdmin && (
                <Link
                  href="/admin"
                  className="text-gray-700 dark:text-gray-300 hover:text-purple-600 dark:hover:text-purple-400"
                >
                  {t('common.admin')}
                </Link>
              )}
            </div>
            <div className="flex items-center space-x-4">
              <Link
                href="/dashboard/settings"
                className="p-2 text-gray-700 dark:text-gray-300 hover:text-purple-600 dark:hover:text-purple-400"
                title={t('common.settings')}
              >
                <Settings className="w-5 h-5" />
              </Link>
              <ThemeToggle />
              <span className="flex items-center space-x-2 text-gray-700 dark:text-gray-300">
                <User className="w-4 h-4" />
                <span>{user?.username}</span>
                <Shield className="w-4 h-4 text-green-500" {...({ title: '2FA включен' } as any)} />
              </span>
              <button
                onClick={handleLogout}
                className="flex items-center space-x-2 px-4 py-2 bg-red-600 hover:bg-red-700 rounded-lg text-white"
              >
                <LogOut className="w-4 h-4" />
                <span>{t('common.logout')}</span>
              </button>
            </div>
          </div>
        </div>
      </nav>
      <main className="container mx-auto px-4 py-8">{children}</main>
    </div>
  );
}

