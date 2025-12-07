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
# Используем -localhost no для прослушивания на всех интерфейсах (IPv4 и IPv6)
echo "🖥️ Starting VNC server on 0.0.0.0:5900..."
x11vnc -display :99 -nopw -localhost no -rfbport 5900 -xkb -forever -shared -bg -o /tmp/x11vnc.log
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

# Проверяем наличие noVNC и создаем директорию, если нужно
echo "🔍 Checking for noVNC..."
NOVNC_DIR="/usr/share/novnc"

# Проверяем несколько возможных мест
if [ ! -d "$NOVNC_DIR" ] || [ ! -f "$NOVNC_DIR/vnc.html" ]; then
    echo "⚠️ noVNC not found, trying to install..."
    
    # Пытаемся найти в других местах
    for path in "/usr/share/novnc" "/opt/novnc" "/usr/local/share/novnc"; do
        if [ -d "$path" ] && [ -f "$path/vnc.html" ]; then
            NOVNC_DIR="$path"
            echo "✅ Found noVNC at $NOVNC_DIR"
            break
        fi
    done
    
    # Если не нашли, скачиваем
    if [ ! -f "$NOVNC_DIR/vnc.html" ]; then
        echo "📥 Downloading noVNC..."
        mkdir -p "$NOVNC_DIR"
        cd "$NOVNC_DIR" || exit 1
        
        # Пытаемся скачать
        if wget -qO- https://github.com/novnc/noVNC/archive/refs/tags/v1.4.0.tar.gz | tar -xz --strip-components=1 2>/dev/null; then
            echo "✅ noVNC downloaded successfully"
        else
            echo "⚠️ Failed to download noVNC, will try without web interface"
            NOVNC_DIR=""
        fi
    fi
fi

# Запускаем websockify
if [ -n "$NOVNC_DIR" ] && [ -d "$NOVNC_DIR" ] && [ -f "$NOVNC_DIR/vnc.html" ]; then
    echo "✅ noVNC found at $NOVNC_DIR"
    cd "$NOVNC_DIR" || exit 1
    websockify --web="$NOVNC_DIR" --listen 0.0.0.0:6080 localhost:5900 > /tmp/websockify.log 2>&1 &
    WEBSOCKIFY_PID=$!
    echo "✅ websockify started with web interface (PID: $WEBSOCKIFY_PID)"
else
    echo "⚠️ noVNC not available, starting websockify without web interface (WebSocket only)"
    websockify --listen 0.0.0.0:6080 localhost:5900 > /tmp/websockify.log 2>&1 &
    WEBSOCKIFY_PID=$!
    echo "✅ websockify started without web interface (PID: $WEBSOCKIFY_PID)"
    echo "⚠️ NOTE: You'll need to use a VNC client that supports WebSocket connections"
fi

# Даем время на запуск
sleep 5

# Проверяем, что websockify запустился
if ! pgrep -f websockify > /dev/null; then
    echo "❌ ERROR: websockify failed to start"
    echo "📋 websockify log:"
    cat /tmp/websockify.log 2>/dev/null || echo "No log file"
    exit 1
fi

# Проверяем, что websockify слушает на порту 6080
echo "🔍 Checking if websockify is listening on port 6080..."
for i in {1..10}; do
    if netstat -tlnp 2>/dev/null | grep -q ":6080" || ss -tlnp 2>/dev/null | grep -q ":6080"; then
        echo "✅ websockify is listening on port 6080"
        break
    fi
    if [ $i -eq 10 ]; then
        echo "⚠️ WARNING: websockify may not be listening on port 6080"
        echo "📋 websockify log:"
        cat /tmp/websockify.log 2>/dev/null || echo "No log file"
    else
        echo "⏳ Waiting for websockify to start listening (attempt $i/10)..."
        sleep 1
    fi
done

echo "✅ websockify is running on port 6080 (PID: $WEBSOCKIFY_PID)"

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

