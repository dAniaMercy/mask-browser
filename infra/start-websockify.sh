#!/bin/bash
# Оптимизированный скрипт запуска websockify
# VNC уже запущен в selenium образе через supervisor

set -e

echo "🚀 Starting websockify for noVNC..."

NOVNC_DIR="/usr/share/novnc"

# Ждем, пока VNC сервер запустится (Selenium образ использует supervisor)
echo "⏳ Waiting for VNC server to be ready on port 5900..."
for i in {1..60}; do
    # Проверяем доступность порта 5900
    if nc -z localhost 5900 2>/dev/null || \
       netstat -tlnp 2>/dev/null | grep -q ":5900" || \
       ss -tlnp 2>/dev/null | grep -q ":5900"; then
        echo "✅ VNC server is ready on port 5900"
        break
    fi
    if [ $i -eq 60 ]; then
        echo "⚠️ WARNING: VNC server may not be ready after 60 seconds, but continuing..."
    else
        sleep 1
    fi
done

# Проверяем наличие noVNC
if [ -d "$NOVNC_DIR" ] && [ -f "$NOVNC_DIR/vnc.html" ]; then
    cd "$NOVNC_DIR"
    echo "✅ Starting websockify with noVNC web interface on port 6080..."
    echo "📋 noVNC directory: $NOVNC_DIR"
    echo "🌐 Web interface will be available at: http://<host>:6080/vnc.html"
    exec websockify --web="$NOVNC_DIR" --listen 0.0.0.0:6080 localhost:5900
else
    echo "⚠️ noVNC not found at $NOVNC_DIR"
    echo "⚠️ Starting websockify without web interface (WebSocket only)..."
    echo "⚠️ NOTE: You'll need a VNC client that supports WebSocket connections"
    exec websockify --listen 0.0.0.0:6080 localhost:5900
fi

