#!/bin/bash

# Быстрое исправление проблемы с маршрутизацией nginx
# Удаляет default_server из wbmoneyback.ru и перезагружает nginx

set -e

echo "=== Быстрое исправление маршрутизации nginx ==="
echo ""

# Проверяем наличие файла
if [ ! -f /etc/nginx/sites-available/wbmoneyback.ru ]; then
    echo "❌ Файл /etc/nginx/sites-available/wbmoneyback.ru не найден"
    exit 1
fi

# Создаем резервную копию
echo "1. Создание резервной копии..."
sudo cp /etc/nginx/sites-available/wbmoneyback.ru /etc/nginx/sites-available/wbmoneyback.ru.backup.$(date +%Y%m%d_%H%M%S)
echo "   ✅ Резервная копия создана"

# Удаляем default_server из всех вариантов listen
echo ""
echo "2. Удаление default_server из wbmoneyback.ru..."
sudo sed -i 's/ listen 443 ssl default_server;/ listen 443 ssl;/g' /etc/nginx/sites-available/wbmoneyback.ru
sudo sed -i 's/ listen \[::\]:443 ssl default_server;/ listen [::]:443 ssl;/g' /etc/nginx/sites-available/wbmoneyback.ru
sudo sed -i 's/ listen 443 ssl http2 default_server;/ listen 443 ssl http2;/g' /etc/nginx/sites-available/wbmoneyback.ru
sudo sed -i 's/ listen \[::\]:443 ssl http2 default_server;/ listen [::]:443 ssl http2;/g' /etc/nginx/sites-available/wbmoneyback.ru
sudo sed -i 's/ listen 80 default_server;/ listen 80;/g' /etc/nginx/sites-available/wbmoneyback.ru
sudo sed -i 's/ listen \[::\]:80 default_server;/ listen [::]:80;/g' /etc/nginx/sites-available/wbmoneyback.ru

# Проверяем результат
if grep -q "default_server" /etc/nginx/sites-available/wbmoneyback.ru; then
    echo "   ⚠️  Внимание: default_server все еще присутствует в файле"
    echo "   Проверьте файл вручную:"
    grep "default_server" /etc/nginx/sites-available/wbmoneyback.ru
else
    echo "   ✅ default_server удален"
fi

# Проверяем синтаксис nginx
echo ""
echo "3. Проверка синтаксиса nginx..."
if sudo nginx -t 2>&1 | grep -q "successful"; then
    echo "   ✅ Синтаксис nginx корректен"
else
    echo "   ❌ Ошибка в конфигурации nginx:"
    sudo nginx -t
    echo ""
    echo "   Восстановление из резервной копии..."
    sudo cp /etc/nginx/sites-available/wbmoneyback.ru.backup.* /etc/nginx/sites-available/wbmoneyback.ru
    exit 1
fi

# Перезагружаем nginx
echo ""
echo "4. Перезагрузка nginx..."
sudo systemctl reload nginx
echo "   ✅ Nginx перезагружен"

echo ""
echo "=== Исправление завершено ==="
echo ""
echo "Проверьте работу сайтов:"
echo "  curl -I https://maskbrowser.ru"
echo "  curl -I https://admin.maskbrowser.ru"
echo "  curl -I https://wbmoneyback.ru"
echo ""
echo "Если проблема сохраняется, проверьте:"
echo "  1. Порядок загрузки конфигов: ls -la /etc/nginx/sites-enabled/"
echo "  2. Логи nginx: sudo tail -f /var/log/nginx/error.log"
echo "  3. Полную конфигурацию: sudo nginx -T | grep -A 5 'server_name'"
