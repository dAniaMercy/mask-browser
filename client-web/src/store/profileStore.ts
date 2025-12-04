import { create } from 'zustand'
import { apiClient } from '../services/api'  // ← ИЗМЕНЕНИЕ: используем apiClient вместо axios

export interface BrowserConfig {
  userAgent: string
  screenResolution: string
  timezone: string
  language: string
  webRTC: boolean
  canvas: boolean
  webGL: boolean
}

export interface BrowserProfile {
  id: number
  userId: number
  name: string
  containerId: string
  serverNodeIp: string
  port: number
  config: BrowserConfig
  status: 'Stopped' | 'Starting' | 'Running' | 'Stopping' | 'Error'
  createdAt: string
  lastStartedAt?: string
}

interface ProfileState {
  profiles: BrowserProfile[]
  loading: boolean
  error: string | null
  fetchProfiles: () => Promise<void>
  createProfile: (name: string, config: BrowserConfig) => Promise<void>
  startProfile: (id: number) => Promise<void>
  stopProfile: (id: number) => Promise<void>
  deleteProfile: (id: number) => Promise<void>
  updateProfile: (id: number, name: string, config: BrowserConfig) => Promise<void>
}

export const useProfileStore = create<ProfileState>((set, get) => ({
  profiles: [],
  loading: false,
  error: null,

  fetchProfiles: async () => {
    set({ loading: true, error: null })
    try {
      const response = await apiClient.get('/profile')  // apiClient уже имеет baseURL с /api
      set({ profiles: response.data, loading: false })
    } catch (error: any) {
      set({ error: error.message || 'Failed to fetch profiles', loading: false })
    }
  },

  createProfile: async (name: string, config: BrowserConfig) => {
    set({ loading: true, error: null })
    try {
      const response = await apiClient.post('/profile', { name, config })
      set((state) => ({
        profiles: [...state.profiles, response.data],
        loading: false,
      }))
    } catch (error: any) {
      set({ error: error?.response?.data?.message || error.message || 'Failed to create profile', loading: false })
      throw error
    }
  },

  startProfile: async (id: number) => {
    set({ loading: true, error: null })
    try {
      await apiClient.post(`/profile/${id}/start`)
      await get().fetchProfiles()
    } catch (error: any) {
      set({ error: error?.response?.data?.message || error.message || 'Failed to start profile', loading: false })
      throw error
    }
  },

  stopProfile: async (id: number) => {
    set({ loading: true, error: null })
    try {
      await apiClient.post(`/profile/${id}/stop`)
      await get().fetchProfiles()
    } catch (error: any) {
      set({ error: error?.response?.data?.message || error.message || 'Failed to stop profile', loading: false })
      throw error
    }
  },

  deleteProfile: async (id: number) => {
    set({ loading: true, error: null })
    try {
      await apiClient.delete(`/profile/${id}`)
      set((state) => ({
        profiles: state.profiles.filter((p) => p.id !== id),
        loading: false,
      }))
    } catch (error: any) {
      set({ error: error?.response?.data?.message || error.message || 'Failed to delete profile', loading: false })
      throw error
    }
  },

  updateProfile: async (id: number, name: string, config: BrowserConfig) => {
    set({ loading: true, error: null })
    try {
      const response = await apiClient.put(`/profile/${id}`, { name, config })
      const updated = response.data
      set((state) => ({
        profiles: state.profiles.map((p) => (p.id === id ? updated : p)),
        loading: false,
      }))
    } catch (error: any) {
      set({ error: error?.response?.data?.message || error.message || 'Failed to update profile', loading: false })
      throw error
    }
  },
}))