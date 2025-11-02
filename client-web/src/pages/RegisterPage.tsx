import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { useTranslation } from 'react-i18next'

export default function RegisterPage() {
  const [username, setUsername] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const { register } = useAuthStore()
  const navigate = useNavigate()
  const { t } = useTranslation()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setLoading(true)

    try {
      await register(username, email, password)
      navigate('/dashboard')
    } catch (err: any) {
      setError(err.response?.data?.message || 'Ошибка регистрации')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-black text-white flex items-center justify-center">
      <div className="w-full max-w-md">
        <h1 className="text-3xl font-bold mb-8 text-center text-purple-400">
          {t('auth.registerTitle')}
        </h1>
        <form onSubmit={handleSubmit} className="bg-gray-900 p-8 rounded-lg border border-gray-800">
          {error && (
            <div className="mb-4 p-4 bg-red-900 text-red-200 rounded">{error}</div>
          )}
          <div className="mb-4">
            <label className="block mb-2">{t('auth.username')}</label>
            <input
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
              className="w-full px-4 py-2 bg-gray-800 border border-gray-700 rounded focus:outline-none focus:border-purple-500"
            />
          </div>
          <div className="mb-4">
            <label className="block mb-2">{t('auth.email')}</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="w-full px-4 py-2 bg-gray-800 border border-gray-700 rounded focus:outline-none focus:border-purple-500"
            />
          </div>
          <div className="mb-6">
            <label className="block mb-2">{t('auth.password')}</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              className="w-full px-4 py-2 bg-gray-800 border border-gray-700 rounded focus:outline-none focus:border-purple-500"
            />
          </div>
          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 bg-purple-600 hover:bg-purple-700 rounded font-semibold disabled:opacity-50"
          >
            {loading ? 'Загрузка...' : t('common.register')}
          </button>
          <p className="mt-4 text-center text-gray-400">
            Уже есть аккаунт?{' '}
            <Link to="/login" className="text-purple-400 hover:underline">
              {t('common.login')}
            </Link>
          </p>
        </form>
      </div>
    </div>
  )
}

