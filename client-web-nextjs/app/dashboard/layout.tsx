'use client';

import Layout from '@/components/Layout';
import { ReactNode, useEffect } from 'react';
import { useAuthStore } from '@/store/authStore';

export default function DashboardLayout({
  children,
}: {
  children: ReactNode;
}) {
  useEffect(() => {
    // Восстанавливаем авторизацию из localStorage при загрузке
    const { hydrate } = useAuthStore.getState();
    hydrate();
  }, []);

  return <Layout>{children}</Layout>;
}

