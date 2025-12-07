'use client';

import { useEffect, useRef, useState, useMemo, useCallback } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { useAuthStore } from '@/store/authStore';
import { useProfileStore } from '@/store/profileStore';
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
  // Используем ref для отслеживания монтирования компонента
  const isMountedRef = useRef(true);

  // Безопасное обновление состояния только если компонент смонтирован
  const safeSetState = useCallback(<T,>(setter: (value: T) => void, value: T) => {
    if (isMountedRef.current) {
      setter(value);
    }
  }, []);

  useEffect(() => {
    // Восстанавливаем авторизацию из localStorage
    const { hydrate } = useAuthStore.getState();
    hydrate();
    
    return () => {
      isMountedRef.current = false;
    };
  }, []);

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
        safeSetState(setLoading, true);
        await fetchProfiles();
        
        // Небольшая задержка для обновления store
        await new Promise(resolve => setTimeout(resolve, 100));
        
        if (!isMountedRef.current) return;
        
        const currentProfiles = useProfileStore.getState().profiles;
        const profile = currentProfiles.find(p => p.id === profileId);
        
        if (!profile) {
          safeSetState(setError, 'Профиль не найден');
          safeSetState(setLoading, false);
          return;
        }

        if (profile.status !== 'Running') {
          safeSetState(setError, 'Профиль не запущен. Запустите профиль перед просмотром браузера.');
          safeSetState(setLoading, false);
          return;
        }

        if (!profile.port || !profile.serverNodeIp) {
          safeSetState(setError, 'Порт или IP сервера не указаны');
          safeSetState(setLoading, false);
          return;
        }

        // Используем прокси через API для безопасности
        // Вместо прямого подключения к порту контейнера, используем API endpoint
        const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://109.172.101.73:5050';
        const vncUrl = `${apiUrl}/api/profile/${profile.id}/browser/proxy?path=vnc.html&autoconnect=true&resize=scale`;
        
        console.log('🌐 VNC URL:', vncUrl);
        console.log('📊 Profile data:', { 
          id: profile.id, 
          status: profile.status, 
          port: profile.port, 
          serverNodeIp: profile.serverNodeIp 
        });
        
        safeSetState(setVncUrl, vncUrl);
        safeSetState(setLoading, false);
      } catch (err) {
        console.error('Ошибка загрузки профиля:', err);
        if (isMountedRef.current) {
          safeSetState(setError, 'Не удалось загрузить профиль');
          safeSetState(setLoading, false);
        }
      }
    };

    loadProfile();
  }, [profileId, isAuthenticated, safeSetState]); // Добавили safeSetState в зависимости

  useEffect(() => {
    if (!vncUrl || !vncContainerRef.current) return;
    
    // Проверяем, не создан ли уже iframe
    if (vncContainerRef.current.children.length > 0) {
      console.log('🖼️ iframe already exists, skipping creation');
      return;
    }

    console.log('🖼️ Creating iframe with URL:', vncUrl);

    // Создаем iframe для noVNC
    const iframe = document.createElement('iframe');
    iframe.src = vncUrl;
    iframe.style.width = '100%';
    iframe.style.height = '100%';
    iframe.style.border = 'none';
    iframe.setAttribute('allow', 'fullscreen');
    // Добавляем sandbox с минимальными разрешениями для безопасности, но разрешаем WebSocket
    // allow-same-origin нужен для работы WebSocket в некоторых браузерах
    iframe.setAttribute('sandbox', 'allow-same-origin allow-scripts allow-forms allow-popups allow-popups-to-escape-sandbox');
    // Добавляем атрибут для работы с куками (но это не поможет для cross-origin iframe)
    iframe.setAttribute('credentialless', 'false');
    
    let loadTimeout: NodeJS.Timeout;
    let errorTimeout: NodeJS.Timeout;
    let checkTimeout: NodeJS.Timeout;
    
    iframe.onload = () => {
      console.log('✅ iframe loaded successfully');
      if (isMountedRef.current) {
        clearTimeout(loadTimeout);
        clearTimeout(errorTimeout);
        clearTimeout(checkTimeout);
      }
    };
    
    iframe.onerror = (error) => {
      console.error('❌ iframe error:', error);
      if (isMountedRef.current) {
        clearTimeout(loadTimeout);
        clearTimeout(errorTimeout);
        clearTimeout(checkTimeout);
        // Не устанавливаем ошибку сразу, даем время на загрузку WebSocket
      }
    };
    
    // Проверяем доступность прокси endpoint перед созданием iframe
    const checkProxy = async () => {
      if (!isMountedRef.current) return;
      try {
        // Используем GET вместо HEAD для лучшей совместимости
        const response = await fetch(vncUrl, { 
          method: 'GET',
          credentials: 'include',
          cache: 'no-cache'
        });
        if (response.ok) {
          console.log('✅ Proxy endpoint is accessible');
        } else {
          console.warn('⚠️ Proxy endpoint returned:', response.status);
        }
      } catch (err) {
        console.warn('⚠️ Proxy check failed:', err);
        // Это может быть нормально, продолжаем
      }
    };
    
    // Проверяем прокси через небольшую задержку
    checkTimeout = setTimeout(checkProxy, 1000);
    
    // Таймаут для проверки загрузки (увеличиваем до 30 секунд для WebSocket)
    loadTimeout = setTimeout(() => {
      if (isMountedRef.current) {
        console.warn('⚠️ iframe loading timeout - проверяем доступность прокси');
        // Проверяем доступность прокси
        fetch(vncUrl, { method: 'GET', credentials: 'include' })
          .catch(() => {
            if (isMountedRef.current) {
              console.error('❌ Прокси недоступен');
              safeSetState(setError, 'Не удалось подключиться к браузеру через прокси. Проверьте, что профиль запущен.');
            }
          });
      }
    }, 30000);
    
    // Дополнительный таймаут для предупреждения
    errorTimeout = setTimeout(() => {
      if (isMountedRef.current) {
        console.warn('⚠️ iframe still loading after 15 seconds - это нормально для WebSocket соединений');
      }
    }, 15000);
    
    if (vncContainerRef.current && isMountedRef.current) {
      vncContainerRef.current.innerHTML = '';
      vncContainerRef.current.appendChild(iframe);
    }

    return () => {
      clearTimeout(loadTimeout);
      clearTimeout(errorTimeout);
      clearTimeout(checkTimeout);
      // Не очищаем iframe при размонтировании, чтобы не прерывать соединение
    };
  }, [vncUrl, safeSetState]);

  // Используем useMemo для предотвращения лишних пересчетов
  const profile = useMemo(() => {
    return profiles.find(p => p.id === profileId);
  }, [profiles, profileId]);

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-gray-900">
        <div className="text-center">
          <RefreshCw className="w-8 h-8 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-gray-400">Загрузка браузера...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-gray-900 p-8">
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
    );
  }

  return (
    <div className="flex flex-col h-screen bg-gray-900">
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
  );
}

