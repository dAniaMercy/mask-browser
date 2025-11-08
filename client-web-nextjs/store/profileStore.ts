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
      await apiClient.post(`/api/profile/${id}/start`);
      console.log('✅ Профиль запущен');
      await get().fetchProfiles();
    } catch (error: any) {
      console.error('❌ Ошибка запуска:', error.response?.data || error.message);
      set({ error: error.message, loading: false });
      throw error;
    }
  },

  stopProfile: async (id: number) => {
    set({ loading: true, error: null });
    try {
      console.log('⏸️ Остановка профиля:', id);
      await apiClient.post(`/api/profile/${id}/stop`);
      console.log('✅ Профиль остановлен');
      await get().fetchProfiles();
    } catch (error: any) {
      console.error('❌ Ошибка остановки:', error.response?.data || error.message);
      set({ error: error.message, loading: false });
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
