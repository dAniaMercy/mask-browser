#!/bin/bash
# Упрощенный и оптимизированный скрипт запуска браузера

set -e  # Остановка при ошибке

PROFILE_ID=${PROFILE_ID:-1}
PROFILE_DATA_DIR="/app/data/profile"
mkdir -p "${PROFILE_DATA_DIR}"

echo "🚀 Starting browser container for profile ${PROFILE_ID}"

# Используем supervisor для управления процессами (если установлен)
# Или запускаем процессы напрямую

# 1. VNC уже должен быть запущен в selenium образе, но проверим
if ! pgrep -x Xvfb > /dev/null; then
    echo "📺 Starting Xvfb..."
    export DISPLAY=:99
    Xvfb :99 -screen 0 1920x1080x24 &
    sleep 2
fi

# 2. Запускаем VNC сервер (если не запущен)
if ! pgrep -x x11vnc > /dev/null; then
    echo "🖥️ Starting VNC server..."
    x11vnc -display :99 -nopw -localhost no -rfbport 5900 -xkb -forever -shared -bg -o /tmp/x11vnc.log
    sleep 2
fi

# 3. Запускаем websockify с noVNC
echo "🌐 Starting websockify..."
NOVNC_DIR="/usr/share/novnc"

if [ -d "$NOVNC_DIR" ] && [ -f "$NOVNC_DIR/vnc.html" ]; then
    cd "$NOVNC_DIR"
    websockify --web="$NOVNC_DIR" --listen 0.0.0.0:6080 localhost:5900 > /tmp/websockify.log 2>&1 &
else
    # Fallback: websockify без веб-интерфейса
    websockify --listen 0.0.0.0:6080 localhost:5900 > /tmp/websockify.log 2>&1 &
fi

WEBSOCKIFY_PID=$!
sleep 3

# Проверяем, что все запустилось
if ! pgrep -f websockify > /dev/null; then
    echo "❌ ERROR: websockify failed to start"
    cat /tmp/websockify.log 2>/dev/null || echo "No log file"
    exit 1
fi

echo "✅ All services started successfully"
echo "📊 Services status:"
echo "   - Xvfb: $(pgrep -x Xvfb > /dev/null && echo '✅ running' || echo '❌ stopped')"
echo "   - VNC: $(pgrep -x x11vnc > /dev/null && echo '✅ running' || echo '❌ stopped')"
echo "   - websockify: $(pgrep -f websockify > /dev/null && echo '✅ running' || echo '❌ stopped')"

# Держим контейнер запущенным
wait

