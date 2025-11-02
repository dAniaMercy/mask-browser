'use client';

import { motion } from 'framer-motion';
import Link from 'next/link';
import { useTranslation } from '@/hooks/useTranslation';
import { Shield, Zap, Server, BarChart3, Lock, Globe } from 'lucide-react';

export default function LandingPage() {
  const { t } = useTranslation();

  const features = [
    {
      icon: Shield,
      title: t('landing.features.feature1'),
      description: t('landing.features.desc1'),
    },
    {
      icon: Zap,
      title: t('landing.features.feature2'),
      description: t('landing.features.desc2'),
    },
    {
      icon: Server,
      title: t('landing.features.feature3'),
      description: t('landing.features.desc3'),
    },
    {
      icon: BarChart3,
      title: t('landing.features.feature4'),
      description: t('landing.features.desc4'),
    },
    {
      icon: Lock,
      title: t('landing.features.feature5'),
      description: t('landing.features.desc5'),
    },
    {
      icon: Globe,
      title: t('landing.features.feature6'),
      description: t('landing.features.desc6'),
    },
  ];

  return (
    <div className="min-h-screen bg-gradient-to-b from-white to-gray-50 dark:from-black dark:to-gray-900">
      <header className="container mx-auto px-4 py-6">
        <nav className="flex items-center justify-between">
          <div className="text-2xl font-bold text-purple-600 dark:text-purple-400">
            MASK BROWSER
          </div>
          <div className="flex items-center space-x-4">
            <Link
              href="/login"
              className="px-4 py-2 text-gray-700 dark:text-gray-300 hover:text-purple-600 dark:hover:text-purple-400"
            >
              {t('common.login')}
            </Link>
            <Link
              href="/register"
              className="px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg transition-colors"
            >
              {t('common.register')}
            </Link>
          </div>
        </nav>
      </header>

      <main className="container mx-auto px-4 py-16">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.6 }}
          className="text-center mb-16"
        >
          <h1 className="text-6xl font-bold mb-4 bg-gradient-to-r from-purple-600 to-blue-600 bg-clip-text text-transparent">
            {t('landing.title')}
          </h1>
          <p className="text-2xl text-gray-600 dark:text-gray-400 max-w-2xl mx-auto">
            {t('landing.subtitle')}
          </p>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 40 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.2 }}
          className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8 mb-16"
        >
          {features.map((feature, index) => (
            <motion.div
              key={index}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.5, delay: 0.3 + index * 0.1 }}
              whileHover={{ scale: 1.05 }}
              className="bg-white dark:bg-gray-900 p-6 rounded-lg border border-gray-200 dark:border-gray-800 shadow-lg hover:shadow-xl transition-shadow"
            >
              <feature.icon className="w-12 h-12 text-purple-600 dark:text-purple-400 mb-4" />
              <h3 className="text-xl font-semibold mb-2">{feature.title}</h3>
              <p className="text-gray-600 dark:text-gray-400">{feature.description}</p>
            </motion.div>
          ))}
        </motion.div>

        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ duration: 0.6, delay: 0.8 }}
          className="text-center"
        >
          <Link
            href="/register"
            className="inline-block px-8 py-4 bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white rounded-lg text-lg font-semibold mr-4 transition-all shadow-lg hover:shadow-xl"
          >
            {t('common.register')}
          </Link>
          <Link
            href="/login"
            className="inline-block px-8 py-4 bg-gray-200 dark:bg-gray-800 hover:bg-gray-300 dark:hover:bg-gray-700 rounded-lg text-lg font-semibold transition-colors"
          >
            {t('common.login')}
          </Link>
        </motion.div>
      </main>
    </div>
  );
}

