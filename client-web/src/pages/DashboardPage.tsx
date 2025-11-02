import { useEffect, useState } from 'react'
import { useProfileStore, BrowserProfile, BrowserConfig } from '../store/profileStore'
import { useTranslation } from 'react-i18next'
import { Plus, Play, Square, Trash2 } from 'lucide-react'

export default function DashboardPage() {
  const { profiles, loading, error, fetchProfiles, createProfile, startProfile, stopProfile, deleteProfile } =
    useProfileStore()
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [profileName, setProfileName] = useState('')
  const [config, setConfig] = useState<BrowserConfig>({
    userAgent: '',
    screenResolution: '1920x1080',
    timezone: 'UTC',
    language: 'en-US',
    webRTC: false,
    canvas: false,
    webGL: false,
  })
  const { t } = useTranslation()

  useEffect(() => {
    fetchProfiles()
  }, [fetchProfiles])

  const handleCreate = async () => {
    try {
      await createProfile(profileName, config)
      setShowCreateModal(false)
      setProfileName('')
      setConfig({
        userAgent: '',
        screenResolution: '1920x1080',
        timezone: 'UTC',
        language: 'en-US',
        webRTC: false,
        canvas: false,
        webGL: false,
      })
    } catch (err) {
      console.error(err)
    }
  }

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Running':
        return 'text-green-400'
      case 'Starting':
        return 'text-yellow-400'
      case 'Stopping':
        return 'text-yellow-400'
      case 'Error':
        return 'text-red-400'
      default:
        return 'text-gray-400'
    }
  }

  return (
    <div>
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold">{t('profile.title')}</h1>
        <button
          onClick={() => setShowCreateModal(true)}
          className="flex items-center space-x-2 px-4 py-2 bg-purple-600 hover:bg-purple-700 rounded"
        >
          <Plus className="w-5 h-5" />
          <span>{t('profile.create')}</span>
        </button>
      </div>

      {error && (
        <div className="mb-4 p-4 bg-red-900 text-red-200 rounded">{error}</div>
      )}

      {loading ? (
        <div className="text-center py-8">Загрузка...</div>
      ) : profiles.length === 0 ? (
        <div className="text-center py-8 text-gray-400">
          Нет профилей. Создайте первый профиль.
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {profiles.map((profile) => (
            <div
              key={profile.id}
              className="bg-gray-900 p-6 rounded-lg border border-gray-800"
            >
              <h3 className="text-xl font-semibold mb-2">{profile.name}</h3>
              <div className="space-y-2 mb-4">
                <div className="flex justify-between">
                  <span className="text-gray-400">{t('profile.status')}:</span>
                  <span className={getStatusColor(profile.status)}>{profile.status}</span>
                </div>
                {profile.serverNodeIp && (
                  <div className="flex justify-between">
                    <span className="text-gray-400">{t('profile.node')}:</span>
                    <span>{profile.serverNodeIp}</span>
                  </div>
                )}
                {profile.port > 0 && (
                  <div className="flex justify-between">
                    <span className="text-gray-400">{t('profile.port')}:</span>
                    <span>{profile.port}</span>
                  </div>
                )}
              </div>
              <div className="flex space-x-2">
                {profile.status === 'Stopped' && (
                  <button
                    onClick={() => startProfile(profile.id)}
                    className="flex-1 flex items-center justify-center space-x-2 px-4 py-2 bg-green-600 hover:bg-green-700 rounded"
                  >
                    <Play className="w-4 h-4" />
                    <span>{t('common.start')}</span>
                  </button>
                )}
                {profile.status === 'Running' && (
                  <button
                    onClick={() => stopProfile(profile.id)}
                    className="flex-1 flex items-center justify-center space-x-2 px-4 py-2 bg-yellow-600 hover:bg-yellow-700 rounded"
                  >
                    <Square className="w-4 h-4" />
                    <span>{t('common.stop')}</span>
                  </button>
                )}
                <button
                  onClick={() => deleteProfile(profile.id)}
                  className="px-4 py-2 bg-red-600 hover:bg-red-700 rounded"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {showCreateModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-gray-900 p-8 rounded-lg border border-gray-800 w-full max-w-md">
            <h2 className="text-2xl font-bold mb-4">{t('profile.create')}</h2>
            <div className="space-y-4">
              <div>
                <label className="block mb-2">{t('profile.name')}</label>
                <input
                  type="text"
                  value={profileName}
                  onChange={(e) => setProfileName(e.target.value)}
                  className="w-full px-4 py-2 bg-gray-800 border border-gray-700 rounded"
                />
              </div>
              <div>
                <label className="block mb-2">User Agent</label>
                <input
                  type="text"
                  value={config.userAgent}
                  onChange={(e) => setConfig({ ...config, userAgent: e.target.value })}
                  className="w-full px-4 py-2 bg-gray-800 border border-gray-700 rounded"
                  placeholder="Mozilla/5.0..."
                />
              </div>
              <div className="flex space-x-4">
                <button
                  onClick={() => setShowCreateModal(false)}
                  className="flex-1 px-4 py-2 bg-gray-700 hover:bg-gray-600 rounded"
                >
                  {t('common.cancel')}
                </button>
                <button
                  onClick={handleCreate}
                  className="flex-1 px-4 py-2 bg-purple-600 hover:bg-purple-700 rounded"
                >
                  {t('common.create')}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

