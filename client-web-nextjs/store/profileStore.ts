'use client';

import { create } from 'zustand';
import apiClient from '@/lib/axios';

export interface BrowserConfig {
  userAgent: string;
  screenResolution: string;
  timezone: string;
  language: string;
  webRTC: boolean;
  canvas: boolean;
  webGL: boolean;
}

export interface BrowserProfile {
  id: number;
  userId: number;
  name: string;
  containerId: string;
  serverNodeIp: string;
  port: number;
  config: BrowserConfig;
  status: 'Stopped' | 'Starting' | 'Running' | 'Stopping' | 'Error';
  createdAt: string;
  lastStartedAt?: string;
}

interface ProfileState {
  profiles: BrowserProfile[];
  loading: boolean;
  error: string | null;
  fetchProfiles: () => Promise<void>;
  createProfile: (name: string, config: BrowserConfig) => Promise<void>;
  startProfile: (id: number) => Promise<void>;
  stopProfile: (id: number) => Promise<void>;
  deleteProfile: (id: number) => Promise<void>;
}

export const useProfileStore = create<ProfileState>((set, get) => ({
  profiles: [],
  loading: false,
  error: null,

  fetchProfiles: async () => {
    set({ loading: true, error: null });
    try {
      console.log('📥 Загрузка профилей...');
      const response = await apiClient.get('/api/profile');
      console.log('✅ Профили загружены:', response.data);
      set({ profiles: response.data, loading: false });
    } catch (error: any) {
      console.error('❌ Ошибка загрузки:', error.response?.data || error.message);
      set({ error: error.message, loading: false });
    }
  },

  createProfile: async (name: string, config: BrowserConfig) => {
    set({ loading: true, error: null });
    try {
      console.log('➕ Создание профиля:', { name });
      const response = await apiClient.post('/api/profile', { name, config });
      console.log('✅ Профиль создан:', response.data);
      set((state) => ({
        profiles: [...state.profiles, response.data],
        loading: false,
      }));
    } catch (error: any) {
      console.error('❌ Ошибка создания:', error.response?.data || error.message);
      set({ error: error.message, loading: false });
      throw error;
    }
  },

  startProfile: async (id: number) => {
    set({ loading: true, error: null });
    try {
      console.log('▶️ Запуск профиля:', id);
      
      // Обновляем статус локально на "Starting" для мгновенной обратной связи
      set((state) => ({
        profiles: state.profiles.map((p) =>
          p.id === id ? { ...p, status: 'Starting' as const } : p
        ),
      }));
      
      const response = await apiClient.post(`/api/profile/${id}/start`);
      console.log('✅ Профиль запущен:', response.data);
      
      // Обновляем профиль из ответа API
      if (response.data?.profile) {
        const updatedProfile = response.data.profile;
        set((state) => ({
          profiles: state.profiles.map((p) =>
            p.id === id ? {
              ...p,
              ...updatedProfile,
              status: updatedProfile.status || 'Starting',
              containerId: updatedProfile.containerId || p.containerId,
              serverNodeIp: updatedProfile.serverNodeIp || p.serverNodeIp,
              port: updatedProfile.port || p.port,
            } : p
          ),
        }));
        
        // Если статус уже Running, не нужно продолжать проверку
        if (updatedProfile.status === 'Running') {
          set({ loading: false });
          return;
        }
      } else {
        // Если нет данных в ответе, обновляем список
        await get().fetchProfiles();
      }
      
      // Продолжаем обновлять статус каждые 2 секунды, пока профиль не запустится
      const checkStatus = async () => {
        try {
          await get().fetchProfiles();
          const profile = get().profiles.find((p) => p.id === id);
          if (profile) {
            if (profile.status === 'Starting') {
              // Продолжаем проверять, если статус все еще "Starting"
              setTimeout(checkStatus, 2000);
            } else if (profile.status === 'Running') {
              // Профиль запущен, останавливаем проверку
              set({ loading: false });
            } else {
              // Статус изменился на что-то другое (Error, Stopped), останавливаем проверку
              set({ loading: false });
            }
          } else {
            // Профиль не найден, останавливаем проверку
            set({ loading: false });
          }
        } catch (error) {
          console.error('Ошибка проверки статуса:', error);
          set({ loading: false });
        }
      };
      
      // Начинаем проверку через 2 секунды
      setTimeout(checkStatus, 2000);
    } catch (error: any) {
      console.error('❌ Ошибка запуска:', error.response?.data || error.message);
      
      // Сбрасываем статус на "Stopped" при ошибке
      set((state) => ({
        profiles: state.profiles.map((p) =>
          p.id === id ? { ...p, status: 'Stopped' as const } : p
        ),
        error: error.response?.data?.message || error.message,
        loading: false,
      }));
      throw error;
    }
  },

  stopProfile: async (id: number) => {
    set({ loading: true, error: null });
    try {
      console.log('⏸️ Остановка профиля:', id);
      
      // Обновляем статус локально на "Stopping" для мгновенной обратной связи
      set((state) => ({
        profiles: state.profiles.map((p) =>
          p.id === id ? { ...p, status: 'Stopping' as const } : p
        ),
      }));
      
      await apiClient.post(`/api/profile/${id}/stop`);
      console.log('✅ Профиль остановлен');
      
      // Обновляем список профилей
      await get().fetchProfiles();
      set({ loading: false });
    } catch (error: any) {
      console.error('❌ Ошибка остановки:', error.response?.data || error.message);
      
      // Сбрасываем статус обратно на "Running" при ошибке
      set((state) => ({
        profiles: state.profiles.map((p) =>
          p.id === id ? { ...p, status: 'Running' as const } : p
        ),
        error: error.response?.data?.message || error.message,
        loading: false,
      }));
      throw error;
    }
  },

  deleteProfile: async (id: number) => {
    set({ loading: true, error: null });
    try {
      console.log('🗑️ Удаление профиля:', id);
      await apiClient.delete(`/api/profile/${id}`);
      console.log('✅ Профиль удалён');
      set((state) => ({
        profiles: state.profiles.filter((p) => p.id !== id),
        loading: false,
      }));
    } catch (error: any) {
      console.error('❌ Ошибка удаления:', error.response?.data || error.message);
      set({ error: error.message, loading: false });
      throw error;
    }
  },
}));
