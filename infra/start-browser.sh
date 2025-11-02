#!/bin/bash

PROFILE_ID=${PROFILE_ID:-1}
CONFIG=${CONFIG:-'{}'}
NODE_IP=${NODE_IP:-localhost}

# Start Xvfb
export DISPLAY=:99
Xvfb :99 -screen 0 1920x1080x24 &

# Start window manager
fluxbox &

# Start VNC server
x11vnc -display :99 -nopw -listen localhost -xkb -forever -shared &

# Start websockify for web access
websockify --web=/usr/share/novnc 6080 localhost:5900 &

# Start Chromium with profile
mkdir -p /tmp/profile-${PROFILE_ID}
chromium-browser \
    --no-sandbox \
    --disable-dev-shm-usage \
    --disable-gpu \
    --user-data-dir=/tmp/profile-${PROFILE_ID} \
    --remote-debugging-port=9222 \
    --remote-allow-origins=* \
    --disable-web-security \
    --disable-features=IsolateOrigins,site-per-process \
    https://google.com &

# Keep container running
wait

