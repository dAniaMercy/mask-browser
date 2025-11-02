import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Shield, Zap, Server, BarChart3 } from 'lucide-react'

export default function LandingPage() {
  const { t } = useTranslation()

  return (
    <div className="min-h-screen bg-black text-white">
      <div className="container mx-auto px-4 py-16">
        <div className="text-center mb-16">
          <h1 className="text-6xl font-bold mb-4 text-purple-400">{t('landing.title')}</h1>
          <p className="text-2xl text-gray-400">{t('landing.subtitle')}</p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8 mb-16">
          <div className="bg-gray-900 p-6 rounded-lg border border-gray-800">
            <Shield className="w-12 h-12 text-purple-400 mb-4" />
            <h3 className="text-xl font-semibold mb-2">{t('landing.features.feature1')}</h3>
          </div>
          <div className="bg-gray-900 p-6 rounded-lg border border-gray-800">
            <Zap className="w-12 h-12 text-purple-400 mb-4" />
            <h3 className="text-xl font-semibold mb-2">{t('landing.features.feature2')}</h3>
          </div>
          <div className="bg-gray-900 p-6 rounded-lg border border-gray-800">
            <Server className="w-12 h-12 text-purple-400 mb-4" />
            <h3 className="text-xl font-semibold mb-2">{t('landing.features.feature3')}</h3>
          </div>
          <div className="bg-gray-900 p-6 rounded-lg border border-gray-800">
            <BarChart3 className="w-12 h-12 text-purple-400 mb-4" />
            <h3 className="text-xl font-semibold mb-2">{t('landing.features.feature4')}</h3>
          </div>
        </div>

        <div className="text-center">
          <Link
            to="/register"
            className="inline-block px-8 py-4 bg-purple-600 hover:bg-purple-700 rounded-lg text-lg font-semibold mr-4"
          >
            {t('common.register')}
          </Link>
          <Link
            to="/login"
            className="inline-block px-8 py-4 bg-gray-800 hover:bg-gray-700 rounded-lg text-lg font-semibold"
          >
            {t('common.login')}
          </Link>
        </div>
      </div>
    </div>
  )
}

