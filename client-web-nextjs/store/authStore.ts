'use client';

import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import apiClient from '@/lib/axios';

interface User {
  id: number;
  username: string;
  email: string;
  isAdmin?: boolean;
  twoFactorEnabled?: boolean;
}

interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  login: (email: string, password: string, twoFactorCode?: string) => Promise<void>;
  register: (username: string, email: string, password: string) => Promise<void>;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      token: null,
      isAuthenticated: false,
      isAdmin: false,

      register: async (username: string, email: string, password: string) => {
        console.log('🚀 Регистрация:', { username, email });
        const response = await apiClient.post('/api/auth/register', {
          username,
          email,
          password,
        });
        console.log('✅ Регистрация успешна');
        const { token, user } = response.data;
        set({
          token,
          user,
          isAuthenticated: true,
          isAdmin: user.isAdmin || false,
        });
      },

      login: async (email: string, password: string, twoFactorCode?: string) => {
        console.log('🚀 Вход:', { email, has2FA: !!twoFactorCode });
        const response = await apiClient.post('/api/auth/login', {
          email,
          password,
          twoFactorCode,
        });
        console.log('✅ Вход успешен');
        const { token, user } = response.data;
        set({
          token,
          user,
          isAuthenticated: true,
          isAdmin: user.isAdmin || false,
        });
      },

      logout: () => {
        set({
          user: null,
          token: null,
          isAuthenticated: false,
          isAdmin: false,
        });
      },
    }),
    {
      name: 'auth-storage',
      storage: createJSONStorage(() => localStorage),
    }
  )
);
