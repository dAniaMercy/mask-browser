'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';

export default function AccountPage() {
  const router = useRouter();

  useEffect(() => {
    // Редирект на новую страницу профиля
    router.replace('/dashboard/profile');
  }, [router]);

  return null;
}
