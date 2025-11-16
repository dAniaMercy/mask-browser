'use client';

import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import axios from 'axios';

interface User {
  id: number;
  username: string;
  email: string;
  isAdmin?: boolean;
  requires2FA?: boolean;
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
  hydrate: () => void;
}

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://109.172.101.73:5050';

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      token: null,
      isAuthenticated: false,
      isAdmin: false,

      // Восстановление состояния после перезагрузки
      hydrate: () => {
        const state = get();
        if (state.token) {
          axios.defaults.headers.common['Authorization'] = `Bearer ${state.token}`;
        }
      },

      login: async (email: string, password: string, twoFactorCode?: string) => {
        try {
          const response = await axios.post(
            `${API_URL}/api/auth/login`,
            { email, password, twoFactorCode },
            { withCredentials: true }
          );
          const { token, user } = response.data;
          
          // Устанавливаем токен в axios
          axios.defaults.headers.common['Authorization'] = `Bearer ${token}`;
          
          set({
            token,
            user,
            isAuthenticated: true,
            isAdmin: user.isAdmin || false,
          });
        } catch (error: any) {
          if (error.response?.status === 426) {
            throw error;
          }
          throw error;
        }
      },

      register: async (username: string, email: string, password: string) => {
        const response = await axios.post(
          `${API_URL}/api/auth/register`,
          { username, email, password },
          { withCredentials: true }
        );
        const { token, user } = response.data;
        
        // Устанавливаем токен в axios
        axios.defaults.headers.common['Authorization'] = `Bearer ${token}`;
        
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
        delete axios.defaults.headers.common['Authorization'];
      },
    }),
    {
      name: 'auth-storage',
      storage: createJSONStorage(() => localStorage),
      // Важно: восстанавливаем состояние после загрузки из localStorage
      onRehydrateStorage: () => (state) => {
        if (state?.token) {
          axios.defaults.headers.common['Authorization'] = `Bearer ${state.token}`;
        }
      },
    }
  )
);