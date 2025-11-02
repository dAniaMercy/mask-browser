import { ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { useTranslation } from 'react-i18next'
import { LogOut, User, Settings } from 'lucide-react'

interface LayoutProps {
  children: ReactNode
}

export default function Layout({ children }: LayoutProps) {
  const { user, logout, isAdmin } = useAuthStore()
  const navigate = useNavigate()
  const { t } = useTranslation()

  const handleLogout = () => {
    logout()
    navigate('/')
  }

  return (
    <div className="min-h-screen bg-black text-white">
      <nav className="border-b border-gray-800">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-8">
              <Link to="/" className="text-2xl font-bold text-purple-400">
                MASK BROWSER
              </Link>
              <Link to="/dashboard" className="hover:text-purple-400">
                {t('common.dashboard')}
              </Link>
              {isAdmin && (
                <Link to="/admin" className="hover:text-purple-400">
                  {t('common.admin')}
                </Link>
              )}
            </div>
            <div className="flex items-center space-x-4">
              <span className="flex items-center space-x-2">
                <User className="w-4 h-4" />
                <span>{user?.username}</span>
              </span>
              <button
                onClick={handleLogout}
                className="flex items-center space-x-2 px-4 py-2 bg-red-600 hover:bg-red-700 rounded"
              >
                <LogOut className="w-4 h-4" />
                <span>{t('common.logout')}</span>
              </button>
            </div>
          </div>
        </div>
      </nav>
      <main className="container mx-auto px-4 py-8">{children}</main>
    </div>
  )
}

