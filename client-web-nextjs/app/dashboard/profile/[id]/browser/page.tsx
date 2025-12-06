'use client';

import { useEffect, useRef, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { useAuthStore } from '@/store/authStore';
import { useProfileStore } from '@/store/profileStore';
import Layout from '@/components/Layout';
import { ArrowLeft, RefreshCw } from 'lucide-react';

export default function BrowserPage() {
  const router = useRouter();
  const params = useParams();
  const profileId = parseInt(params.id as string);
  const { isAuthenticated } = useAuthStore();
  const { profiles, fetchProfiles } = useProfileStore();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [vncUrl, setVncUrl] = useState<string | null>(null);
  const vncContainerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/login');
      return;
    }
  }, [isAuthenticated, router]);

  useEffect(() => {
    if (!profileId || !isAuthenticated) return;

    const loadProfile = async () => {
      try {
        setLoading(true);
        await fetchProfiles();
        
        const profile = profiles.find(p => p.id === profileId);
        
        if (!profile) {
          setError('Профиль не найден');
          setLoading(false);
          return;
        }

        if (profile.status !== 'Running') {
          setError('Профиль не запущен. Запустите профиль перед просмотром браузера.');
          setLoading(false);
          return;
        }

        if (!profile.port || !profile.serverNodeIp) {
          setError('Порт или IP сервера не указаны');
          setLoading(false);
          return;
        }

        // Формируем URL для noVNC
        // websockify работает на порту 6080 и предоставляет WebSocket endpoint
        // noVNC клиент должен подключаться через WebSocket
        const vncUrl = `http://${profile.serverNodeIp}:${profile.port}/vnc.html?autoconnect=true&resize=scale&password=`;
        
        setVncUrl(vncUrl);
        setLoading(false);
      } catch (err) {
        console.error('Ошибка загрузки профиля:', err);
        setError('Не удалось загрузить профиль');
        setLoading(false);
      }
    };

    loadProfile();
  }, [profileId, isAuthenticated, fetchProfiles, profiles]);

  useEffect(() => {
    if (!vncUrl || !vncContainerRef.current) return;

    // Создаем iframe для noVNC
    const iframe = document.createElement('iframe');
    iframe.src = vncUrl;
    iframe.style.width = '100%';
    iframe.style.height = '100%';
    iframe.style.border = 'none';
    iframe.setAttribute('allow', 'fullscreen');
    
    vncContainerRef.current.innerHTML = '';
    vncContainerRef.current.appendChild(iframe);

    return () => {
      if (vncContainerRef.current) {
        vncContainerRef.current.innerHTML = '';
      }
    };
  }, [vncUrl]);

  const profile = profiles.find(p => p.id === profileId);

  if (loading) {
    return (
      <Layout>
        <div className="flex items-center justify-center min-h-screen">
          <div className="text-center">
            <RefreshCw className="w-8 h-8 animate-spin mx-auto mb-4 text-blue-500" />
            <p className="text-gray-400">Загрузка браузера...</p>
          </div>
        </div>
      </Layout>
    );
  }

  if (error) {
    return (
      <Layout>
        <div className="container mx-auto px-4 py-8">
          <button
            onClick={() => router.push('/dashboard')}
            className="mb-4 flex items-center space-x-2 text-gray-400 hover:text-white transition-colors"
          >
            <ArrowLeft className="w-4 h-4" />
            <span>Назад к профилям</span>
          </button>
          <div className="bg-red-500/10 border border-red-500/50 rounded-lg p-6 text-center">
            <p className="text-red-400 mb-4">{error}</p>
            <button
              onClick={() => router.push('/dashboard')}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 rounded-lg text-white"
            >
              Вернуться к профилям
            </button>
          </div>
        </div>
      </Layout>
    );
  }

  return (
    <Layout>
      <div className="flex flex-col h-screen">
        <div className="bg-gray-900 border-b border-gray-800 px-4 py-3 flex items-center justify-between">
          <div className="flex items-center space-x-4">
            <button
              onClick={() => router.push('/dashboard')}
              className="flex items-center space-x-2 text-gray-400 hover:text-white transition-colors"
            >
              <ArrowLeft className="w-4 h-4" />
              <span>Назад</span>
            </button>
            <div className="h-6 w-px bg-gray-700" />
            <h1 className="text-lg font-semibold text-white">
              Браузер: {profile?.name || 'Профиль'}
            </h1>
          </div>
          <div className="flex items-center space-x-2">
            <span className="text-sm text-gray-400">
              {profile?.serverNodeIp}:{profile?.port}
            </span>
            <button
              onClick={() => window.location.reload()}
              className="p-2 text-gray-400 hover:text-white transition-colors"
              title="Обновить"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
          </div>
        </div>
        <div ref={vncContainerRef} className="flex-1 bg-black overflow-hidden" />
      </div>
    </Layout>
  );
}

