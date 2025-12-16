#!/bin/bash

# Скрипт для исправления проблемы с маршрутизацией nginx
# Проблема: запросы к maskbrowser.ru и admin.maskbrowser.ru идут на wbmoneyback.ru

set -e

echo "=== Диагностика проблемы с nginx ==="
echo ""

# 1. Проверяем конфиг wbmoneyback.ru на наличие default_server
echo "1. Проверка default_server в wbmoneyback.ru:"
if grep -q "default_server" /etc/nginx/sites-available/wbmoneyback.ru 2>/dev/null; then
    echo "   ⚠️  НАЙДЕН default_server в wbmoneyback.ru - это проблема!"
    grep "default_server" /etc/nginx/sites-available/wbmoneyback.ru
else
    echo "   ✅ default_server не найден в wbmoneyback.ru"
fi
echo ""

# 2. Проверяем все конфиги на default_server
echo "2. Проверка всех конфигов на default_server:"
grep -r "default_server" /etc/nginx/sites-enabled/ 2>/dev/null || echo "   ✅ default_server не найден ни в одном конфиге"
echo ""

# 3. Проверяем порядок загрузки конфигов
echo "3. Порядок загрузки конфигов (по алфавиту):"
ls -1 /etc/nginx/sites-enabled/ | sort
echo ""

# 4. Показываем содержимое конфига wbmoneyback.ru
echo "4. Содержимое конфига wbmoneyback.ru (первые 30 строк):"
head -30 /etc/nginx/sites-available/wbmoneyback.ru 2>/dev/null || echo "   ⚠️  Файл не найден"
echo ""

# 5. Проверяем, есть ли редиректы в wbmoneyback.ru
echo "5. Проверка редиректов в wbmoneyback.ru:"
if grep -E "(return 301|return 302|rewrite.*permanent)" /etc/nginx/sites-available/wbmoneyback.ru 2>/dev/null; then
    echo "   ⚠️  Найдены редиректы в wbmoneyback.ru"
else
    echo "   ✅ Редиректов не найдено"
fi
echo ""

# 6. Проверяем полную конфигурацию nginx
echo "6. Проверка активной конфигурации nginx:"
echo "   Проверка синтаксиса..."
if nginx -t 2>&1 | grep -q "successful"; then
    echo "   ✅ Синтаксис nginx корректен"
else
    echo "   ❌ Ошибка в конфигурации nginx:"
    nginx -t
    exit 1
fi
echo ""

echo "=== Рекомендации по исправлению ==="
echo ""
echo "Если проблема в default_server:"
echo "  1. Удалите 'default_server' из конфига wbmoneyback.ru"
echo "  2. Или добавьте 'default_server' к правильному конфигу (maskbrowser.ru)"
echo ""
echo "Если проблема в порядке загрузки:"
echo "  1. Переименуйте конфиги, чтобы maskbrowser.ru загружался первым"
echo "  2. Или используйте префиксы: 00-maskbrowser.ru.conf, 01-admin.maskbrowser.ru.conf, 99-wbmoneyback.ru.conf"
echo ""
echo "=== Автоматическое исправление ==="
read -p "Выполнить автоматическое исправление? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo ""
    echo "Исправление..."
    
    # Удаляем default_server из wbmoneyback.ru, если есть
    if [ -f /etc/nginx/sites-available/wbmoneyback.ru ]; then
        sed -i 's/ listen 443 ssl default_server;/ listen 443 ssl;/g' /etc/nginx/sites-available/wbmoneyback.ru
        sed -i 's/ listen \[::\]:443 ssl default_server;/ listen [::]:443 ssl;/g' /etc/nginx/sites-available/wbmoneyback.ru
        sed -i 's/ listen 443 ssl http2 default_server;/ listen 443 ssl http2;/g' /etc/nginx/sites-available/wbmoneyback.ru
        sed -i 's/ listen \[::\]:443 ssl http2 default_server;/ listen [::]:443 ssl http2;/g' /etc/nginx/sites-available/wbmoneyback.ru
        echo "   ✅ Удален default_server из wbmoneyback.ru (если был)"
    fi
    
    # Проверяем синтаксис
    if nginx -t; then
        echo "   ✅ Синтаксис nginx корректен"
        echo ""
        read -p "Перезагрузить nginx? (y/n) " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            systemctl reload nginx
            echo "   ✅ Nginx перезагружен"
        fi
    else
        echo "   ❌ Ошибка в конфигурации nginx после исправления"
        exit 1
    fi
fi

echo ""
echo "=== Проверка после исправления ==="
echo "Проверьте работу сайтов:"
echo "  curl -I https://maskbrowser.ru"
echo "  curl -I https://admin.maskbrowser.ru"
echo "  curl -I https://wbmoneyback.ru"
