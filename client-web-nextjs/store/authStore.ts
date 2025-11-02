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

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5050';

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      token: null,
      isAuthenticated: false,
      isAdmin: false,

      login: async (email: string, password: string, twoFactorCode?: string) => {
        try {
          const response = await axios.post(
            `${API_URL}/api/auth/login`,
            { email, password, twoFactorCode },
            { withCredentials: true }
          );
          const { token, user } = response.data;
          set({
            token,
            user,
            isAuthenticated: true,
            isAdmin: user.isAdmin || false,
          });
          axios.defaults.headers.common['Authorization'] = `Bearer ${token}`;
        } catch (error: any) {
          // Если требуется 2FA, пробрасываем ошибку дальше
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
        set({
          token,
          user,
          isAuthenticated: true,
          isAdmin: user.isAdmin || false,
        });
        axios.defaults.headers.common['Authorization'] = `Bearer ${token}`;
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
    }
  )
);

