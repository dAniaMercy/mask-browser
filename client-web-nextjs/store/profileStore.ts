'use client';

import { create } from 'zustand';
import axios from 'axios';

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

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://109.172.101.73:5050';

export const useProfileStore = create<ProfileState>((set, get) => ({
  profiles: [],
  loading: false,
  error: null,

  fetchProfiles: async () => {
    set({ loading: true, error: null });
    try {
      const response = await axios.get(`${API_URL}/api/profile`, {
        headers: {
          Authorization: `Bearer ${localStorage.getItem('auth-storage') ? 
            JSON.parse(localStorage.getItem('auth-storage') || '{}').state?.token : ''}`
        }
      });
      set({ profiles: response.data, loading: false });
    } catch (error: any) {
      set({ error: error.message, loading: false });
    }
  },

  createProfile: async (name: string, config: BrowserConfig) => {
    set({ loading: true, error: null });
    try {
      const token = JSON.parse(localStorage.getItem('auth-storage') || '{}').state?.token;
      const response = await axios.post(
        `${API_URL}/api/profile`,
        { name, config },
        {
          headers: { Authorization: `Bearer ${token}` }
        }
      );
      set((state) => ({
        profiles: [...state.profiles, response.data],
        loading: false,
      }));
    } catch (error: any) {
      set({ error: error.message, loading: false });
      throw error;
    }
  },

  startProfile: async (id: number) => {
    set({ loading: true, error: null });
    try {
      const token = JSON.parse(localStorage.getItem('auth-storage') || '{}').state?.token;
      await axios.post(
        `${API_URL}/api/profile/${id}/start`,
        {},
        {
          headers: { Authorization: `Bearer ${token}` }
        }
      );
      await get().fetchProfiles();
    } catch (error: any) {
      set({ error: error.message, loading: false });
      throw error;
    }
  },

  stopProfile: async (id: number) => {
    set({ loading: true, error: null });
    try {
      const token = JSON.parse(localStorage.getItem('auth-storage') || '{}').state?.token;
      await axios.post(
        `${API_URL}/api/profile/${id}/stop`,
        {},
        {
          headers: { Authorization: `Bearer ${token}` }
        }
      );
      await get().fetchProfiles();
    } catch (error: any) {
      set({ error: error.message, loading: false });
      throw error;
    }
  },

  deleteProfile: async (id: number) => {
    set({ loading: true, error: null });
    try {
      const token = JSON.parse(localStorage.getItem('auth-storage') || '{}').state?.token;
      await axios.delete(`${API_URL}/api/profile/${id}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      set((state) => ({
        profiles: state.profiles.filter((p) => p.id !== id),
        loading: false,
      }));
    } catch (error: any) {
      set({ error: error.message, loading: false });
      throw error;
    }
  },
}));

