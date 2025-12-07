#!/bin/bash
# Скрипт для проверки состояния контейнера профиля

CONTAINER_ID=$1

if [ -z "$CONTAINER_ID" ]; then
    echo "Использование: $0 <container_id>"
    echo "Пример: $0 maskbrowser-profile-18"
    exit 1
fi

echo "🔍 Проверка контейнера: $CONTAINER_ID"
echo ""

# 1. Проверка статуса контейнера
echo "📊 Статус контейнера:"
docker ps -a | grep "$CONTAINER_ID" || echo "❌ Контейнер не найден"
echo ""

# 2. Проверка логов
echo "📋 Последние 50 строк логов:"
docker logs "$CONTAINER_ID" --tail 50
echo ""

# 3. Проверка процессов
echo "🔧 Запущенные процессы:"
docker exec "$CONTAINER_ID" ps aux | grep -E "(supervisor|vnc|websockify|chrome)" || echo "❌ Не удалось проверить процессы"
echo ""

# 4. Проверка supervisor статуса
echo "🎛️ Статус supervisor:"
docker exec "$CONTAINER_ID" supervisorctl status 2>/dev/null || echo "⚠️ Supervisor недоступен или не запущен"
echo ""

# 5. Проверка портов
echo "🔌 Открытые порты:"
docker exec "$CONTAINER_ID" netstat -tlnp 2>/dev/null | grep -E ":(5900|6080)" || \
docker exec "$CONTAINER_ID" ss -tlnp 2>/dev/null | grep -E ":(5900|6080)" || \
echo "⚠️ Не удалось проверить порты"
echo ""

# 6. Проверка noVNC
echo "📁 Проверка noVNC:"
docker exec "$CONTAINER_ID" ls -la /usr/share/novnc/ 2>/dev/null | head -10 || echo "❌ noVNC не найден"
echo ""

# 7. Проверка websockify логов
echo "📝 Логи websockify:"
docker exec "$CONTAINER_ID" cat /var/log/websockify.out.log 2>/dev/null | tail -20 || echo "⚠️ Логи websockify не найдены"
echo ""

# 8. Проверка ошибок websockify
echo "❌ Ошибки websockify:"
docker exec "$CONTAINER_ID" cat /var/log/websockify.err.log 2>/dev/null | tail -20 || echo "⚠️ Логи ошибок не найдены"
echo ""

# 9. Проверка доступности портов с хоста
echo "🌐 Проверка доступности портов с хоста:"
CONTAINER_PORT=$(docker port "$CONTAINER_ID" 6080/tcp 2>/dev/null | cut -d: -f2)
if [ -n "$CONTAINER_PORT" ]; then
    echo "   Порт 6080 проброшен на: $CONTAINER_PORT"
    curl -I "http://localhost:$CONTAINER_PORT/vnc.html" 2>/dev/null | head -5 || echo "   ❌ Порт недоступен"
else
    echo "   ❌ Порт 6080 не проброшен"
fi
echo ""

echo "✅ Проверка завершена"

