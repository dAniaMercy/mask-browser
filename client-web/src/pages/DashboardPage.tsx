import { useEffect, useState, useRef } from 'react'
import { useProfileStore, BrowserProfile, BrowserConfig } from '../store/profileStore'
import { useTranslation } from 'react-i18next'
import { Plus, Play, Square, Trash2, Edit } from 'lucide-react'

export default function DashboardPage() {
  const { profiles, loading, error, fetchProfiles, createProfile, startProfile, stopProfile, deleteProfile, updateProfile } =
    useProfileStore()
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)
  const [editingProfile, setEditingProfile] = useState<BrowserProfile | null>(null)
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

  // Validation / UI state
  const [formErrors, setFormErrors] = useState<{ name?: string; userAgent?: string }>({})
  const [saveError, setSaveError] = useState<string | null>(null)
  const [saveSuccess, setSaveSuccess] = useState(false)
  const [saving, setSaving] = useState(false)
  const inputRef = useRef<HTMLInputElement | null>(null)

  useEffect(() => {
    fetchProfiles()
  }, [fetchProfiles])

  useEffect(() => {
    if (showEditModal && inputRef.current) {
      // focus input after modal is shown
      setTimeout(() => inputRef.current?.focus(), 50)
    }
    if (showCreateModal && inputRef.current) {
      setTimeout(() => inputRef.current?.focus(), 50)
    }
  }, [showEditModal, showCreateModal])

  const validateForm = (name: string, ua: string) => {
    const errors: { name?: string; userAgent?: string } = {}
    if (!name || name.trim().length === 0) {
      errors.name = t('profile.validation.nameRequired') || 'Profile name is required'
    }
    if (!ua || ua.trim().length < 10) {
      errors.userAgent = t('profile.validation.userAgentTooShort') || 'User Agent must be at least 10 characters'
    }
    return errors
  }

  const handleCreate = async () => {
    setFormErrors({})
    setSaveError(null)
    setSaveSuccess(false)

    const errors = validateForm(profileName, config.userAgent)
    if (Object.keys(errors).length > 0) {
      setFormErrors(errors)
      return
    }

    setSaving(true)
    try {
      await createProfile(profileName, config)
      setSaveSuccess(true)
      // Auto-close after short delay
      setTimeout(() => {
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
        setSaveSuccess(false)
      }, 900)
    } catch (err: any) {
      setSaveError(err?.response?.data?.message || err?.message || 'Failed to create profile')
    } finally {
      setSaving(false)
    }
  }

  const handleEditOpen = (profile: BrowserProfile) => {
    setEditingProfile(profile)
    setProfileName(profile.name)
    setConfig(profile.config)
    setFormErrors({})
    setSaveError(null)
    setSaveSuccess(false)
    setShowEditModal(true)
  }

  const handleUpdate = async () => {
    if (!editingProfile) return
    setFormErrors({})
    setSaveError(null)
    setSaveSuccess(false)

    const errors = validateForm(profileName, config.userAgent)
    if (Object.keys(errors).length > 0) {
      setFormErrors(errors)
      return
    }

    setSaving(true)
    try {
      await updateProfile(editingProfile.id, profileName, config)
      setSaveSuccess(true)
      // close modal shortly after success
      setTimeout(() => {
        setShowEditModal(false)
        setEditingProfile(null)
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
        setSaveSuccess(false)
      }, 900)
    } catch (err: any) {
      setSaveError(err?.response?.data?.message || err?.message || 'Failed to save profile')
    } finally {
      setSaving(false)
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
          onClick={() => { setShowCreateModal(true); setFormErrors({}); setSaveError(null); setSaveSuccess(false) }}
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
                  onClick={() => handleEditOpen(profile)}
                  className="px-3 py-2 bg-blue-600 hover:bg-blue-700 rounded flex items-center space-x-2"
                >
                  <Edit className="w-4 h-4" />
                </button>
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

            {/* success / error */}
            {saveSuccess && (
              <div className="mb-4 p-3 bg-green-700 text-green-100 rounded">{t('profile.savedSuccess') || 'Profile saved'}</div>
            )}
            {saveError && (
              <div className="mb-4 p-3 bg-red-700 text-red-100 rounded">{saveError}</div>
            )}

            <div className="space-y-4">
              <div>
                <label className="block mb-2">{t('profile.name')}</label>
                <input
                  ref={inputRef}
                  type="text"
                  value={profileName}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => setProfileName(e.target.value)}
                  className={`w-full px-4 py-2 bg-gray-800 border rounded focus:outline-none focus:border-purple-500 ${formErrors.name ? 'border-red-500' : 'border-gray-700'}`}
                />
                {formErrors.name && <div className="mt-1 text-sm text-red-400">{formErrors.name}</div>}
              </div>
              <div>
                <label className="block mb-2">User Agent</label>
                <input
                  type="text"
                  value={config.userAgent}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => setConfig({ ...config, userAgent: e.target.value })}
                  className={`w-full px-4 py-2 bg-gray-800 border rounded focus:outline-none focus:border-purple-500 ${formErrors.userAgent ? 'border-red-500' : 'border-gray-700'}`}
                  placeholder="Mozilla/5.0..."
                />
                {formErrors.userAgent && <div className="mt-1 text-sm text-red-400">{formErrors.userAgent}</div>}
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
                  disabled={saving}
                  className={`flex-1 px-4 py-2 ${saving ? 'bg-gray-500' : 'bg-purple-600 hover:bg-purple-700'} rounded text-white`}
                >
                  {saving ? (t('common.saving') || 'Saving...') : (t('common.create') || 'Create')}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {showEditModal && editingProfile && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-gray-900 p-8 rounded-lg border border-gray-800 w-full max-w-md">
            <h2 className="text-2xl font-bold mb-4">{t('profile.edit') || 'Edit profile'}</h2>

            {/* success / error */}
            {saveSuccess && (
              <div className="mb-4 p-3 bg-green-700 text-green-100 rounded">{t('profile.savedSuccess') || 'Profile saved'}</div>
            )}
            {saveError && (
              <div className="mb-4 p-3 bg-red-700 text-red-100 rounded">{saveError}</div>
            )}

            <div className="space-y-4">
              <div>
                <label className="block mb-2">{t('profile.name')}</label>
                <input
                  ref={inputRef}
                  type="text"
                  value={profileName}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => setProfileName(e.target.value)}
                  className={`w-full px-4 py-2 bg-gray-800 border rounded focus:outline-none focus:border-purple-500 ${formErrors.name ? 'border-red-500' : 'border-gray-700'}`}
                />
                {formErrors.name && <div className="mt-1 text-sm text-red-400">{formErrors.name}</div>}
              </div>
              <div>
                <label className="block mb-2">User Agent</label>
                <input
                  type="text"
                  value={config.userAgent}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => setConfig({ ...config, userAgent: e.target.value })}
                  className={`w-full px-4 py-2 bg-gray-800 border rounded focus:outline-none focus:border-purple-500 ${formErrors.userAgent ? 'border-red-500' : 'border-gray-700'}`}
                  placeholder="Mozilla/5.0..."
                />
                {formErrors.userAgent && <div className="mt-1 text-sm text-red-400">{formErrors.userAgent}</div>}
              </div>
              <div className="flex space-x-4">
                <button
                  onClick={() => { setShowEditModal(false); setEditingProfile(null) }}
                  className="flex-1 px-4 py-2 bg-gray-700 hover:bg-gray-600 rounded"
                >
                  {t('common.cancel')}
                </button>
                <button
                  onClick={handleUpdate}
                  disabled={saving}
                  className={`flex-1 px-4 py-2 ${saving ? 'bg-gray-500' : 'bg-blue-600 hover:bg-blue-700'} rounded text-white`}
                >
                  {saving ? (t('common.saving') || 'Saving...') : (t('common.save') || 'Save')}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

