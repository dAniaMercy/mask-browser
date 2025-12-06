#!/bin/bash

PROFILE_ID=${PROFILE_ID:-1}
CONFIG=${CONFIG:-'{}'}
NODE_IP=${NODE_IP:-109.172.101.73}

echo "🚀 Starting browser container for profile ${PROFILE_ID}"

# Start Xvfb
export DISPLAY=:99
echo "📺 Starting Xvfb..."
Xvfb :99 -screen 0 1920x1080x24 &
sleep 2

# Start window manager
echo "🪟 Starting fluxbox..."
fluxbox &
sleep 1

# Start VNC server (слушаем на всех интерфейсах внутри контейнера)
echo "🖥️ Starting VNC server on 0.0.0.0:5900..."
x11vnc -display :99 -nopw -listen 0.0.0.0 -xkb -forever -shared &
sleep 3

# Проверяем, что VNC сервер запустился
if ! pgrep -x x11vnc > /dev/null; then
    echo "❌ ERROR: VNC server failed to start"
    exit 1
fi
echo "✅ VNC server is running"

# Start websockify for web access
# websockify слушает на всех интерфейсах (0.0.0.0) для доступа извне контейнера
echo "🌐 Starting websockify on 0.0.0.0:6080..."

# Проверяем наличие noVNC
if [ -d "/usr/share/novnc" ]; then
    echo "✅ noVNC found at /usr/share/novnc"
    websockify --web=/usr/share/novnc --listen 0.0.0.0 6080 localhost:5900 &
else
    echo "⚠️ noVNC not found, starting websockify without web interface"
    # Запускаем websockify без веб-интерфейса (только WebSocket)
    websockify --listen 0.0.0.0 6080 localhost:5900 &
fi

sleep 3

# Проверяем, что websockify запустился
if ! pgrep -f websockify > /dev/null; then
    echo "❌ ERROR: websockify failed to start"
    exit 1
fi
echo "✅ websockify is running on port 6080"

# Проверяем доступность портов
echo "🔍 Checking ports..."
netstat -tlnp 2>/dev/null | grep -E ":(5900|6080)" || ss -tlnp 2>/dev/null | grep -E ":(5900|6080)" || echo "⚠️ netstat/ss not available, skipping port check"

# Start Chromium with profile
# Используем постоянное хранилище для сохранения данных профиля (монтируется через Docker volume)
PROFILE_DATA_DIR="/app/data/profile"
mkdir -p "${PROFILE_DATA_DIR}"

echo "🌐 Starting Chromium browser..."
chromium-browser \
    --no-sandbox \
    --disable-dev-shm-usage \
    --disable-gpu \
    --user-data-dir="${PROFILE_DATA_DIR}" \
    --remote-debugging-port=9222 \
    --remote-allow-origins=* \
    --disable-web-security \
    --disable-features=IsolateOrigins,site-per-process \
    https://google.com &

echo "✅ All services started. Container is ready."
echo "📊 Services status:"
echo "   - Xvfb: $(pgrep -x Xvfb > /dev/null && echo 'running' || echo 'stopped')"
echo "   - VNC: $(pgrep -x x11vnc > /dev/null && echo 'running' || echo 'stopped')"
echo "   - websockify: $(pgrep -f websockify > /dev/null && echo 'running' || echo 'stopped')"
echo "   - Chromium: $(pgrep -x chromium-browser > /dev/null && echo 'running' || echo 'stopped')"

# Keep container running
wait

