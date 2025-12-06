'use client';

import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import axios from 'axios';

interface Subscription {
  tier: string;
  tierValue: number;
  maxProfiles: number;
  isActive: boolean;
  startDate: string;
  endDate: string | null;
}

interface User {
  id: number;
  username: string;
  email: string;
  isAdmin?: boolean;
  requires2FA?: boolean;
  twoFactorEnabled?: boolean;
  subscription?: Subscription | null;
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
  // Computed getter для isAuthenticated
  getIsAuthenticated: () => boolean;
}

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://109.172.101.73:5050';

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      token: null,
      isAuthenticated: false,
      isAdmin: false,

      // Computed getter для isAuthenticated
      getIsAuthenticated: () => {
        const state = get();
        return !!(state.token && state.user);
      },

      // Восстановление состояния после перезагрузки
      hydrate: () => {
        const state = get();
        if (state.token && state.user) {
          axios.defaults.headers.common['Authorization'] = `Bearer ${state.token}`;
          // Обновляем isAuthenticated на основе наличия токена и пользователя
          const isAuth = !!(state.token && state.user);
          set({
            isAuthenticated: isAuth,
            isAdmin: state.user?.isAdmin || false,
          });
        } else {
          // Если нет токена или пользователя, сбрасываем авторизацию
          set({
            isAuthenticated: false,
            isAdmin: false,
            user: null,
            token: null,
          });
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
        if (!state) return;
        
        if (state.token && state.user) {
          axios.defaults.headers.common['Authorization'] = `Bearer ${state.token}`;
          // Убеждаемся, что isAuthenticated установлен правильно
          state.isAuthenticated = !!(state.token && state.user);
          state.isAdmin = state.user.isAdmin || false;
        } else {
          // Если нет токена или пользователя, сбрасываем
          state.isAuthenticated = false;
          state.isAdmin = false;
          state.user = null;
          state.token = null;
        }
      },
    }
  )
);