import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Server, Users, DollarSign } from 'lucide-react'

export default function AdminPanel() {
  const { t } = useTranslation()

  return (
    <div>
      <h1 className="text-3xl font-bold mb-8">{t('common.admin')}</h1>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
        <div className="bg-gray-900 p-6 rounded-lg border border-gray-800">
          <div className="flex items-center space-x-4">
            <Server className="w-12 h-12 text-purple-400" />
            <div>
              <h3 className="text-lg font-semibold">Серверы</h3>
              <p className="text-2xl font-bold">0</p>
            </div>
          </div>
        </div>
        <div className="bg-gray-900 p-6 rounded-lg border border-gray-800">
          <div className="flex items-center space-x-4">
            <Users className="w-12 h-12 text-purple-400" />
            <div>
              <h3 className="text-lg font-semibold">Пользователи</h3>
              <p className="text-2xl font-bold">0</p>
            </div>
          </div>
        </div>
        <div className="bg-gray-900 p-6 rounded-lg border border-gray-800">
          <div className="flex items-center space-x-4">
            <DollarSign className="w-12 h-12 text-purple-400" />
            <div>
              <h3 className="text-lg font-semibold">Платежи</h3>
              <p className="text-2xl font-bold">0</p>
            </div>
          </div>
        </div>
      </div>

      <div className="bg-gray-900 p-6 rounded-lg border border-gray-800">
        <h2 className="text-xl font-semibold mb-4">Мониторинг</h2>
        <p className="text-gray-400">Здесь будет отображаться статистика и метрики системы</p>
      </div>
    </div>
  )
}

