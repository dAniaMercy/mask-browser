#!/bin/bash

PROFILE_ID=${PROFILE_ID:-1}
CONFIG=${CONFIG:-'{}'}
NODE_IP=${NODE_IP:-109.172.101.73}

# Start Xvfb
export DISPLAY=:99
Xvfb :99 -screen 0 1920x1080x24 &

# Start window manager
fluxbox &

# Start VNC server (слушаем на всех интерфейсах внутри контейнера)
x11vnc -display :99 -nopw -listen 0.0.0.0 -xkb -forever -shared &

# Start websockify for web access
# websockify слушает на всех интерфейсах (0.0.0.0) для доступа извне контейнера
websockify --web=/usr/share/novnc --listen 0.0.0.0 6080 localhost:5900 &

# Start Chromium with profile
# Используем постоянное хранилище для сохранения данных профиля (монтируется через Docker volume)
PROFILE_DATA_DIR="/app/data/profile"
mkdir -p "${PROFILE_DATA_DIR}"

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

# Keep container running
wait

